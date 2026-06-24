using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Skysim.Logger.Api.Common;
using Skysim.Logger.Api.Contracts.DTOs;

namespace Skysim.Logger.Api.Infrastructure.Kafka;

public class KafkaLogProducer : IKafkaLogProducer, IDisposable
{
    private const string Topic = "skysim.action.logs";
    private const int FlushTimeoutSeconds = 5;

    private readonly IKafkaProducerWrapper _wrapper;
    private readonly IProducer<string, byte[]> _producer;
    private readonly ILogger<KafkaLogProducer> _logger;
    private readonly ResiliencePipeline _retryPipeline;
    private readonly string _serviceName;

    public KafkaLogProducer(
        IKafkaLogProducerOptions options,
        ILogger<KafkaLogProducer> logger,
        IKafkaProducerWrapper? wrapper = null)
    {
        _producer = BuildProducer(options);
        _wrapper = wrapper ?? new KafkaProducerWrapper(_producer);
        _logger = logger;
        _serviceName = options.ServiceName;
        _retryPipeline = BuildRetryPipeline(options.Retry);
    }

    public async Task PublishAsync(LogEventMessage message, CancellationToken cancellationToken = default)
    {
        message.ServiceName = _serviceName;

        byte[] payload;
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(message, LogEventMessage.JsonOptions);
            payload = Encoding.UTF8.GetBytes(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize LogEventMessage. EventId={EventId}", message.EventId);
            return;
        }

        var key = !string.IsNullOrEmpty(message.FlowId)
            ? message.FlowId
            : message.EventId.ToString();

        var kafkaMessage = new Message<string, byte[]>
        {
            Key = key,
            Value = payload
        };

        try
        {
            await _retryPipeline.ExecuteAsync(
                async ct =>
                {
                    var result = await _wrapper.ProduceAsync(Topic, kafkaMessage, ct);
                    _logger.LogDebug(
                        "LogEventMessage delivered. Topic={Topic}, Partition={Partition}, Offset={Offset}, EventId={EventId}",
                        result.Topic,
                        result.PartitionValue,
                        result.OffsetValue,
                        message.EventId);
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to publish LogEventMessage after all retries. EventId={EventId}, FlowId={FlowId}, Topic={Topic}",
                message.EventId,
                message.FlowId,
                Topic);
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(FlushTimeoutSeconds));
        _producer.Dispose();
    }

    private static IProducer<string, byte[]> BuildProducer(IKafkaLogProducerOptions options)
    {
        var config = new ProducerConfig
        {
            BootstrapServers = options.BootstrapServers,
            Acks = ParseAcks(options.Acks)
        };

        return new ProducerBuilder<string, byte[]>(config).Build();
    }

    private static ResiliencePipeline BuildRetryPipeline(RetryOptions retryOptions)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = retryOptions.MaxAttempts,
                DelayGenerator = args =>
                {
                    var delay = CalculateDelay(args.AttemptNumber + 1, retryOptions);
                    return new ValueTask<TimeSpan?>(delay);
                },
                ShouldHandle = new PredicateBuilder()
                    .Handle<ProduceException<string, byte[]>>()
            })
            .Build();
    }

    private static TimeSpan CalculateDelay(int attempt, RetryOptions options)
    {
        var delay = options.InitialDelayMs * Math.Pow(options.BackoffMultiplier, attempt - 1);
        return TimeSpan.FromMilliseconds(Math.Min(delay, options.MaxDelayMs));
    }

    private static Acks ParseAcks(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "all" or "-1" => Acks.All,
            "none" or "0" => Acks.None,
            "leader" or "1" => Acks.Leader,
            _ => Acks.All
        };
    }
}
