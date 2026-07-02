using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Skysim.Logger.Client.Middlewares;
using Skysim.Logger.Client.Producers;

namespace Skysim.Logger.Client.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSkysimLogger(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LoggerMiddlewareOptions>(configuration.GetSection(LoggerMiddlewareOptions.SectionName));

        var kafkaSection = configuration.GetSection("Kafka");
        var bootstrapServers = kafkaSection["BootstrapServers"] ?? "localhost:9092";
        var producerTopic = kafkaSection.GetSection("Producer")["Topic"] ?? "skysim.action.logs";
        var producerAcks = kafkaSection.GetSection("Producer")["Acks"] ?? "all";
        var retryMaxAttempts = int.Parse(kafkaSection.GetSection("Producer")["RetryMaxAttempts"] ?? "3");
        var retryBaseDelayMs = int.Parse(kafkaSection.GetSection("Producer")["RetryBaseDelayMs"] ?? "100");
        var serviceName = configuration[$"{LoggerMiddlewareOptions.SectionName}:ServiceName"] ?? "unknown-service";

        services.AddSingleton<IKafkaLogProducer>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<KafkaLogProducer>>();
            return new KafkaLogProducer(
                bootstrapServers,
                producerTopic,
                producerAcks,
                retryMaxAttempts,
                retryBaseDelayMs,
                serviceName,
                logger);
        });

        return services;
    }
}
