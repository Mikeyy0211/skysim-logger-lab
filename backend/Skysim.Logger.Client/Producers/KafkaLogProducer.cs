using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using LogEventMessage = Skysim.Logger.Contracts.Events.LogEventMessage;

namespace Skysim.Logger.Client.Producers;

public class KafkaLogProducer : IKafkaLogProducer, IDisposable
{
    private const string Topic = "skysim.action.logs";
    private const int FlushTimeoutSeconds = 5;

    private readonly string _bootstrapServers;
    private readonly Acks _acks;
    private readonly int _retryMaxAttempts;
    private readonly int _retryBaseDelayMs;
    private readonly double _backoffMultiplier = 2.0;
    private readonly int _maxDelayMs = 3200;
    private readonly string _serviceName;
    private readonly ILogger<KafkaLogProducer> _logger;

    private IProducer<string, byte[]>? _producer;

    public KafkaLogProducer(
        string bootstrapServers,
        string acks,
        int retryMaxAttempts,
        int retryBaseDelayMs,
        string serviceName,
        ILogger<KafkaLogProducer> logger)
    {
        _bootstrapServers = bootstrapServers;
        _acks = ParseAcks(acks);
        _retryMaxAttempts = retryMaxAttempts;
        _retryBaseDelayMs = retryBaseDelayMs;
        _serviceName = serviceName;
        _logger = logger;
    }

    internal KafkaLogProducer(
        string bootstrapServers,
        string acks,
        int retryMaxAttempts,
        int retryBaseDelayMs,
        string serviceName,
        ILogger<KafkaLogProducer> logger,
        IProducer<string, byte[]> producer)
        : this(bootstrapServers, acks, retryMaxAttempts, retryBaseDelayMs, serviceName, logger)
    {
        _producer = producer;
    }

    private IProducer<string, byte[]> Producer
    {
        get
        {
            _producer ??= BuildProducer();
            return _producer;
        }
    }

    public async Task PublishAsync(LogEventMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message.ServiceName))
        {
            message.ServiceName = _serviceName;
        }

        byte[] payload;
        try
        {
            payload = Encoding.UTF8.GetBytes(
                System.Text.Json.JsonSerializer.Serialize(message, LogEventMessage.JsonOptions));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize LogEventMessage. EventId={EventId}", message.EventId);
            return;
        }

        var kafkaMessage = new Message<string, byte[]>
        {
            Key = string.IsNullOrEmpty(message.FlowId) ? message.EventId.ToString() : message.FlowId,
            Value = payload
        };

        var success = false;
        var attempt = 0;

        while (!success && attempt < _retryMaxAttempts)
        {
            attempt++;
            try
            {
                var result = await Producer.ProduceAsync(Topic, kafkaMessage, cancellationToken);
                _logger.LogDebug(
                    "LogEventMessage delivered. Topic={Topic}, Partition={Partition}, "
                    + "Offset={Offset}, EventId={EventId}",
                    result.Topic,
                    result.Partition.Value,
                    result.Offset.Value,
                    message.EventId);
                success = true;
            }
            catch (ProduceException<string, byte[]> ex) when (attempt < _retryMaxAttempts)
            {
                var delay = CalculateDelay(attempt);
                _logger.LogWarning(
                    ex,
                    "Kafka produce failed. EventId={EventId}, Attempt={Attempt}, "
                    + "DelayMs={DelayMs}",
                    message.EventId,
                    attempt,
                    (int)delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Kafka produce failed after all retries. EventId={EventId}, "
                    + "FlowId={FlowId}, Topic={Topic}",
                    message.EventId,
                    message.FlowId,
                    Topic);
                return;
            }
        }

        if (!success)
        {
            _logger.LogWarning(
                "Kafka produce failed after all retries. EventId={EventId}, "
                + "FlowId={FlowId}, Topic={Topic}",
                message.EventId,
                message.FlowId,
                Topic);
        }
    }

    public void Dispose()
    {
        if (_producer != null)
        {
            _producer.Flush(TimeSpan.FromSeconds(FlushTimeoutSeconds));
            _producer.Dispose();
        }
    }

    private IProducer<string, byte[]> BuildProducer()
    {
        var config = new ProducerConfig
        {
            BootstrapServers = _bootstrapServers,
            Acks = _acks
        };

        return new ProducerBuilder<string, byte[]>(config).Build();
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

    private TimeSpan CalculateDelay(int attempt)
    {
        var delay = _retryBaseDelayMs * Math.Pow(_backoffMultiplier, attempt - 1);
        return TimeSpan.FromMilliseconds(Math.Min(delay, _maxDelayMs));
    }
}
