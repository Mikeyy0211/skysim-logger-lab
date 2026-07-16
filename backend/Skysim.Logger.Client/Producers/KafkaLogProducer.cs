using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skysim.Logger.Contracts.Events;
using Skysim.Logger.Contracts.Kafka;

namespace Skysim.Logger.Client.Producers;

public class KafkaLogProducer : IKafkaLogProducer, IDisposable
{
    private const string DefaultTopic = "skysim.action.logs";
    private const int FlushTimeoutSeconds = 5;

    private readonly string _bootstrapServers;
    private readonly string _topic;
    private readonly Acks _acks;
    private readonly int _retryMaxAttempts;
    private readonly int _retryBaseDelayMs;
    private readonly double _backoffMultiplier;
    private readonly int _maxDelayMs;
    private readonly string _serviceName;
    private readonly ILogger<KafkaLogProducer> _logger;

    private IProducer<string, byte[]>? _producer;

    public KafkaLogProducer(
        IOptions<KafkaConsumerOptions> kafkaOptions,
        IOptions<LoggerOptions> loggerOptions,
        ILogger<KafkaLogProducer> logger)
    {
        ArgumentNullException.ThrowIfNull(kafkaOptions);
        ArgumentNullException.ThrowIfNull(loggerOptions);
        ArgumentNullException.ThrowIfNull(logger);

        var kafka = kafkaOptions.Value;
        var loggerConfig = loggerOptions.Value;

        _bootstrapServers = kafka.Producer.BootstrapServers;
        _topic = string.IsNullOrWhiteSpace(kafka.Producer.Topic) ? DefaultTopic : kafka.Producer.Topic;
        _acks = ParseAcks(kafka.Producer.Acks);
        _retryMaxAttempts = kafka.Retry.MaxAttempts;
        _retryBaseDelayMs = kafka.Retry.InitialDelayMs;
        _backoffMultiplier = kafka.Retry.BackoffMultiplier;
        _maxDelayMs = kafka.Retry.MaxDelayMs;
        _serviceName = loggerConfig.ServiceName;
        _logger = logger;
    }

    internal KafkaLogProducer(
        string bootstrapServers,
        string topic,
        string acks,
        int retryMaxAttempts,
        int retryBaseDelayMs,
        string serviceName,
        ILogger<KafkaLogProducer> logger,
        IProducer<string, byte[]> producer)
    {
        _bootstrapServers = bootstrapServers;
        _topic = string.IsNullOrWhiteSpace(topic) ? DefaultTopic : topic;
        _acks = ParseAcks(acks);
        _retryMaxAttempts = retryMaxAttempts;
        _retryBaseDelayMs = retryBaseDelayMs;
        _backoffMultiplier = 2.0;
        _maxDelayMs = 3200;
        _serviceName = serviceName;
        _logger = logger;
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
        if (message is null)
        {
            return;
        }

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

        await ProduceAsync(kafkaMessage, message.EventId.ToString(), message.FlowId, cancellationToken);
    }

    public async Task PublishAsync(object payload, CancellationToken cancellationToken = default)
    {
        if (payload is null)
        {
            return;
        }

        byte[] jsonBytes;
        try
        {
            jsonBytes = Encoding.UTF8.GetBytes(
                System.Text.Json.JsonSerializer.Serialize(payload));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize payload to JSON");
            return;
        }

        string? flowId = null;
        string? eventId = null;

        try
        {
            using var doc = JsonDocument.Parse(jsonBytes);
            if (doc.RootElement.TryGetProperty("flowId", out var flowIdElement))
                flowId = flowIdElement.ValueKind == JsonValueKind.String
                    ? flowIdElement.GetString()
                    : flowIdElement.ToString();

            if (doc.RootElement.TryGetProperty("eventId", out var eventIdElement))
                eventId = eventIdElement.ValueKind == JsonValueKind.String
                    ? eventIdElement.GetString()
                    : eventIdElement.ToString();
        }
        catch (JsonException)
        {
            // If we can't parse the JSON for key extraction, proceed without keys
        }

        var key = string.IsNullOrEmpty(flowId) ? eventId ?? Guid.NewGuid().ToString() : flowId;

        var kafkaMessage = new Message<string, byte[]>
        {
            Key = key,
            Value = jsonBytes
        };

        await ProduceAsync(kafkaMessage, eventId, flowId, cancellationToken);
    }

    public void Dispose()
    {
        if (_producer != null)
        {
            _producer.Flush(TimeSpan.FromSeconds(FlushTimeoutSeconds));
            _producer.Dispose();
        }
    }

    private async Task ProduceAsync(
        Message<string, byte[]> kafkaMessage,
        string? eventId,
        string? flowId,
        CancellationToken cancellationToken)
    {
        var success = false;
        var attempt = 0;

        while (!success && attempt < _retryMaxAttempts)
        {
            attempt++;
            try
            {
                var result = await Producer.ProduceAsync(_topic, kafkaMessage, cancellationToken);
                _logger.LogDebug(
                    "LogEventMessage delivered. Topic={Topic}, Partition={Partition}, "
                    + "Offset={Offset}, EventId={EventId}",
                    result.Topic,
                    result.Partition.Value,
                    result.Offset.Value,
                    eventId ?? "unknown");
                success = true;
            }
            catch (ProduceException<string, byte[]> ex) when (attempt < _retryMaxAttempts)
            {
                var delay = CalculateDelay(attempt);
                _logger.LogWarning(
                    ex,
                    "Kafka produce failed. EventId={EventId}, Attempt={Attempt}, "
                    + "DelayMs={DelayMs}",
                    eventId ?? "unknown",
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
                    eventId ?? "unknown",
                    flowId ?? "unknown",
                    _topic);
                return;
            }
        }

        if (!success)
        {
            _logger.LogWarning(
                "Kafka produce failed after all retries. EventId={EventId}, "
                + "FlowId={FlowId}, Topic={Topic}",
                eventId ?? "unknown",
                flowId ?? "unknown",
                _topic);
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
