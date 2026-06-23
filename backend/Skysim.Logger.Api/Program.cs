using FluentValidation;
using Skysim.Logger.Api.Common;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Infrastructure.Kafka;
using Skysim.Logger.Api.Infrastructure.Persistence;
using Skysim.Logger.Api.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure KafkaConsumerOptions
builder.Services.Configure<KafkaConsumerOptions>(
    builder.Configuration.GetSection("Kafka"));

// Register DbContext
builder.Services.AddDbContext<LoggerDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            // Use the custom NoRetryExecutionStrategy so that EF Core does not
            // wrap every query in its own retry loop.  The Kafka consumer
            // already uses Polly (ResiliencePipeline) to retry the entire
            // persist operation as an atomic unit; two retry layers cause
            // connection-state corruption (NpgsqlConnection disposed) on
            // transient PostgreSQL failures.
            npgsqlOptions.ExecutionStrategy(_ => new NoRetryExecutionStrategy());
        });
});

// Register repositories
builder.Services.AddScoped<ILogFlowRepository, LogFlowRepository>();
builder.Services.AddScoped<ILogActionRepository, LogActionRepository>();
builder.Services.AddScoped<ILogActionDetailRepository, LogActionDetailRepository>();

// Register FluentValidation validators
builder.Services.AddScoped<IValidator<LogEventMessage>, LogEventMessageValidator>();

// Register SensitiveDataMasker as singleton
builder.Services.AddSingleton<SensitiveDataMasker>();

// Register SensitiveFields
builder.Services.AddSingleton(SensitiveFields.Instance);

// Register DLQ Publisher
builder.Services.AddSingleton<IDlqPublisher, DlqPublisher>();

// Register Kafka Consumer Background Service
builder.Services.AddHostedService<KafkaLogConsumerService>();

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .WithName("HealthCheck")
   .WithTags("Health");

app.Run();
