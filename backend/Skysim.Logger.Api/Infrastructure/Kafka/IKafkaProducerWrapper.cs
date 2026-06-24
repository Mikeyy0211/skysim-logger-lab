namespace Skysim.Logger.Api.Infrastructure.Kafka;

public sealed class KafkaDeliveryResult
{
    public string Topic { get; init; } = string.Empty;
    public int PartitionValue { get; init; }
    public long OffsetValue { get; init; }
}

public interface IKafkaProducerWrapper
{
    Task<KafkaDeliveryResult> ProduceAsync(
        string topic,
        Confluent.Kafka.Message<string, byte[]> message,
        CancellationToken cancellationToken);
}
