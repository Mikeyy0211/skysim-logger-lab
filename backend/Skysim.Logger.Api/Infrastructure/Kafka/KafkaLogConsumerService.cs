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
using Skysim.Logger.Common.Kafka;
using Skysim.Logger.Common.Masking;
using Skysim.Logger.Infrastructure.Data;
using Skysim.Logger.Infrastructure.Entities;
using Skysim.Logger.Infrastructure.Repositories;
using StatusTypes = Skysim.Logger.Contracts.Constants.StatusTypes;

namespace Skysim.Logger.Api.Infrastructure.Kafka;

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
        flow.FlowType = message.FlowType;
        flow.CheckoutType = message.CheckoutType;
        flow.Status = message.Status;
        flow.CustomerEmail = message.CustomerEmail;
        flow.CustomerPhone = message.CustomerPhone;
        flow.UserId = message.UserId;
        flow.OrderId = message.OrderId;
        flow.PaymentId = message.PaymentId;
        flow.LastActionType = message.ActionType;
        flow.LastMessage = message.Message;
        flow.StartedAt = message.CreatedAt;

        if (message.Status == StatusTypes.Success)
        {
            flow.SuccessSteps++;
            flow.CompletedAt = DateTime.UtcNow;
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
        var hasPayload = message.RequestData.HasValue
            || message.ResponseData.HasValue
            || !string.IsNullOrEmpty(message.ErrorCode)
            || !string.IsNullOrEmpty(message.Exception);

        if (!hasPayload)
        {
            return;
        }

        var detail = new LogActionDetail
        {
            ActionId = actionId
        };

        if (message.RequestData.HasValue)
        {
            detail.RequestPayload = _masker.MaskJson(message.RequestData.Value.GetRawText());
        }

        if (message.ResponseData.HasValue)
        {
            detail.ResponsePayload = _masker.MaskJson(message.ResponseData.Value.GetRawText());
        }

        if (!string.IsNullOrEmpty(message.ErrorCode) || !string.IsNullOrEmpty(message.Exception))
        {
            var errorObj = new
            {
                errorCode = message.ErrorCode,
                errorMessage = message.ErrorMessage,
                exception = message.Exception
            };

            detail.ErrorPayload = _masker.MaskJson(JsonSerializer.Serialize(errorObj));
        }

        await detailRepo.UpsertAsync(detail, ct);
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
