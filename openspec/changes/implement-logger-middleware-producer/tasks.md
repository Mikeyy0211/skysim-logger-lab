## 1. Enums Extension

- [x] 1.1 Add `HttpAction` to the `FlowType` enum in `Domain/Enums/FlowType.cs`
- [x] 1.2 Add `HttpRequest` to the `ActionType` enum in `Domain/Enums/ActionType.cs`

## 2. Kafka Producer Abstraction

- [x] 2.1 Create `Infrastructure/Kafka/IKafkaLogProducerOptions.cs` — interface exposing `BootstrapServers`, `Acks`, and a `RetryOptions` reference
- [x] 2.2 Create `Infrastructure/Kafka/IKafkaLogProducer.cs` — interface with `PublishAsync(LogEventMessage, CancellationToken)`
- [x] 2.3 Create `Infrastructure/Kafka/KafkaLogProducer.cs` — implementation using `Confluent.Kafka.IProducer<string, byte[]>`, JSON serialization, Polly retry, and graceful dispose with 5-second `Flush` timeout
- [x] 2.4 Register `IKafkaLogProducer` as a singleton in `Program.cs` using the `Kafka:Producer` configuration section

## 3. Request/Response Body Buffering

- [x] 3.1 Create `Middlewares/RequestBodyBufferingMiddleware.cs` — calls `HttpRequest.EnableBuffering()` and rewinds the stream to position 0 after reading
- [x] 3.2 Register `RequestBodyBufferingMiddleware` as the first middleware in the pipeline in `Program.cs`

## 4. Logger Middleware

- [x] 4.1 Create `Middlewares/ResponseBodyBufferingStream.cs` — `Stream` wrapper that copies writes to a `MemoryStream` buffer while delegating to the underlying `HttpResponse.Body`
- [x] 4.2 Create `Middlewares/LoggerMiddleware.cs` — captures request method, path, query, status code, timestamps, duration, correlation ID, reads buffered request/response bodies, builds a `LogEventMessage`, masks sensitive data via `SensitiveDataMasker`, and calls `IKafkaLogProducer.PublishAsync` (fire-and-forget with warning-level error handling)
- [x] 4.3 Add static factory method `LogEventMessage.FromHttpContext(...)` that constructs the message with `FlowType.HttpAction`, `ActionType.HttpRequest`, `serviceName` from config, and `Status` derived from status code
- [x] 4.4 Register `LoggerMiddleware` in `Program.cs` after `RequestBodyBufferingMiddleware`

## 5. Program.cs Integration

- [x] 5.1 Add `Polly` NuGet package to `Skysim.Logger.Api.csproj` if not already present
- [x] 5.2 Wire `IKafkaLogProducerOptions` configuration from `Kafka:Producer` section
- [x] 5.3 Register `RequestBodyBufferingMiddleware` and `LoggerMiddleware` in the correct pipeline order

## 6. Unit Tests

- [x] 6.1 Create `KafkaLogProducerTests.cs` in `Skysim.Logger.Api.Tests` — mock `IKafkaLogProducer` and verify publish is called, retries are triggered on transient failure, and no exception propagates after exhausting retries
- [x] 6.2 Create `LoggerMiddlewareTests.cs` — mock `IKafkaLogProducer`, invoke middleware with `HttpContext` carrying request body, response body, and correlation header, verify `PublishAsync` is called with a correctly built `LogEventMessage`
- [x] 6.3 Create `SensitiveDataMaskerTests.cs` or extend existing tests — verify sensitive fields are masked in HTTP request/response payloads

## 7. Documentation

- [x] 7.1 Create `docs/smoke-test.md` — end-to-end verification steps: start Kafka + PostgreSQL via Docker Compose, call a known endpoint (e.g., `GET /api/log-flows`), verify a record appears in the `log_actions` table with `actionType = HttpRequest`, verify sensitive data is masked in `log_action_details`
