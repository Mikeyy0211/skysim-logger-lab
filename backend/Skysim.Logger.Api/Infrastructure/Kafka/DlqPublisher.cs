using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skysim.Logger.Api.Common;
using Skysim.Logger.Api.Contracts.DTOs;

namespace Skysim.Logger.Api.Infrastructure.Kafka;

public class DlqPublisher : IDlqPublisher, IDisposable
{
    private readonly IProducer<byte[], byte[]> _producer;
    private readonly KafkaConsumerOptions _options;
    private readonly SensitiveDataMasker _masker;
    private readonly ILogger<DlqPublisher> _logger;

    public DlqPublisher(
        IOptions<KafkaConsumerOptions> options,
        SensitiveDataMasker masker,
        ILogger<DlqPublisher> logger)
    {
        _options = options.Value;
        _masker = masker;
        _logger = logger;

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _options.Producer.BootstrapServers,
            Acks = ParseAcks(_options.Producer.Acks)
        };

        _producer = new ProducerBuilder<byte[], byte[]>(producerConfig).Build();
    }

    public async Task PublishAsync(
        ConsumeResult<byte[], byte[]> originalResult,
        string failureReason,
        int attempt,
        CancellationToken cancellationToken = default)
    {
        var originalKey = originalResult.Message.Key;
        var originalPayload = originalResult.Message.Value;

        // Parse and mask the payload
        var maskedPayload = MaskPayload(originalPayload);

        var headers = new Headers
        {
            { "failure_reason", Encoding.UTF8.GetBytes(failureReason) },
            { "failed_at", Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O")) },
            { "consumer_attempt", Encoding.UTF8.GetBytes(attempt.ToString()) }
        };

        // Preserve original topic/partition/offset info
        headers.Add("original_topic", Encoding.UTF8.GetBytes(originalResult.Topic));
        headers.Add("original_partition", Encoding.UTF8.GetBytes(originalResult.Partition.Value.ToString()));
        headers.Add("original_offset", Encoding.UTF8.GetBytes(originalResult.Offset.Value.ToString()));

        var message = new Message<byte[], byte[]>
        {
            Key = originalKey,
            Value = maskedPayload,
            Headers = headers
        };

        try
        {
            var result = await _producer.ProduceAsync(_options.DlqTopic, message, cancellationToken);

            _logger.LogWarning(
                "Message published to DLQ. Topic={DlqTopic}, Partition={Partition}, Offset={Offset}, " +
                "FailureReason={FailureReason}, Attempt={Attempt}",
                _options.DlqTopic,
                result.Partition.Value,
                result.Offset.Value,
                failureReason,
                attempt);
        }
        catch (ProduceException<byte[], byte[]> ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish message to DLQ. Topic={DlqTopic}, FailureReason={FailureReason}, Attempt={Attempt}",
                _options.DlqTopic,
                failureReason,
                attempt);
            throw;
        }
    }

    private byte[] MaskPayload(byte[] payload)
    {
        if (payload == null || payload.Length == 0)
        {
            return Array.Empty<byte>();
        }

        try
        {
            var jsonString = Encoding.UTF8.GetString(payload);
            var maskedJson = _masker.MaskJson(jsonString);
            return Encoding.UTF8.GetBytes(maskedJson);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to mask payload, publishing raw payload to DLQ");
            return payload;
        }
    }

    public void Dispose()
    {
        _producer?.Flush(TimeSpan.FromSeconds(5));
        _producer?.Dispose();
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
