using System.Net.Http.Headers;
using Skysim.Logger.Client.Http;
using Skysim.Logger.Client.Masking;
using Skysim.Logger.Client.Middlewares;
using Skysim.Logger.Client.Producers;
using Skysim.Logger.Contracts.Kafka;
using Skysim.Logger.SampleService.Middlewares;
using Skysim.Logger.SampleService.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

// Register SensitiveDataMasker
builder.Services.AddSingleton<ISensitiveDataMasker, SensitiveDataMasker>();

// FlowContextForwardingHandler resolves via DI when AddHttpMessageHandler<T>() is used.
// It also implements IHttpContextAccessor based fallbacks, so the explicit AddHttpContextAccessor()
// call here is required for the typed-client configured below.
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<FlowContextForwardingHandler>();

// Configure KafkaLogProducer
builder.Services.Configure<KafkaConsumerOptions>(options =>
{
    var kafkaSection = builder.Configuration.GetSection("Kafka");
    kafkaSection.Bind(options);

    options.Producer.BootstrapServers =
        kafkaSection.GetSection("Producer")["BootstrapServers"]
        ?? kafkaSection["BootstrapServers"]
        ?? options.Producer.BootstrapServers;

    options.Retry.MaxAttempts =
        kafkaSection.GetValue<int?>("Retry:MaxAttempts")
        ?? kafkaSection.GetValue<int?>("Producer:RetryMaxAttempts")
        ?? 3;

    options.Retry.InitialDelayMs =
        kafkaSection.GetValue<int?>("Retry:InitialDelayMs")
        ?? kafkaSection.GetValue<int?>("Producer:RetryBaseDelayMs")
        ?? 100;
});

// Configure LoggerMiddlewareOptions
builder.Services.Configure<LoggerMiddlewareOptions>(
    builder.Configuration.GetSection("Logger"));

builder.Services.Configure<LoggerOptions>(options =>
{
    var loggerSection = builder.Configuration.GetSection("Logger");
    loggerSection.Bind(options);
    options.ServiceName = loggerSection["ServiceName"] ?? "sample-checkout-service";
});

builder.Services.AddSingleton<IKafkaLogProducer, KafkaLogProducer>();

// Register BusinessActionLogger
builder.Services.AddScoped<IBusinessActionLogger, BusinessActionLogger>();

// HttpClient for downstream service calls, with flow-context propagation
builder.Services.AddHttpClient<PropagationHttpClient>()
    .AddHttpMessageHandler<FlowContextForwardingHandler>();

// Add controllers
builder.Services.AddControllers();

// Add Swagger for Development environment
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Sample Checkout Service API",
        Version = "v1",
        Description = "Demo checkout eSIM service for Logger integration testing."
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline

// LoggerMiddleware handles X-Flow-Id/X-Correlation-Id: reuses existing header or creates new
app.UseMiddleware<LoggerMiddleware>();

// Enable Swagger in Development environment
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Sample Checkout Service API v1");
    });
}

app.UseRouting();
app.MapControllers();

app.Run();

public partial class Program { }
