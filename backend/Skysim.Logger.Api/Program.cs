using System.Reflection;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Skysim.Logger.Api.Consumers;
using Skysim.Logger.Api.Kafka;
using Skysim.Logger.Api.Services.Query;
using Skysim.Logger.Api.Validators;
using Skysim.Logger.Client.Middlewares;
using Skysim.Logger.Infrastructure.Data;
using Skysim.Logger.Infrastructure.Repositories;
using LogEventMessage = Skysim.Logger.Contracts.Events.LogEventMessage;
using LoggerOptions = Skysim.Logger.Api.Kafka.LoggerOptions;
using LogFlowListQuery = Skysim.Logger.Api.Contracts.Queries.LogFlowListQuery;
using LogActionListQuery = Skysim.Logger.Api.Contracts.Queries.LogActionListQuery;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<KafkaConsumerOptions>(
    builder.Configuration.GetSection("Kafka"));

builder.Services.Configure<LoggerOptions>(
    builder.Configuration.GetSection("Logger"));

builder.Services.AddDbContextFactory<LoggerDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.ExecutionStrategy(_ => new NoRetryExecutionStrategy());
        });
});

builder.Services.AddScoped<ILogFlowRepository, LogFlowRepository>();
builder.Services.AddScoped<ILogActionRepository, LogActionRepository>();
builder.Services.AddScoped<ILogActionDetailRepository, LogActionDetailRepository>();

builder.Services.AddScoped<IValidator<LogEventMessage>, Skysim.Logger.Api.Validators.LogEventMessageValidator>();
builder.Services.AddScoped<IValidator<LogFlowListQuery>, LogFlowListQueryValidator>();
builder.Services.AddScoped<IValidator<LogActionListQuery>, LogActionListQueryValidator>();

builder.Services.AddSingleton<Skysim.Logger.Client.Masking.ISensitiveDataMasker, Skysim.Logger.Client.Masking.SensitiveDataMasker>();

builder.Services.AddSingleton<IKafkaProducerFactory, KafkaProducerFactory>();
builder.Services.AddSingleton<IDlqPublisher, DlqPublisher>();

builder.Services.AddSingleton<Skysim.Logger.Client.Producers.IKafkaLogProducer>(sp =>
{
    var kafkaOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<KafkaConsumerOptions>>().Value;
    var loggerOptions = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LoggerOptions>>().Value;
    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Skysim.Logger.Client.Producers.KafkaLogProducer>>();
    return new Skysim.Logger.Client.Producers.KafkaLogProducer(
        kafkaOptions.Producer.BootstrapServers,
        kafkaOptions.Producer.Acks,
        kafkaOptions.Retry.MaxAttempts,
        kafkaOptions.Retry.InitialDelayMs,
        loggerOptions.ServiceName,
        logger);
});

builder.Services.AddHostedService<KafkaLogConsumerService>();

builder.Services.AddScoped<ILogFlowQueryService, LogFlowQueryService>();
builder.Services.AddScoped<ILogActionQueryService, LogActionQueryService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Skysim Logger API",
        Version = "v1",
        Description = "Read-only Query API for the Skysim Logger module."
    });

    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .WithName("HealthCheck")
   .WithTags("Health");

app.UseMiddleware<LoggerMiddleware>();

app.MapControllers();

app.Run();
