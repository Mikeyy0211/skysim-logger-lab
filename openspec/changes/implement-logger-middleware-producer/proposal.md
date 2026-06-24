## Why

The current Skysim Logger pipeline only supports consuming log events from Kafka (published by other services) and storing them in PostgreSQL. To make the Skysim.Logger.Api itself a logging publisher, we need an HTTP middleware that captures every incoming request, builds a `LogEventMessage`, and publishes it to Kafka so the existing consumer pipeline can persist it alongside all other service logs. This closes the loop: the logger API will now both consume and produce events, enabling a self-instrumented, uniform audit trail.

## What Changes

- Add a Kafka producer abstraction (`IKafkaLogProducer`) and implementation for publishing `LogEventMessage` to `skysim.action.logs`.
- Add `IKafkaLogProducerOptions` bound to the `Kafka:Producer` configuration section.
- Add a typed `LoggerMiddleware` that runs on every ASP.NET Core HTTP request to capture method, path, status code, duration, request body, response body, and correlation ID.
- Extend the `LogEventMessage` contract (already has all required fields) with a factory method that builds an HTTP-action log message from the middleware context.
- Integrate the middleware into `Program.cs` and wire the producer as a singleton.
- Reuse the existing `SensitiveDataMasker` to mask sensitive fields before publishing.
- Producer failures are swallowed with structured logging so the HTTP request is never disrupted.
- Add unit tests for `KafkaLogProducer` and `LoggerMiddleware`.
- Add a smoke-test markdown document describing end-to-end verification steps.

## Capabilities

### New Capabilities

- `http-action-logging`: ASP.NET Core middleware that intercepts every HTTP request, builds a `LogEventMessage` from the request/response context, masks sensitive payloads, and asynchronously publishes it to the `skysim.action.logs` Kafka topic via a retry-backed producer. Failures are logged but never bubble up into the HTTP pipeline.
- `kafka-log-producer`: A `Confluent.Kafka`-based producer abstraction (`IKafkaLogProducer`) that wraps the producer client, exposes a `PublishAsync` method accepting `LogEventMessage`, applies a Polly retry policy with exponential backoff, and uses the configured `acks=all` for durability.

### Modified Capabilities

- `log-event-message-contract` (existing): No schema change required. The existing `LogEventMessage` DTO already contains `requestTime`, `responseTime`, `duration`, `requestData`, `responseData`, `errorCode`, `errorMessage`, `exception`, and `correlationId`. A static factory method will be added for constructing HTTP-action instances, but the contract itself is unchanged.

## Impact

- **Backend**:
  - New files: `Infrastructure/Kafka/IKafkaLogProducer.cs`, `Infrastructure/Kafka/KafkaLogProducer.cs`, `Infrastructure/Kafka/IKafkaLogProducerOptions.cs`, `Middlewares/LoggerMiddleware.cs`, `Middlewares/RequestBodyBufferingMiddleware.cs`, `tests/Skysim.Logger.Api.Tests/KafkaProducerTests/`, `tests/Skysim.Logger.Api.Tests/MiddlewareTests/`, `docs/smoke-test.md`.
  - Modified files: `appsettings.json` (Producer section already present — no change needed), `Program.cs` (wire middleware and producer), `Skysim.Logger.Api.csproj` (add `Polly` package reference if not already present).
  - No database schema changes.
  - No changes to existing Kafka consumer, controllers, or query services.
- **Configuration**: The `Kafka:Producer` block in `appsettings.json` is already defined and will be used. A `Logger:ServiceName` key is also already present.
- **Dependencies**: Requires `Polly` NuGet package for retry policy. `Confluent.Kafka` is already referenced by the consumer.
