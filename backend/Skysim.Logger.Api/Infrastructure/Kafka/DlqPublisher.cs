using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Common.Kafka;
using Skysim.Logger.Common.Masking;

namespace Skysim.Logger.Api.Infrastructure.Kafka;

public interface IKafkaProducerFactory
{
    IProducer<byte[], byte[]> CreateProducer();
}

public class KafkaProducerFactory : IKafkaProducerFactory
{
    private readonly KafkaConsumerOptions _options;

    public KafkaProducerFactory(IOptions<KafkaConsumerOptions> options)
    {
        _options = options.Value;
    }

    public IProducer<byte[], byte[]> CreateProducer()
    {
        var config = new ProducerConfig
        {
            BootstrapServers = _options.Producer.BootstrapServers,
            Acks = KafkaCommon.ParseAcks(_options.Producer.Acks)
        };

        return new ProducerBuilder<byte[], byte[]>(config).Build();
    }
}

public interface IDlqPublisher
{
    Task PublishAsync(
        ConsumeResult<byte[], byte[]> originalResult,
        string failureReason,
        int attempt,
        CancellationToken cancellationToken = default);
}

public class DlqPublisher : IDlqPublisher, IDisposable
{
    private readonly IKafkaProducerFactory _producerFactory;
    private readonly string _dlqTopic;
    private readonly ISensitiveDataMasker _masker;
    private readonly ILogger<DlqPublisher> _logger;
    private readonly RetryOptions _retryOptions;

    private IProducer<byte[], byte[]>? _lazyProducer;

    public DlqPublisher(
        IOptions<KafkaConsumerOptions> options,
        IKafkaProducerFactory producerFactory,
        ISensitiveDataMasker masker,
        ILogger<DlqPublisher> logger)
    {
        _producerFactory = producerFactory;
        _dlqTopic = options.Value.DlqTopic;
        _masker = masker;
        _logger = logger;
        _retryOptions = options.Value.Retry;
    }

    private IProducer<byte[], byte[]> Producer
    {
        get
        {
            _lazyProducer ??= _producerFactory.CreateProducer();
            return _lazyProducer;
        }
    }

    public async Task PublishAsync(
        ConsumeResult<byte[], byte[]> originalResult,
        string failureReason,
        int attempt,
        CancellationToken cancellationToken = default)
    {
        var originalPayload = originalResult.Message.Value;
        var maskedPayload = MaskPayload(originalPayload);

        var headers = new Headers
        {
            { "failure_reason", Encoding.UTF8.GetBytes(failureReason) },
            { "failed_at", Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O")) },
            { "consumer_attempt", Encoding.UTF8.GetBytes(attempt.ToString()) },
            { "original_topic", Encoding.UTF8.GetBytes(originalResult.Topic) },
            { "original_partition", Encoding.UTF8.GetBytes(originalResult.Partition.Value.ToString()) },
            { "original_offset", Encoding.UTF8.GetBytes(originalResult.Offset.Value.ToString()) }
        };

        var message = new Message<byte[], byte[]>
        {
            Key = originalResult.Message.Key,
            Value = maskedPayload,
            Headers = headers
        };

        var success = false;
        var currentAttempt = 0;

        while (!success && currentAttempt < _retryOptions.MaxAttempts)
        {
            currentAttempt++;
            try
            {
                var result = await Producer.ProduceAsync(_dlqTopic, message, cancellationToken);

                _logger.LogWarning(
                    "Message published to DLQ. Topic={DlqTopic}, Partition={Partition}, Offset={Offset}, "
                    + "FailureReason={FailureReason}, Attempt={Attempt}",
                    _dlqTopic,
                    result.Partition.Value,
                    result.Offset.Value,
                    failureReason,
                    currentAttempt);
                success = true;
            }
            catch (ProduceException<byte[], byte[]> ex) when (currentAttempt < _retryOptions.MaxAttempts)
            {
                var delay = KafkaCommon.CalculateDelay(currentAttempt, _retryOptions);
                _logger.LogWarning(
                    ex,
                    "DLQ publish failed. Topic={DlqTopic}, FailureReason={FailureReason}, Attempt={Attempt}, DelayMs={DelayMs}",
                    _dlqTopic,
                    failureReason,
                    currentAttempt,
                    (int)delay.TotalMilliseconds);

                await Task.Delay(delay, cancellationToken);
            }
            catch (ProduceException<byte[], byte[]> ex)
            {
                _logger.LogError(
                    ex,
                    "DLQ publish failed after all retries. Topic={DlqTopic}, FailureReason={FailureReason}, "
                    + "OriginalTopic={OriginalTopic}, OriginalOffset={OriginalOffset}",
                    _dlqTopic,
                    failureReason,
                    originalResult.Topic,
                    originalResult.Offset.Value);
                throw;
            }
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
        if (_lazyProducer != null)
        {
            _lazyProducer.Flush(TimeSpan.FromSeconds(5));
            _lazyProducer.Dispose();
        }
    }
}
