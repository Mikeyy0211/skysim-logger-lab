# Kafka Environment Switching Guide

This document describes how to switch the `Skysim.Logger.SampleService` between local Kafka and PM Kafka environments.

## Available Environments

| Environment | BootstrapServers | Topic | Config File |
|-------------|-----------------|-------|-------------|
| Local (default) | `localhost:9092` | `skysim.action.logs` | `appsettings.json` |
| PM | `149.28.132.56:9092` | `system-event-log` | `appsettings.PM.json` |

## Switching Between Environments

### Option 1: Using `ASPNETCORE_ENVIRONMENT` variable (Recommended)

**macOS/Linux:**
```bash
# Local Kafka
ASPNETCORE_ENVIRONMENT=Development dotnet run --project backend/Skysim.Logger.SampleService

# PM Kafka
ASPNETCORE_ENVIRONMENT=PM dotnet run --project backend/Skysim.Logger.SampleService
```

**Windows (cmd):**
```cmd
set ASPNETCORE_ENVIRONMENT=PM
dotnet run --project backend\Skysim.Logger.SampleService
```

**Windows (PowerShell):**
```powershell
$env:ASPNETCORE_ENVIRONMENT="PM"
dotnet run --project backend\Skysim.Logger.SampleService
```

### Option 2: Using `--environment` flag

```bash
dotnet run --project backend/Skysim.Logger.SampleService -- --environment PM
```

### Option 3: Visual Studio / Rider

Set the `ASPNETCORE_ENVIRONMENT` environment variable in the project's run configuration:

- Key: `ASPNETCORE_ENVIRONMENT`
- Value: `PM` (or `Development` for local)

## Configuration Details

### Local (appsettings.json)

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Producer": {
      "Topic": "skysim.action.logs"
    }
  }
}
```

### PM (appsettings.PM.json)

```json
{
  "Kafka": {
    "BootstrapServers": "149.28.132.56:9092",
    "Producer": {
      "Topic": "system-event-log"
    }
  }
}
```

## Verification Checklist

### Local Kafka Testing

1. Start the service with default environment:
   ```bash
   dotnet run --project backend/Skysim.Logger.SampleService
   ```

2. Send an HTTP request to the service (e.g., via Postman or Swagger UI at `http://localhost:5000/swagger`).

3. Verify the message appears in local Kafka:
   - Open **Kafka UI** (available at `http://localhost:8080` if using Docker Compose)
   - Navigate to the **Topics** section
   - Select `skysim.action.logs`
   - Check that a new message was published with your request data

### PM Kafka Testing

1. Start the service with PM environment:
   ```bash
   ASPNETCORE_ENVIRONMENT=PM dotnet run --project backend/Skysim.Logger.SampleService
   ```

2. Send an HTTP request to the service.

3. Verify the message appears in PM Kafka:
   - Access **Kafka UI** or tool configured for PM environment
   - Navigate to the **Topics** section
   - Select `system-event-log`
   - Check that a new message was published with your request data

## Using Extension Methods in Consuming Services

For services that want to use the Logger middleware, the extension methods simplify registration:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register all Logger services with a single call
builder.Services.AddSkysimLogger(builder.Configuration);

// ... other registrations ...

var app = builder.Build();

// Add Logger middleware with a single call
app.UseSkysimLogger();

// ... other middleware ...
```

The consuming service's `appsettings.json` should include:

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Producer": {
      "Topic": "skysim.action.logs",
      "Acks": "all",
      "RetryMaxAttempts": 3,
      "RetryBaseDelayMs": 100
    }
  },
  "Logger": {
    "ServiceName": "your-service-name"
  }
}
```

## Notes

- `appsettings.PM.json` is **NOT** added to `.gitignore`. It contains only non-sensitive configuration (BootstrapServers IP and Topic name).
- If you need to store sensitive data (e.g., credentials for PM environment), use environment variables or a secrets manager instead.
- The `KafkaLogProducer` defaults to `skysim.action.logs` if no topic is configured.
- Flow ID generation is handled internally by `LoggerMiddleware` — no separate `FlowIdSeedingMiddleware` registration is required when using `UseSkysimLogger()`.
