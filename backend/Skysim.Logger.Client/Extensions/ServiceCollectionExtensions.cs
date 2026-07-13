using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Skysim.Logger.Client.Http;
using Skysim.Logger.Client.Middlewares;
using Skysim.Logger.Client.Producers;
using Skysim.Logger.Contracts.Kafka;

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

        // Required so that downstream `services.AddHttpClient(...).AddHttpMessageHandler<FlowContextForwardingHandler>()`
        // calls can resolve the handler from DI. Transient lifetime matches the
        // default for DelegatingHandler / HttpMessageHandler in ASP.NET Core.
        services.AddTransient<FlowContextForwardingHandler>();

        services.Configure<LoggerMiddlewareOptions>(opts =>
        {
            var section = configuration.GetSection(LoggerMiddlewareOptions.SectionName);
            section.Bind(opts);
        });

        services.Configure<KafkaConsumerOptions>(opts =>
        {
            var kafkaSection = configuration.GetSection("Kafka");
            kafkaSection.Bind(opts);

            opts.Producer.BootstrapServers =
                kafkaSection.GetSection("Producer")["BootstrapServers"]
                ?? kafkaSection["BootstrapServers"]
                ?? opts.Producer.BootstrapServers;

            opts.Retry.MaxAttempts =
                kafkaSection.GetValue<int?>("Retry:MaxAttempts")
                ?? kafkaSection.GetValue<int?>("Producer:RetryMaxAttempts")
                ?? 3;

            opts.Retry.InitialDelayMs =
                kafkaSection.GetValue<int?>("Retry:InitialDelayMs")
                ?? kafkaSection.GetValue<int?>("Producer:RetryBaseDelayMs")
                ?? 100;
        });

        services.Configure<LoggerOptions>(opts =>
        {
            var section = configuration.GetSection(LoggerMiddlewareOptions.SectionName);
            section.Bind(opts);
            opts.ServiceName = section["ServiceName"] ?? "unknown-service";
        });

        services.AddSingleton<IKafkaLogProducer, KafkaLogProducer>();

        return services;
    }
}
