namespace Skysim.Logger.Api.Infrastructure.Kafka;

public interface IConsumeResultWrapper
{
    string Topic { get; }
    int Partition { get; }
    long Offset { get; }
    byte[] Value { get; }
    byte[] Key { get; }
    DateTime Timestamp { get; }
}

public class ConsumeResultWrapper : IConsumeResultWrapper
{
    public string Topic { get; set; } = string.Empty;
    public int Partition { get; set; }
    public long Offset { get; set; }
    public byte[] Value { get; set; } = Array.Empty<byte>();
    public byte[] Key { get; set; } = Array.Empty<byte>();
    public DateTime Timestamp { get; set; }

    public static ConsumeResultWrapper FromConsumeResult(Confluent.Kafka.ConsumeResult<byte[], byte[]> result)
    {
        return new ConsumeResultWrapper
        {
            Topic = result.Topic,
            Partition = result.Partition.Value,
            Offset = result.Offset.Value,
            Value = result.Message.Value,
            Key = result.Message.Key,
            Timestamp = result.Message.Timestamp.UtcDateTime
        };
    }
}
