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
using Skysim.Logger.Api.Common;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Domain.Entities;
using Skysim.Logger.Api.Domain.Enums;
using Skysim.Logger.Api.Infrastructure.Persistence;
using Skysim.Logger.Api.Infrastructure.Persistence.Exceptions;
using Skysim.Logger.Api.Infrastructure.Persistence.Repositories;
using Status = Skysim.Logger.Api.Domain.Enums.Status;

namespace Skysim.Logger.Api.Infrastructure.Kafka;

public class KafkaLogConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaConsumerOptions _options;
    private readonly IValidator<LogEventMessage> _validator;
    private readonly IDlqPublisher _dlqPublisher;
    private readonly SensitiveDataMasker _masker;
    private readonly ResiliencePipeline _dbRetryPolicy;
    private readonly ResiliencePipeline _brokerRetryPolicy;
    private readonly ILogger<KafkaLogConsumerService> _logger;

    private IConsumer<byte[], byte[]>? _consumer;

    public KafkaLogConsumerService(
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaConsumerOptions> options,
        IValidator<LogEventMessage> validator,
        IDlqPublisher dlqPublisher,
        SensitiveDataMasker masker,
        ILogger<KafkaLogConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _validator = validator;
        _dlqPublisher = dlqPublisher;
        _masker = masker;
        _logger = logger;

        _dbRetryPolicy = RetryPolicyFactory.CreateDbRetryPolicy(options);
        _brokerRetryPolicy = RetryPolicyFactory.CreateBrokerRetryPolicy(options);
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
            EnableAutoCommit = _options.Consumer.EnableAutoCommit,
            MaxPollIntervalMs = _options.Consumer.MaxPollIntervalMs,
            SessionTimeoutMs = _options.Consumer.SessionTimeoutMs
        };

        _consumer = new ConsumerBuilder<byte[], byte[]>(consumerConfig).Build();
        _consumer.Subscribe(_options.Consumer.Topic);

        _logger.LogInformation("Subscribed to topic: {Topic}", _options.Consumer.Topic);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(stoppingToken);

                    if (consumeResult == null)
                    {
                        continue;
                    }

                    await ProcessMessageAsync(consumeResult, stoppingToken);

                    _consumer.Commit(consumeResult);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume error. ErrorCode={ErrorCode}", ex.Error.Code);
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

        // Step 1-2: Deserialize
        try
        {
            message = DeserializeMessage(rawPayload);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to deserialize message. RawPayload={RawPayload}, Partition={Partition}, Offset={Offset}",
                Encoding.UTF8.GetString(rawPayload),
                result.Partition.Value,
                result.Offset.Value);

            await HandleFailureAsync(result, "DESERIALIZATION_FAILED: " + ex.Message, 1, ct);
            return;
        }

        if (message == null)
        {
            await HandleFailureAsync(result, "DESERIALIZATION_FAILED: null message", 1, ct);
            return;
        }

        // Step 3: Validate
        var validationResult = await _validator.ValidateAsync(message, ct);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            _logger.LogWarning(
                "Message validation failed. EventId={EventId}, FlowId={FlowId}, Errors={Errors}",
                message.EventId,
                message.FlowId,
                errors);

            await HandleFailureAsync(result, "VALIDATION_FAILED: " + errors, 1, ct);
            return;
        }

        // Step 4-7: Try to persist with retry
        var attempt = 0;
        var success = await TryPersistWithRetryAsync(message, ct, () => attempt++);

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
            await HandleFailureAsync(result, "PERSISTENCE_FAILED: Max retries exceeded", attempt, ct);
        }
    }

    private async Task<bool> TryPersistWithRetryAsync(LogEventMessage message, CancellationToken ct, Action onAttempt)
    {
        return await _dbRetryPolicy.ExecuteAsync(async token =>
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

        using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            // Step 5: Upsert flow
            var flow = await flowRepo.UpsertAsync(message, ct);

            // Step 6: Insert action (may throw DuplicateEventException)
            var action = await BuildLogActionAsync(message, flow.Id, ct);
            try
            {
                action = await actionRepo.InsertAsync(action, ct);
            }
            catch (DuplicateEventException dex)
            {
                _logger.LogInformation(
                    "Duplicate event detected (idempotent skip). EventId={EventId}",
                    dex.EventId);

                await transaction.RollbackAsync(ct);
                return true; // Return true to commit offset
            }

            // Step 7: Insert/upsert details
            await TryUpsertDetailAsync(detailRepo, action.Id, message, ct);

            // Commit transaction
            await transaction.CommitAsync(ct);
            return true;
        }
        catch (DuplicateEventException dex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogInformation(
                "Duplicate event detected during persist (idempotent skip). EventId={EventId}",
                dex.EventId);
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(
                ex,
                "Failed to persist message. EventId={EventId}, FlowId={FlowId}",
                message.EventId,
                message.FlowId);
            throw;
        }
    }

    private Task<LogAction> BuildLogActionAsync(LogEventMessage message, Guid flowId, CancellationToken ct)
    {
        var durationMs = message.Duration;
        if (!durationMs.HasValue && message.RequestTime.HasValue && message.ResponseTime.HasValue)
        {
            durationMs = (int)(message.ResponseTime.Value - message.RequestTime.Value).TotalMilliseconds;
        }

        var action = new LogAction
        {
            Id = Guid.NewGuid(),
            EventId = message.EventId,
            FlowId = message.FlowId,
            StepOrder = 0, // Will be set by repository
            ServiceName = message.ServiceName,
            ActionType = message.ActionType.ToString(),
            Status = message.Status.ToString(),
            Message = message.Message,
            ErrorCode = message.ErrorCode,
            ErrorMessage = message.ErrorMessage,
            RequestTime = message.RequestTime,
            ResponseTime = message.ResponseTime,
            DurationMs = durationMs,
            CorrelationId = message.CorrelationId,
            CreatedAt = message.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };

        return Task.FromResult(action);
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
            Id = Guid.NewGuid(),
            ActionId = actionId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (message.RequestData.HasValue)
        {
            detail.RequestPayload = message.RequestData.Value.GetRawText();
        }

        if (message.ResponseData.HasValue)
        {
            detail.ResponsePayload = message.ResponseData.Value.GetRawText();
        }

        if (!string.IsNullOrEmpty(message.ErrorCode) || !string.IsNullOrEmpty(message.Exception))
        {
            var errorObj = new
            {
                errorCode = message.ErrorCode,
                errorMessage = message.ErrorMessage,
                exception = message.Exception
            };
            detail.ErrorPayload = JsonSerializer.Serialize(errorObj);
        }

        await detailRepo.UpsertAsync(detail, ct);
    }

    private async Task HandleFailureAsync(ConsumeResult<byte[], byte[]> result, string reason, int attempt, CancellationToken ct)
    {
        if (attempt < _options.Retry.MaxAttempts)
        {
            var delay = CalculateDelay(attempt, _options.Retry);
            _logger.LogWarning(
                "Retrying after failure. Reason={Reason}, Attempt={Attempt}, DelayMs={DelayMs}",
                reason,
                attempt,
                delay.TotalMilliseconds);

            await Task.Delay(delay, ct);
        }
        else
        {
            _logger.LogError(
                "Max retry attempts reached, publishing to DLQ. Reason={Reason}, MaxAttempts={MaxAttempts}",
                reason,
                _options.Retry.MaxAttempts);

            try
            {
                await _brokerRetryPolicy.ExecuteAsync(async token =>
                {
                    await _dlqPublisher.PublishAsync(result, reason, attempt, token);
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to publish to DLQ after retries. Reason={Reason}, Attempt={Attempt}",
                    reason,
                    attempt);
            }
        }
    }

    private static TimeSpan CalculateDelay(int attempt, RetryOptions options)
    {
        var delay = options.InitialDelayMs * Math.Pow(options.BackoffMultiplier, attempt - 1);
        return TimeSpan.FromMilliseconds(Math.Min(delay, options.MaxDelayMs));
    }

    private static LogEventMessage? DeserializeMessage(byte[] payload)
    {
        if (payload == null || payload.Length == 0)
        {
            return null;
        }

        var jsonString = Encoding.UTF8.GetString(payload);
        return JsonSerializer.Deserialize<LogEventMessage>(jsonString, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
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
