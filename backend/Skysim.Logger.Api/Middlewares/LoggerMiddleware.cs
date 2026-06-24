using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Http.Extensions;
using Skysim.Logger.Api.Common;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Domain.Enums;
using Skysim.Logger.Api.Infrastructure.Kafka;

namespace Skysim.Logger.Api.Middlewares;

public class LoggerMiddleware
{
    private static readonly string[] SelectedRequestHeaders = ["x-flow-id", "x-correlation-id"];
    private static readonly string[] LargeResponseContentTypes = ["application/octet-stream"];
    private const int ResponseBodySizeLimit = 64 * 1024;

    private readonly RequestDelegate _next;
    private readonly IKafkaLogProducer _producer;
    private readonly SensitiveDataMasker _masker;
    private readonly ILogger<LoggerMiddleware> _logger;

    private static readonly string[] ExcludedPathPrefixes =
    [
        "/swagger",
        "/api/log-flows",
        "/api/log-actions",
        "/favicon.ico",
        "/health"
    ];

    public LoggerMiddleware(
        RequestDelegate next,
        IKafkaLogProducer producer,
        SensitiveDataMasker masker,
        ILogger<LoggerMiddleware> logger)
    {
        _next = next;
        _producer = producer;
        _masker = masker;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestPath = context.Request.Path.ToString().ToLowerInvariant();

        if (IsExcludedPath(requestPath))
        {
            await _next(context);
            return;
        }

        var requestTime = DateTime.UtcNow;
        var flowId = GetOrCreateFlowId(context);
        var requestBody = await ReadRequestBodyAsync(context.Request);
        var selectedHeaders = SelectedRequestHeaders
            .Select(header => new { Header = header, Value = context.Request.Headers[header].ToString() })
            .ToArray();

        // Replace response body with a buffering wrapper so we can read it back
        var originalResponseBody = context.Response.Body;
        await using var bufferingStream = new ResponseBodyBufferingStream(originalResponseBody);
        context.Response.Body = bufferingStream;

        Exception? caughtException = null;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            caughtException = ex;
            throw;
        }
        finally
        {
            // Restore original response body so WriteAsync flushes to the real stream
            context.Response.Body = originalResponseBody;

            var responseTime = DateTime.UtcNow;
            var statusCode = context.Response.StatusCode;
            var duration = (int)(responseTime - requestTime).TotalMilliseconds;
            var responseBody = bufferingStream.GetBuffer();

            var message = BuildLogEventMessage(
                context,
                flowId,
                requestTime,
                responseTime,
                statusCode,
                duration,
                requestBody,
                responseBody,
                caughtException,
                selectedHeaders);

            _masker.Mask(message);

            _ = FireAndForgetPublishAsync(message);
        }
    }

    private static bool IsExcludedPath(string path)
    {
        foreach (var prefix in ExcludedPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static string GetOrCreateFlowId(HttpContext context)
    {
        // 1. X-Flow-Id header
        if (context.Request.Headers.TryGetValue("X-Flow-Id", out var flowIdHeader)
            && !string.IsNullOrWhiteSpace(flowIdHeader))
        {
            return flowIdHeader.ToString();
        }

        // 2. X-Correlation-Id header
        if (context.Request.Headers.TryGetValue("X-Correlation-Id", out var correlationIdHeader)
            && !string.IsNullOrWhiteSpace(correlationIdHeader))
        {
            return correlationIdHeader.ToString();
        }

        // 3. X-Request-ID header
        if (context.Request.Headers.TryGetValue("X-Request-ID", out var requestIdHeader)
            && !string.IsNullOrWhiteSpace(requestIdHeader))
        {
            return requestIdHeader.ToString();
        }

        // 4. HttpContext.TraceIdentifier
        if (!string.IsNullOrWhiteSpace(context.TraceIdentifier))
        {
            return context.TraceIdentifier;
        }

        // 5. Fallback to new Guid (format "N" = 32 hex chars without dashes)
        var generated = Guid.NewGuid().ToString("N");
        context.Response.Headers["X-Correlation-ID"] = generated;
        return generated;
    }

    private static async Task<byte[]?> ReadRequestBodyAsync(HttpRequest request)
    {
        request.Body.Position = 0;
        if (!request.Body.CanSeek)
        {
            return null;
        }

        using var memoryStream = new MemoryStream();
        await request.Body.CopyToAsync(memoryStream);
        request.Body.Position = 0; // reset so downstream handlers can read it

        if (memoryStream.Length == 0)
        {
            return null;
        }
        return memoryStream.ToArray();
    }

    private LogEventMessage BuildLogEventMessage(
        HttpContext context,
        string flowId,
        DateTime requestTime,
        DateTime responseTime,
        int statusCode,
        int duration,
        byte[]? requestBody,
        byte[] responseBody,
        Exception? exception,
        object selectedHeaders)
    {
        var requestPath = context.Request.Path.ToString();
        var queryString = context.Request.QueryString.HasValue
            ? context.Request.QueryString.Value
            : string.Empty;
        var requestData = BuildRequestData(
            context.Request.Method,
            requestPath,
            queryString,
            requestBody,
            context.Request.ContentType,
            selectedHeaders);

        var fullPath = string.IsNullOrEmpty(queryString)
            ? requestPath
            : $"{requestPath}{queryString}";

        var actionType = ActionType.HttpRequest;
        var status = MapStatus(statusCode, exception);
        var errorCode = exception != null ? statusCode.ToString() : null;
        var errorMessage = exception?.Message;
        var exceptionText = exception != null ? exception.ToString() : null;

        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = flowId,
            FlowType = FlowType.HttpAction,
            ActionType = actionType,
            Status = status,
            CreatedAt = requestTime,
            RequestTime = requestTime,
            ResponseTime = responseTime,
            Duration = duration,
            CorrelationId = flowId,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Exception = exceptionText,
            Message = $"{context.Request.Method} {fullPath} -> {statusCode}",
            RequestData = requestData,
            ResponseData = BuildResponseData(statusCode, responseBody, context.Response.ContentType)
        };

        return message;
    }

    private static JsonElement? BuildRequestData(
        string method,
        string path,
        string queryString,
        byte[]? body,
        string? contentType,
        object selectedHeaders)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteString("method", method);
        writer.WriteString("path", path);

        if (!string.IsNullOrEmpty(queryString))
        {
            writer.WritePropertyName("query");
            WriteMaskedQueryString(writer, queryString);
        }

        WriteHeaders(writer, "selectedHeaders", selectedHeaders);

        if (body != null && body.Length > 0 && IsJsonContentType(contentType))
        {
            writer.WritePropertyName("body");
            try
            {
                using var bodyDocument = JsonDocument.Parse(body);
                bodyDocument.RootElement.WriteTo(writer);
            }
            catch
            {
                writer.WriteStringValue(Encoding.UTF8.GetString(body));
            }
        }

        writer.WriteEndObject();
        writer.Flush();

        try
        {
            using var doc = JsonDocument.Parse(stream.ToArray());
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static JsonElement? BuildResponseData(int statusCode, byte[] responseBody, string? contentType)
    {
        if (!IsJsonContentType(contentType) || IsLargeBinaryContentType(contentType) || responseBody.Length > ResponseBodySizeLimit)
        {
            return CreateStatusCodeObject(statusCode);
        }

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            using var statusStream = new MemoryStream();
            using var statusWriter = new Utf8JsonWriter(statusStream);

            statusWriter.WriteStartObject();
            statusWriter.WriteNumber("statusCode", statusCode);
            statusWriter.WritePropertyName("body");
            doc.RootElement.WriteTo(statusWriter);
            statusWriter.WriteEndObject();
            statusWriter.Flush();

            using var mergedDoc = JsonDocument.Parse(statusStream.ToArray());
            return mergedDoc.RootElement.Clone();
        }
        catch
        {
            return CreateStatusCodeObject(statusCode);
        }
    }

    private static void WriteMaskedQueryString(Utf8JsonWriter writer, string queryString)
    {
        var query = HttpUtility.ParseQueryString(queryString);
        writer.WriteStartObject();

        foreach (string? key in query.AllKeys)
        {
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            var values = query.GetValues(key);
            var value = values is { Length: > 0 } ? values[0] : string.Empty;
            var maskedValue = SensitiveFields.Instance.IsSensitive(key) ? "***" : value;

            writer.WriteString(key, maskedValue);
        }

        writer.WriteEndObject();
    }

    private static void WriteHeaders(Utf8JsonWriter writer, string propertyName, object selectedHeaders)
    {
        writer.WriteStartObject(propertyName);

        foreach (var header in (System.Collections.IEnumerable)selectedHeaders)
        {
            var headerType = header.GetType();
            var headerName = headerType.GetProperty("Header")!.GetValue(header)!.ToString();
            var headerValue = headerType.GetProperty("Value")!.GetValue(header)!.ToString();

            writer.WriteString(headerName!, headerValue);
        }

        writer.WriteEndObject();
    }

    private static JsonElement? CreateStatusCodeObject(int statusCode)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteNumber("statusCode", statusCode);
        writer.WriteEndObject();
        writer.Flush();

        using var doc = JsonDocument.Parse(stream.ToArray());
        return doc.RootElement.Clone();
    }

    private static bool IsJsonContentType(string? contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType)
            && contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLargeBinaryContentType(string? contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType)
            && contentType.Contains("application/octet-stream", StringComparison.OrdinalIgnoreCase);
    }

    private static JsonElement? ParseJsonElement(byte[] data)
    {
        try
        {
            using var doc = JsonDocument.Parse(data);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static Status MapStatus(int statusCode, Exception? exception)
    {
        if (exception != null || statusCode >= 500)
        {
            return Status.Failed;
        }
        return statusCode >= 200 && statusCode < 300 ? Status.Success : Status.Failed;
    }

    private async Task FireAndForgetPublishAsync(LogEventMessage message)
    {
        try
        {
            await _producer.PublishAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Kafka publish failed for LogEventMessage. EventId={EventId}, FlowId={FlowId}",
                message.EventId,
                message.FlowId);
        }
    }
}
