namespace Skysim.Logger.Api.Infrastructure.Kafka;

public interface IKafkaLogProducerOptions
{
    string BootstrapServers { get; }
    string Acks { get; }
    RetryOptions Retry { get; }
    string ServiceName { get; }
}
