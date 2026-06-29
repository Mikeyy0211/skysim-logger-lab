using Skysim.Logger.Client.Masking;
using Skysim.Logger.Client.Middlewares;
using Skysim.Logger.Client.Producers;
using Skysim.Logger.SampleService.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Configure logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

// Register SensitiveDataMasker
builder.Services.AddSingleton<ISensitiveDataMasker, SensitiveDataMasker>();

// Register KafkaLogProducer
var kafkaSection = builder.Configuration.GetSection("Kafka");
var bootstrapServers = kafkaSection["BootstrapServers"] ?? "localhost:9092";
var producerAcks = kafkaSection.GetSection("Producer")["Acks"] ?? "all";
var retryMaxAttempts = int.Parse(kafkaSection.GetSection("Producer")["RetryMaxAttempts"] ?? "3");
var retryBaseDelayMs = int.Parse(kafkaSection.GetSection("Producer")["RetryBaseDelayMs"] ?? "100");
var serviceName = builder.Configuration["Logger:ServiceName"] ?? "sample-checkout-service";

builder.Services.AddSingleton<IKafkaLogProducer>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<KafkaLogProducer>>();
    return new KafkaLogProducer(
        bootstrapServers,
        producerAcks,
        retryMaxAttempts,
        retryBaseDelayMs,
        serviceName,
        logger);
});

// Register LoggerMiddleware
builder.Services.AddSingleton(sp =>
{
    var producer = sp.GetRequiredService<IKafkaLogProducer>();
    var masker = sp.GetRequiredService<ISensitiveDataMasker>();
    var logger = sp.GetRequiredService<ILogger<LoggerMiddleware>>();
    return new LoggerMiddleware(
        next: null!,
        producer,
        masker,
        logger);
});

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

// FlowIdSeedingMiddleware BEFORE LoggerMiddleware
app.UseFlowIdSeeding();

// LoggerMiddleware
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
