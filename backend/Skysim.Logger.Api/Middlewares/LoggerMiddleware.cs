using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Http.Extensions;
using Skysim.Logger.Api.Common;
using Skysim.Logger.Api.Infrastructure.Kafka;
using Skysim.Logger.Common.Masking;
using LogEventMessage = Skysim.Logger.Contracts.Events.LogEventMessage;
using StatusTypes = Skysim.Logger.Contracts.Constants.StatusTypes;
using FlowTypes = Skysim.Logger.Contracts.Constants.FlowTypes;
using ActionTypes = Skysim.Logger.Contracts.Constants.ActionTypes;
using SensitiveFields = Skysim.Logger.Common.Masking.SensitiveFields;

namespace Skysim.Logger.Api.Middlewares;

public class LoggerMiddleware
{
    private static readonly string[] SelectedRequestHeaders = ["x-flow-id", "x-correlation-id"];
    private static readonly string[] LargeResponseContentTypes = ["application/octet-stream"];
    private const int ResponseBodySizeLimit = 64 * 1024;

    private readonly RequestDelegate _next;
    private readonly IKafkaLogProducer _producer;
    private readonly ISensitiveDataMasker _masker;
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
        ISensitiveDataMasker masker,
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
        var selectedHeaders = CaptureSelectedHeaders(context.Request);

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

            try
            {
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

                var maskedJson = _masker.MaskJson(JsonSerializer.Serialize(message, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }));
                var maskedMessage = JsonSerializer.Deserialize<LogEventMessage>(maskedJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (maskedMessage != null)
                {
                    _ = FireAndForgetPublishAsync(maskedMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to build log event message. FlowId={FlowId}",
                    flowId);
            }
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
        if (context.Request.Headers.TryGetValue("X-Flow-Id", out var flowIdHeader)
            && !string.IsNullOrWhiteSpace(flowIdHeader))
        {
            return flowIdHeader.ToString();
        }

        if (context.Request.Headers.TryGetValue("X-Correlation-Id", out var correlationIdHeader)
            && !string.IsNullOrWhiteSpace(correlationIdHeader))
        {
            return correlationIdHeader.ToString();
        }

        if (context.Request.Headers.TryGetValue("X-Request-ID", out var requestIdHeader)
            && !string.IsNullOrWhiteSpace(requestIdHeader))
        {
            return requestIdHeader.ToString();
        }

        if (!string.IsNullOrWhiteSpace(context.TraceIdentifier))
        {
            return context.TraceIdentifier;
        }

        var generated = Guid.NewGuid().ToString("N");
        context.Response.Headers["X-Correlation-ID"] = generated;
        return generated;
    }

    private static string? ExtractUserId(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value
            ?? context.User.FindFirst("userId")?.Value;

        return string.IsNullOrWhiteSpace(userId) ? null : userId;
    }

    private static async Task<byte[]?> ReadRequestBodyAsync(HttpRequest request)
    {
        if (!request.Body.CanSeek)
        {
            return null;
        }

        request.Body.Position = 0;
        using var memoryStream = new MemoryStream();
        await request.Body.CopyToAsync(memoryStream);
        request.Body.Position = 0;

        if (memoryStream.Length == 0)
        {
            return null;
        }
        return memoryStream.ToArray();
    }

    private static Dictionary<string, string> CaptureSelectedHeaders(HttpRequest request)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var headerName in SelectedRequestHeaders)
        {
            if (request.Headers.TryGetValue(headerName, out var value)
                && !string.IsNullOrWhiteSpace(value))
            {
                headers[headerName] = value.ToString();
            }
        }
        return headers;
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
        Dictionary<string, string> selectedHeaders)
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

        var status = MapStatus(statusCode, exception);
        var errorCode = exception != null ? statusCode.ToString() : null;
        var errorMessage = exception?.Message;
        var exceptionText = exception != null ? exception.ToString() : null;

        return new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = flowId,
            FlowType = FlowTypes.HttpAction,
            ActionType = ActionTypes.HttpRequest,
            Status = status,
            CreatedAt = requestTime,
            RequestTime = requestTime,
            ResponseTime = responseTime,
            Duration = duration,
            CorrelationId = flowId,
            UserId = ExtractUserId(context),
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Exception = exceptionText,
            Message = $"{context.Request.Method} {fullPath} -> {statusCode}",
            RequestData = requestData,
            ResponseData = BuildResponseData(statusCode, responseBody, context.Response.ContentType)
        };
    }

    private static JsonElement? BuildRequestData(
        string method,
        string path,
        string queryString,
        byte[]? body,
        string? contentType,
        Dictionary<string, string> selectedHeaders)
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

        writer.WritePropertyName("selectedHeaders");
        WriteHeaders(writer, selectedHeaders);

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

    private static void WriteHeaders(Utf8JsonWriter writer, Dictionary<string, string> headers)
    {
        writer.WriteStartObject();

        foreach (var kvp in headers)
        {
            writer.WriteString(kvp.Key, kvp.Value);
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

    private static string MapStatus(int statusCode, Exception? exception)
    {
        if (exception != null || statusCode >= 500)
        {
            return StatusTypes.Failed;
        }
        return statusCode >= 200 && statusCode < 300 ? StatusTypes.Success : StatusTypes.Failed;
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

/// <summary>
/// A stream wrapper that buffers the response body in memory for later reading,
/// while also writing to the underlying stream for actual response delivery.
/// </summary>
internal sealed class ResponseBodyBufferingStream : Stream
{
    private readonly Stream _innerStream;
    private readonly MemoryStream _buffer = new();

    public ResponseBodyBufferingStream(Stream innerStream)
    {
        _innerStream = innerStream;
    }

    public byte[] GetBuffer() => _buffer.ToArray();

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => _buffer.Length;
    public override long Position
    {
        get => _buffer.Position;
        set => _buffer.Position = value;
    }

    public override void Flush() => _innerStream.Flush();

    public override int Read(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) =>
        throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        _buffer.Write(buffer, offset, count);
        _innerStream.Write(buffer, offset, count);
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await _innerStream.FlushAsync(cancellationToken);
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await _buffer.WriteAsync(buffer, offset, count, cancellationToken);
        await _innerStream.WriteAsync(buffer, offset, count, cancellationToken);
    }
}
