using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Skysim.Logger.Api.Common;
using Skysim.Logger.Api.Contracts.DTOs;

namespace Skysim.Logger.Api.Infrastructure.Kafka;

public class KafkaLogProducer : IKafkaLogProducer, IDisposable
{
    private const string Topic = "skysim.action.logs";
    private const int FlushTimeoutSeconds = 5;

    private readonly IKafkaLogProducerOptions _options;
    private readonly ILogger<KafkaLogProducer> _logger;
    private readonly RetryOptions _retryOptions;

    private IProducer<string, byte[]>? _producer;

    public KafkaLogProducer(
        IKafkaLogProducerOptions options,
        ILogger<KafkaLogProducer> logger)
    {
        _options = options;
        _logger = logger;
        _retryOptions = options.Retry;
    }

    internal KafkaLogProducer(
        IKafkaLogProducerOptions options,
        ILogger<KafkaLogProducer> logger,
        IProducer<string, byte[]> producer)
    {
        _options = options;
        _logger = logger;
        _retryOptions = options.Retry;
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
        message.ServiceName = _options.ServiceName;

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

        while (!success && attempt < _retryOptions.MaxAttempts)
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
            catch (ProduceException<string, byte[]> ex) when (attempt < _retryOptions.MaxAttempts)
            {
                var delay = KafkaCommon.CalculateDelay(attempt, _retryOptions);
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
            BootstrapServers = _options.BootstrapServers,
            Acks = KafkaCommon.ParseAcks(_options.Acks)
        };

        return new ProducerBuilder<string, byte[]>(config).Build();
    }
}
