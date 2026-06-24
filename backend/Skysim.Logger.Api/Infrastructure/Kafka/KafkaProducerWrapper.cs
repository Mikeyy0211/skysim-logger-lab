using Confluent.Kafka;

namespace Skysim.Logger.Api.Infrastructure.Kafka;

public sealed class KafkaProducerWrapper : IKafkaProducerWrapper
{
    private readonly IProducer<string, byte[]> _producer;

    public KafkaProducerWrapper(IProducer<string, byte[]> producer)
    {
        _producer = producer;
    }

    public async Task<KafkaDeliveryResult> ProduceAsync(
        string topic,
        Message<string, byte[]> message,
        CancellationToken cancellationToken)
    {
        var result = await _producer.ProduceAsync(topic, message, cancellationToken);
        return new KafkaDeliveryResult
        {
            Topic = result.Topic,
            PartitionValue = result.Partition.Value,
            OffsetValue = result.Offset.Value
        };
    }
}
