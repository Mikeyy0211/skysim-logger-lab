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
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .WithName("HealthCheck")
   .WithTags("Health");

app.Run();
