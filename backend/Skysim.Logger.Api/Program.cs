using System.Reflection;
using System.Text.Json;
using FluentValidation;
using Skysim.Logger.Api.Common;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Contracts.DTOs.Queries;
using Skysim.Logger.Api.Infrastructure.Kafka;
using Skysim.Logger.Api.Infrastructure.Persistence;
using Skysim.Logger.Api.Infrastructure.Persistence.Repositories;
using Skysim.Logger.Api.Services.Query;
using Skysim.Logger.Api.Validators;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi.Models;

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
            npgsqlOptions.ExecutionStrategy(_ => new NoRetryExecutionStrategy());
        });
});

// Register DbContextFactory for read-only query services
builder.Services.AddDbContextFactory<LoggerDbContext>(
    options =>
    {
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            npgsqlOptions =>
            {
                npgsqlOptions.ExecutionStrategy(_ => new NoRetryExecutionStrategy());
            });
    },
    ServiceLifetime.Scoped);

// Register repositories
builder.Services.AddScoped<ILogFlowRepository, LogFlowRepository>();
builder.Services.AddScoped<ILogActionRepository, LogActionRepository>();
builder.Services.AddScoped<ILogActionDetailRepository, LogActionDetailRepository>();

// Register FluentValidation validators
builder.Services.AddScoped<IValidator<LogEventMessage>, LogEventMessageValidator>();
builder.Services.AddScoped<IValidator<LogFlowListQuery>, LogFlowListQueryValidator>();
builder.Services.AddScoped<IValidator<LogActionListQuery>, LogActionListQueryValidator>();

// Register SensitiveDataMasker as singleton
builder.Services.AddSingleton<SensitiveDataMasker>();

// Register SensitiveFields
builder.Services.AddSingleton(SensitiveFields.Instance);

// Register DLQ Publisher
builder.Services.AddSingleton<IDlqPublisher, DlqPublisher>();

// Register Kafka Consumer Background Service
builder.Services.AddHostedService<KafkaLogConsumerService>();

// Register query services
builder.Services.AddScoped<ILogFlowQueryService, LogFlowQueryService>();
builder.Services.AddScoped<ILogActionQueryService, LogActionQueryService>();

// Add controllers with camelCase JSON naming
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// Add Swagger/OpenAPI
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
   .WithName("HealthCheck")
   .WithTags("Health");

app.MapControllers();

app.Run();
