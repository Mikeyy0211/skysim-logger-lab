using Skysim.Logger.Common.Kafka;

namespace Skysim.Logger.Api.Infrastructure.Kafka;

public class KafkaConsumerOptions
{
    public ConsumerOptions Consumer { get; set; } = new();
    public ProducerOptions Producer { get; set; } = new();
    public RetryOptions Retry { get; set; } = new();
    public string DlqTopic { get; set; } = "skysim.action.logs.dlq";
}

public class ConsumerOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string Topic { get; set; } = "skysim.action.logs";
    public string ConsumerGroup { get; set; } = "skysim-logger-consumer";
    public string AutoOffsetReset { get; set; } = "earliest";
    public bool EnableAutoCommit { get; set; } = false;
    public bool EnableAutoCommitStore { get; set; } = false;
    public int MaxPollIntervalMs { get; set; } = 600000;
    public int SessionTimeoutMs { get; set; } = 45000;
}

public class ProducerOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string Acks { get; set; } = "all";
}
