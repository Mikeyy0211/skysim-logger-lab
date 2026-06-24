using Microsoft.Extensions.Options;

namespace Skysim.Logger.Api.Infrastructure.Kafka;

public class KafkaLogProducerOptions : IKafkaLogProducerOptions
{
    private readonly KafkaConsumerOptions _kafkaOptions;
    private readonly string _serviceName;

    public KafkaLogProducerOptions(
        IOptions<KafkaConsumerOptions> kafkaOptions,
        IOptions<LoggerOptions> loggerOptions)
    {
        _kafkaOptions = kafkaOptions.Value;
        _serviceName = loggerOptions.Value.ServiceName;
    }

    public string BootstrapServers => _kafkaOptions.Producer.BootstrapServers;
    public string Acks => _kafkaOptions.Producer.Acks;
    public RetryOptions Retry => _kafkaOptions.Retry;
    public string ServiceName => _serviceName;
}

public class LoggerOptions
{
    public string ServiceName { get; set; } = "Skysim.Logger.Api";
}
