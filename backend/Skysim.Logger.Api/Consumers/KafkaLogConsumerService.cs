using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Skysim.Logger.Contracts.Events;
using Skysim.Logger.Api.Infrastructure.Persistence.Exceptions;
using Skysim.Logger.Api.Kafka;
using Skysim.Logger.Client.Masking;
using Skysim.Logger.Infrastructure.Data;
using Skysim.Logger.Infrastructure.Entities;
using Skysim.Logger.Infrastructure.Repositories;
using StatusTypes = Skysim.Logger.Contracts.Constants.StatusTypes;
using ActionTypes = Skysim.Logger.Contracts.Constants.ActionTypes;
using FlowTypes = Skysim.Logger.Contracts.Constants.FlowTypes;
using Skysim.Logger.Api.Domain.Services;

namespace Skysim.Logger.Api.Consumers;

public class KafkaLogConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaConsumerOptions _options;
    private readonly IDlqPublisher _dlqPublisher;
    private readonly ILogger<KafkaLogConsumerService> _logger;
    private readonly ISensitiveDataMasker _masker;

    private IConsumer<byte[], byte[]>? _consumer;

    public KafkaLogConsumerService(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaConsumerOptions> options,
        IDlqPublisher dlqPublisher,
        ILogger<KafkaLogConsumerService> logger,
        ISensitiveDataMasker masker)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _dlqPublisher = dlqPublisher;
        _logger = logger;
        _masker = masker;
    }

    private ResiliencePipeline CreateDbRetryPolicy()
    {
        return RetryPolicyFactory.CreateDbRetryPolicy(_options.Retry);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting KafkaLogConsumerService. BootstrapServers={BootstrapServers}, Topic={Topic}, ConsumerGroup={ConsumerGroup}",
            _options.Consumer.BootstrapServers,
            _options.Consumer.Topic,
            _options.Consumer.ConsumerGroup);

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _options.Consumer.BootstrapServers,
            GroupId = _options.Consumer.ConsumerGroup,
            AutoOffsetReset = ParseAutoOffsetReset(_options.Consumer.AutoOffsetReset),
            EnableAutoCommit = false,
            MaxPollIntervalMs = _options.Consumer.MaxPollIntervalMs,
            SessionTimeoutMs = _options.Consumer.SessionTimeoutMs
        };

        _consumer = new ConsumerBuilder<byte[], byte[]>(consumerConfig).Build();
        _consumer.Subscribe(_options.Consumer.Topic);

        _logger.LogInformation("Subscribed to topic: {Topic}", _options.Consumer.Topic);

        await Task.Yield();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<byte[], byte[]>? consumeResult = null;
                try
                {
                    consumeResult = _consumer.Consume(stoppingToken);

                    if (consumeResult == null)
                    {
                        continue;
                    }

                    await ProcessMessageAsync(consumeResult, stoppingToken);

                    _consumer.Commit(consumeResult);
                }
                catch (ConsumeException ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Kafka consume error. ErrorCode={ErrorCode}", ex.Error.Code);

                    if (consumeResult != null)
                    {
                        await PublishToDlqSafelyAsync(consumeResult, $"CONSUME_ERROR: {ex.Error.Code}", 0, stoppingToken);
                        _consumer.Commit(consumeResult);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Unexpected error during message processing.");

                    if (consumeResult != null)
                    {
                        await PublishToDlqSafelyAsync(consumeResult, $"PROCESSING_ERROR: {ex.Message}", 0, stoppingToken);
                        _consumer.Commit(consumeResult);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("KafkaLogConsumerService is stopping due to cancellation");
        }
        finally
        {
            _consumer?.Close();
            _consumer?.Dispose();
            _logger.LogInformation("KafkaLogConsumerService stopped");
        }
    }

    private async Task ProcessMessageAsync(ConsumeResult<byte[], byte[]> result, CancellationToken ct)
    {
        var rawPayload = result.Message.Value;
        LogEventMessage? message = null;

        try
        {
            message = LogEventMessage.Deserialize(rawPayload);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to deserialize message. RawPayload={RawPayload}, Partition={Partition}, Offset={Offset}",
                Encoding.UTF8.GetString(rawPayload),
                result.Partition.Value,
                result.Offset.Value);

            await PublishToDlqAndCommitAsync(result, "DESERIALIZATION_FAILED: " + ex.Message, ct);
            return;
        }

        if (message == null)
        {
            await PublishToDlqAndCommitAsync(result, "DESERIALIZATION_FAILED: null message", ct);
            return;
        }

        NormalizeServiceName(message);

        using var validationScope = _scopeFactory.CreateScope();
        var validator = validationScope.ServiceProvider.GetRequiredService<IValidator<LogEventMessage>>();
        var validationResult = await validator.ValidateAsync(message, ct);

        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            _logger.LogWarning(
                "Message validation failed. EventId={EventId}, FlowId={FlowId}, Errors={Errors}",
                message.EventId,
                message.FlowId,
                errors);

            await PublishToDlqAndCommitAsync(result, "VALIDATION_FAILED: " + errors, ct);
            return;
        }

        var attempt = 0;
        var dbRetryPolicy = CreateDbRetryPolicy();
        var success = await TryPersistWithRetryAsync(message, ct, () => attempt++, dbRetryPolicy);

        if (success)
        {
            _logger.LogInformation(
                "Message persisted successfully. EventId={EventId}, FlowId={FlowId}, ActionType={ActionType}, Status={Status}",
                message.EventId,
                message.FlowId,
                message.ActionType,
                message.Status);
        }
        else
        {
            await PublishToDlqAndCommitAsync(result, "PERSISTENCE_FAILED: Max retries exceeded", ct);
        }
    }

    private async Task PublishToDlqAndCommitAsync(ConsumeResult<byte[], byte[]> result, string reason, CancellationToken ct)
    {
        try
        {
            await _dlqPublisher.PublishAsync(result, reason, 0, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish to DLQ. Reason={Reason}",
                reason);
        }
    }

    private async Task PublishToDlqSafelyAsync(ConsumeResult<byte[], byte[]> result, string reason, int attempt, CancellationToken ct)
    {
        try
        {
            await _dlqPublisher.PublishAsync(result, reason, attempt, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish to DLQ safely. Reason={Reason}",
                reason);
        }
    }

    private async Task<bool> TryPersistWithRetryAsync(LogEventMessage message, CancellationToken ct, Action onAttempt, ResiliencePipeline retryPolicy)
    {
        return await retryPolicy.ExecuteAsync(async token =>
        {
            onAttempt();
            var success = await TryPersistAsync(message, token);
            if (!success)
            {
                throw new InvalidOperationException("Persistence returned false, triggering retry");
            }
            return true;
        }, ct);
    }

    private async Task<bool> TryPersistAsync(LogEventMessage message, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LoggerDbContext>();
        var flowRepo = scope.ServiceProvider.GetRequiredService<ILogFlowRepository>();
        var actionRepo = scope.ServiceProvider.GetRequiredService<ILogActionRepository>();
        var detailRepo = scope.ServiceProvider.GetRequiredService<ILogActionDetailRepository>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            var flow = await flowRepo.UpsertAsync(message.FlowId, f => MapFlowFromMessage(f, message), ct);

            var action = CreateLogAction(message, flow.Id);
            action = await actionRepo.InsertAsync(action, ct);

            await TryUpsertDetailAsync(detailRepo, action.Id, message, ct);

            await transaction.CommitAsync(ct);
            return true;
        }
        catch (DuplicateEventException dex)
        {
            _logger.LogInformation(
                "Duplicate event detected (idempotent skip). EventId={EventId}",
                dex.EventId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist message. EventId={EventId}, FlowId={FlowId}",
                message.EventId,
                message.FlowId);
            throw;
        }
    }

    private static void MapFlowFromMessage(LogFlow flow, LogEventMessage message)
    {
        // 1.2: Upgrade flowType: set to CHECKOUT_ESIM for business messages.
        // Handles both new flows (string.Empty -> CHECKOUT_ESIM) and HTTP_ACTION -> CHECKOUT_ESIM upgrades.
        if (message.FlowType == FlowTypes.CheckoutEsim)
        {
            flow.FlowType = message.FlowType;
        }

        // 1.3: Merge business fields: use incoming non-null values, preserve existing non-null values
        flow.CheckoutType ??= message.CheckoutType;
        flow.CustomerEmail ??= message.CustomerEmail;
        flow.CustomerPhone ??= message.CustomerPhone;
        flow.UserId ??= message.UserId;
        flow.OrderId ??= message.OrderId;
        flow.PaymentId ??= message.PaymentId;

        // Update status from latest event
        flow.Status = message.Status;
        flow.StartedAt = message.CreatedAt;

        // 1.4: Preserve lastActionType/lastMessage: HTTP_REQUEST after CHECKOUT_ESIM should not overwrite business action
        // HTTP_ACTION-only flows: update normally from HTTP_REQUEST
        // CHECKOUT_ESIM flows with HTTP_REQUEST arriving later: preserve last business action
        bool isExistingBusinessFlow = flow.FlowType == FlowTypes.CheckoutEsim;
        bool isHttpRequest = message.ActionType == ActionTypes.HttpRequest;

        if (isHttpRequest && isExistingBusinessFlow)
        {
            // Preserve existing lastActionType/lastMessage (business action preserved)
        }
        else
        {
            flow.LastActionType = message.ActionType;
            flow.LastMessage = message.Message;
        }

        if (message.Status == StatusTypes.Success)
        {
            flow.SuccessSteps++;
            if (FlowDomainService.IsTerminalAction(message.ActionType, message.Status))
            {
                flow.CompletedAt = DateTime.UtcNow;
            }
        }
        else if (message.Status == StatusTypes.Failed)
        {
            flow.FailedSteps++;
            flow.CompletedAt = DateTime.UtcNow;
        }
        flow.TotalSteps++;
    }

    private static LogAction CreateLogAction(LogEventMessage message, Guid flowId)
    {
        var durationMs = message.Duration;
        if (!durationMs.HasValue && message.RequestTime.HasValue && message.ResponseTime.HasValue)
        {
            durationMs = (int)(message.ResponseTime.Value - message.RequestTime.Value).TotalMilliseconds;
        }

        return new LogAction
        {
            EventId = message.EventId,
            FlowId = message.FlowId,
            StepOrder = 0,
            ServiceName = message.ServiceName,
            ActionType = message.ActionType,
            Status = message.Status,
            Message = message.Message,
            ErrorCode = message.ErrorCode,
            ErrorMessage = message.ErrorMessage,
            RequestTime = message.RequestTime,
            ResponseTime = message.ResponseTime,
            DurationMs = durationMs,
            CorrelationId = message.CorrelationId
        };
    }

    private async Task TryUpsertDetailAsync(
        ILogActionDetailRepository detailRepo,
        Guid actionId,
        LogEventMessage message,
        CancellationToken ct)
    {
        var requestObj = BuildRequestPayload(message);
        var responseObj = BuildResponsePayload(message);
        var errorObj = BuildErrorPayload(message);
        var metadataObj = BuildMetadataPayload(message);

        bool hasRequest = requestObj != null;
        bool hasResponse = responseObj != null;
        bool hasError = errorObj != null;
        bool hasMetadata = metadataObj != null;

        if (!hasRequest && !hasResponse && !hasError && !hasMetadata)
        {
            return;
        }

        var detail = new LogActionDetail
        {
            ActionId = actionId
        };

        if (hasRequest)
        {
            var maskedHeaders = _masker.MaskHeaders(message.RequestHeaders);
            var maskedBody = _masker.MaskBody(message.RequestBody);

            var maskedRequestObj = new
            {
                method = message.Method,
                path = message.Path,
                queryString = message.QueryString,
                fullUrl = message.FullUrl,
                clientIp = message.ClientIp,
                sourceService = message.SourceService,
                headers = maskedHeaders,
                body = maskedBody
            };

            detail.RequestPayload = JsonSerializer.Serialize(maskedRequestObj);
        }

        if (hasResponse)
        {
            var maskedHeaders = _masker.MaskHeaders(message.ResponseHeaders);
            var maskedBody = _masker.MaskBody(message.ResponseBody);

            var maskedResponseObj = new
            {
                statusCode = message.StatusCode,
                headers = maskedHeaders,
                body = maskedBody,
                durationMs = message.DurationMs
            };

            detail.ResponsePayload = JsonSerializer.Serialize(maskedResponseObj);
        }

        if (hasError)
        {
            detail.ErrorPayload = _masker.MaskJson(JsonSerializer.Serialize(errorObj));
        }

        if (hasMetadata)
        {
            detail.Metadata = _masker.MaskJson(JsonSerializer.Serialize(metadataObj));
        }

        await detailRepo.UpsertAsync(detail, ct);
    }

    private static object? BuildRequestPayload(LogEventMessage message)
    {
        bool hasData =
            !string.IsNullOrEmpty(message.Method)
            || !string.IsNullOrEmpty(message.Path)
            || !string.IsNullOrEmpty(message.QueryString)
            || !string.IsNullOrEmpty(message.FullUrl)
            || !string.IsNullOrEmpty(message.ClientIp)
            || !string.IsNullOrEmpty(message.SourceService)
            || (message.RequestHeaders != null && message.RequestHeaders.Count > 0)
            || !string.IsNullOrEmpty(message.RequestBody);

        if (!hasData)
        {
            return null;
        }

        return new
        {
            method = message.Method,
            path = message.Path,
            queryString = message.QueryString,
            fullUrl = message.FullUrl,
            clientIp = message.ClientIp,
            sourceService = message.SourceService,
            headers = message.RequestHeaders,
            body = message.RequestBody
        };
    }

    private static object? BuildResponsePayload(LogEventMessage message)
    {
        bool hasData =
            message.StatusCode.HasValue
            || (message.ResponseHeaders != null && message.ResponseHeaders.Count > 0)
            || !string.IsNullOrEmpty(message.ResponseBody)
            || message.DurationMs.HasValue;

        if (!hasData)
        {
            return null;
        }

        return new
        {
            statusCode = message.StatusCode,
            headers = message.ResponseHeaders,
            body = message.ResponseBody,
            durationMs = message.DurationMs
        };
    }

    private static object? BuildErrorPayload(LogEventMessage message)
    {
        bool hasData =
            !string.IsNullOrEmpty(message.ErrorCode)
            || !string.IsNullOrEmpty(message.ErrorMessage)
            || !string.IsNullOrEmpty(message.Exception);

        if (!hasData)
        {
            return null;
        }

        return new
        {
            errorCode = message.ErrorCode,
            errorMessage = message.ErrorMessage,
            exception = message.Exception
        };
    }

    private static object? BuildMetadataPayload(LogEventMessage message)
    {
        bool hasAuthContext =
            message.HasAuthorization
            || !string.IsNullOrEmpty(message.AuthScheme)
            || message.IsAuthenticated
            || !string.IsNullOrEmpty(message.UserId)
            || !string.IsNullOrEmpty(message.Username)
            || !string.IsNullOrEmpty(message.UserEmail)
            || (message.Roles != null && message.Roles.Count > 0)
            || !string.IsNullOrEmpty(message.AuthResult)
            || !string.IsNullOrEmpty(message.CorrelationId);

        if (!hasAuthContext)
        {
            return null;
        }

        return new
        {
            flowType = message.FlowType,
            actionType = message.ActionType,
            status = message.Status,
            hasAuthorization = message.HasAuthorization,
            authScheme = message.AuthScheme,
            isAuthenticated = message.IsAuthenticated,
            userId = message.UserId,
            username = message.Username,
            userEmail = message.UserEmail,
            roles = message.Roles,
            authResult = message.AuthResult,
            correlationId = message.CorrelationId
        };
    }

    private static void NormalizeServiceName(LogEventMessage message)
    {
        if (!string.IsNullOrEmpty(message.ServiceName))
        {
            return;
        }

        if (message.FlowType != FlowTypes.HttpAction)
        {
            return;
        }

        var headers = message.RequestHeaders ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (TryGetHeader(headers, "X-Source-Service", out var sourceService) && !string.IsNullOrEmpty(sourceService))
        {
            message.ServiceName = sourceService;
            return;
        }

        if (TryGetHeader(headers, "X-Caller-Service", out var callerService) && !string.IsNullOrEmpty(callerService))
        {
            message.ServiceName = callerService;
            return;
        }

        if (TryGetHeader(headers, "X-Forwarded-Prefix", out var forwardedPrefix) && !string.IsNullOrEmpty(forwardedPrefix))
        {
            message.ServiceName = ParseServiceNameFromPrefix(forwardedPrefix);
            return;
        }

        if (!string.IsNullOrEmpty(message.Path))
        {
            message.ServiceName = ParseServiceNameFromPath(message.Path);
            if (!string.IsNullOrEmpty(message.ServiceName))
            {
                return;
            }
        }

        if (!string.IsNullOrEmpty(message.FullUrl))
        {
            message.ServiceName = ParseServiceNameFromPath(message.FullUrl);
            return;
        }

        message.ServiceName = "unknown-service";
    }

    private static bool TryGetHeader(Dictionary<string, string> headers, string key, out string value)
    {
        if (headers.TryGetValue(key, out var headerValue))
        {
            value = headerValue;
            return true;
        }

        foreach (var kvp in headers)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kvp.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static string ParseServiceNameFromPrefix(string prefix)
    {
        var trimmed = prefix.TrimEnd('/').TrimStart('/');
        var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return "unknown-service";
        }

        var lastSegment = segments[^1];
        return lastSegment.ToLowerInvariant() switch
        {
            "partner" => "partner-service",
            "admin" => "admin-service",
            "user" => "user-service",
            "payment" => "payment-service",
            _ => lastSegment.ToLowerInvariant() + "-service"
        };
    }

    private static string ParseServiceNameFromPath(string pathOrUrl)
    {
        if (string.IsNullOrEmpty(pathOrUrl))
        {
            return string.Empty;
        }

        string path = pathOrUrl;

        // If it's a full URL (contains ://), extract just the path portion
        if (pathOrUrl.Contains("://"))
        {
            var afterScheme = pathOrUrl.Substring(pathOrUrl.IndexOf("://") + 3);
            var slashIndex = afterScheme.IndexOf('/');
            if (slashIndex < 0)
            {
                return "unknown-service";
            }
            path = afterScheme.Substring(slashIndex);
        }

        var segments = path.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            return string.Empty;
        }

        var prefixSegment = segments[0];

        if (prefixSegment.Equals("apis", StringComparison.OrdinalIgnoreCase) && segments.Length > 1)
        {
            prefixSegment = segments[1];
        }

        if (prefixSegment.Equals("partner", StringComparison.OrdinalIgnoreCase))
        {
            return "partner-service";
        }

        if (prefixSegment.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            return "admin-service";
        }

        if (prefixSegment.Equals("user", StringComparison.OrdinalIgnoreCase))
        {
            return "user-service";
        }

        if (prefixSegment.Equals("payment", StringComparison.OrdinalIgnoreCase))
        {
            return "payment-service";
        }

        if (prefixSegment.StartsWith("api", StringComparison.OrdinalIgnoreCase))
        {
            return prefixSegment.ToLowerInvariant() + "-service";
        }

        return prefixSegment.ToLowerInvariant() + "-service";
    }

    private static AutoOffsetReset ParseAutoOffsetReset(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "earliest" => AutoOffsetReset.Earliest,
            "latest" => AutoOffsetReset.Latest,
            _ => AutoOffsetReset.Earliest
        };
    }
}
