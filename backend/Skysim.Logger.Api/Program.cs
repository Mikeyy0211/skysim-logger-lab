using System.Reflection;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Skysim.Logger.Api.Common;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Contracts.DTOs.Queries;
using Skysim.Logger.Api.Infrastructure.Kafka;
using Skysim.Logger.Api.Infrastructure.Persistence;
using Skysim.Logger.Api.Infrastructure.Persistence.Repositories;
using Skysim.Logger.Api.Middlewares;
using Skysim.Logger.Api.Services.Query;
using Skysim.Logger.Api.Validators;

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

builder.Services.AddScoped<IValidator<LogEventMessage>, LogEventMessageValidator>();
builder.Services.AddScoped<IValidator<LogFlowListQuery>, LogFlowListQueryValidator>();
builder.Services.AddScoped<IValidator<LogActionListQuery>, LogActionListQueryValidator>();

builder.Services.AddSingleton<SensitiveDataMasker>();
builder.Services.AddSingleton(SensitiveFields.Instance);

builder.Services.AddSingleton<IKafkaProducerFactory, KafkaProducerFactory>();
builder.Services.AddSingleton<IDlqPublisher, DlqPublisher>();

builder.Services.AddSingleton<IKafkaLogProducerOptions, KafkaLogProducerOptions>();
builder.Services.AddSingleton<IKafkaLogProducer, KafkaLogProducer>();

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

app.UseMiddleware<RequestBodyBufferingMiddleware>();
app.UseMiddleware<LoggerMiddleware>();

app.MapControllers();

app.Run();
