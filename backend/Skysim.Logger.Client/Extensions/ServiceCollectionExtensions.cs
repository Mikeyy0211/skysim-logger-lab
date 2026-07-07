using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Skysim.Logger.Client.Http;
using Skysim.Logger.Client.Middlewares;
using Skysim.Logger.Client.Producers;

namespace Skysim.Logger.Client.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Skysim Logger infrastructure: LoggerMiddleware options, KafkaLogProducer,
    /// and IHttpContextAccessor (required by FlowContextForwardingHandler).
    ///
    /// To propagate X-Flow-Id / X-Correlation-Id through downstream HTTP calls:
    /// <code>
    /// services.AddHttpClient("MyService")
    ///     .AddHttpMessageHandler&lt;FlowContextForwardingHandler&gt;();
    /// </code>
    /// </summary>
    public static IServiceCollection AddSkysimLogger(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        services.Configure<LoggerMiddlewareOptions>(opts =>
        {
            var section = configuration.GetSection(LoggerMiddlewareOptions.SectionName);
            section.Bind(opts);
        });

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
