## Context

The Skysim.Logger.Api currently acts as a pure consumer — it reads `LogEventMessage` records from the `skysim.action.logs` Kafka topic and persists them to PostgreSQL. A read-only Query API exposes the stored logs to the frontend. The service does not currently instrument its own HTTP traffic.

To achieve full traceability, every incoming HTTP request to the logger API should itself be captured as a log event and published back to the same Kafka topic, so it enters the same pipeline as logs from all other services. This turns the logger API into a self-instrumenting service.

Constraints imposed by the existing codebase:
- `LogEventMessage` DTO already has all fields needed for HTTP logging (`requestTime`, `responseTime`, `duration`, `requestData`, `responseData`, `correlationId`, `errorCode`, `errorMessage`, `exception`).
- `SensitiveDataMasker` and `SensitiveFields` are already implemented and registered as singletons.
- `KafkaConsumerOptions` already contains a `ProducerOptions` sub-section in `appsettings.json`.
- `Confluent.Kafka` is already a project dependency.

## Goals / Non-Goals

**Goals:**
- Publish an HTTP-action `LogEventMessage` to `skysim.action.logs` for every incoming request handled by the API.
- Ensure publishing never blocks or faults the HTTP response pipeline.
- Reuse existing masking utilities for sensitive data.
- Make the producer testable via an interface.

**Non-Goals:**
- Authentication or authorization — this is a logging instrument, not a security feature.
- Changes to the Kafka consumer, database schema, or Query API.
- Frontend changes.
- Using `ActionFilter` vs `Middleware` debate — middleware is chosen for its ability to wrap the entire pipeline, including request body reads.

## Decisions

### 1. HTTP Middleware Pattern: `LoggerMiddleware`

**Decision:** Use ASP.NET Core `IMiddleware` (or the conventional `RequestDelegate` middleware) for request/response interception.

**Rationale:** Middleware runs on every request regardless of which controller or endpoint handles it, which is exactly the semantics needed for uniform instrumentation. `IMiddleware` gives us a strongly-typed `HttpContext` and access to both the request stream (via a pre-buffered copy) and the response stream (via a response body buffering feature).

**Alternative considered — Action Filter:**
`ActionFilterAttribute` only runs for MVC/Razor Pages actions and is harder to make run on every pipeline including failed middleware short-circuits. Middleware is the more universal interception point.

### 2. Request Body Buffering

**Decision:** Add a lightweight `RequestBodyBufferingMiddleware` that runs **before** `LoggerMiddleware` to read and re-wind the request body stream into `EnableBuffering()` mode. This allows the downstream `LoggerMiddleware` to read the body multiple times — once for logging, once for the actual handler.

**Rationale:** ASP.NET Core request streams are forward-only and cannot be read after being consumed. `HttpRequest.EnableBuffering()` + seek to position 0 is the canonical solution. By separating this into its own middleware class, it remains reusable and is explicitly ordered before `LoggerMiddleware` in the pipeline.

### 3. Response Body Capture

**Decision:** Wrap `HttpResponse.Body` with a `StreamProxy` that copies bytes to an in-memory buffer as the response is written. After the next middleware completes, `LoggerMiddleware` reads the buffered copy.

**Rationale:** The response body stream is writable but not readable by default. Wrapping it is the standard pattern. A `MemoryStream` backed buffer is appropriate for typical API response sizes (a few KB). For very large payloads, an optional size cap could be added later.

### 4. Kafka Producer Abstraction

**Decision:** Define `IKafkaLogProducer` with a single `PublishAsync(LogEventMessage, CancellationToken)` method. Implement `KafkaLogProducer` using `Confluent.Kafka.IProducer<string, byte[]>`, with the message key set to `flowId ?? eventId.ToString()` and the value set to the JSON-serialized `LogEventMessage`.

**Rationale:** The existing consumer already uses `string` keys and `byte[]` values, so this maintains compatibility. The abstraction enables unit testing with a mock/fake implementation. The `eventId` is the natural idempotency key for deduplication.

**Alternative considered — `IKafkaLogProducer` backed by `IHostedService` with an in-memory channel:**
A channel-based approach (e.g., `System.Threading.Channels`) would decouple the HTTP thread from Kafka I/O and prevent blocking, but adds complexity (channel size limits, backpressure, channel consumer background service). Given the non-critical nature of logging failures, fire-and-forget `Task.Run` or async continuation is sufficient and simpler. The Polly retry policy provides durability guarantees.

### 5. Retry Strategy

**Decision:** Apply Polly's exponential backoff retry policy (already configured in `RetryOptions`) around the Kafka produce call.

**Rationale:** Transient network blips or broker restarts should not cause log events to be silently dropped. The retry config is already present in `appsettings.json` under `Kafka:Retry`. The existing `RetryPolicyFactory` can be reused.

**Alternative considered — publish to DLQ on final failure:**
The Kafka DLQ (`skysim.action.logs.dlq`) is consumer-side infrastructure for the main topic's dead-letter handling. Having the producer publish to the DLQ on failure creates a loop (DLQ messages would be consumed again). Instead, after exhausting all retries, log the failed event to the application logger (structured Serilog/ILogger) and discard — this is acceptable because:
1. HTTP logging is best-effort.
2. The application log still preserves the record.

### 6. `LogEventMessage` Factory Method

**Decision:** Add a static factory method `LogEventMessage.FromHttpContext(HttpContext, statusCode, requestBody, responseBody, exception)` to the existing DTO class.

**Rationale:** Keeps construction logic co-located with the DTO definition. Avoids creating a separate mapper class for a single use case. The method is internal/public as appropriate — internal would be preferred but the DTO is in `Contracts` which may be referenced by tests in other assemblies, so it can be public with `Obsolete` or internal via `InternalsVisibleTo` if needed later.

### 7. Correlation ID

**Decision:** Read correlation ID from the `X-Correlation-ID` or `X-Request-ID` request header. If absent, generate a new `Guid` and add it to the response headers with the same key.

**Rationale:** Consistent with distributed tracing conventions used across Skysim services. Both header names are checked for interoperability with different upstream services.

### 8. Failure Isolation

**Decision:** The entire Kafka publish operation (including masking, serialization, retry, and delivery confirmation wait) is wrapped in a `try-catch` that catches all exceptions, logs them via `ILogger<LoggerMiddleware>` at `Warning` level, and **does not re-throw**.

**Rationale:** The primary contract of the HTTP pipeline is returning a response. Logging instrumentation is secondary. Failures must never cause HTTP errors.

## Risks / Trade-offs

- **[Risk] Large request/response bodies cause memory pressure.**
  → **Mitigation:** Add an optional size cap (e.g., 64 KB) to the buffering middleware. Bodies larger than the cap are truncated or replaced with `"<body truncated>"` sentinel. This keeps memory bounded in production.

- **[Risk] Kafka broker is down, all publish attempts fail, logs are silently lost.**
  → **Mitigation:** The application structured logger still writes the event as a `Warning` during the final failure path. Operations can grep application logs as a fallback. A future enhancement could write to a local file as a fallback queue.

- **[Risk] Synchronous `IProducer.Flush()` on shutdown blocks application restart.**
  → **Mitigation:** On `IDispose`, call `IProducer.Flush(TimeSpan.FromSeconds(5))` with a timeout. Events that cannot be flushed within 5 seconds are logged and discarded — acceptable for best-effort HTTP logging.

- **[Trade-off] Fire-and-forget publish is not awaited by the HTTP pipeline.**
  → This means a request may return `200 OK` before the Kafka message is delivered. This is the intended behavior — HTTP response time must not be impacted by Kafka latency. For debugging, the caller can correlate via `eventId` (logged in response headers or returned in a custom header).

- **[Trade-off] Middleware order matters.**
  → `RequestBodyBufferingMiddleware` must run before `LoggerMiddleware`. If other middleware (e.g., authentication, CORS) consumes the request body first, buffering ensures it can still be read downstream. The registration order in `Program.cs` controls this.

## Migration Plan

This change is additive with no breaking surface changes.

1. Add `Polly` NuGet package if not already present.
2. Create `IKafkaLogProducer.cs`, `KafkaLogProducer.cs`, `IKafkaLogProducerOptions.cs`.
3. Create `RequestBodyBufferingMiddleware.cs` and `LoggerMiddleware.cs`.
4. Register producer and middleware in `Program.cs`.
5. Add unit tests for producer (mock `IProducer`) and middleware (mock producer).
6. Run smoke test against a live broker (see `docs/smoke-test.md`).
7. Deploy — no migration or rollback needed since this is purely additive instrumentation.

**Rollback:** Remove the middleware registration from `Program.cs` and delete the new files. No schema, config, or API changes.

## Open Questions

- Should `LoggerMiddleware` be opt-in per controller/action via a `[LogHttpRequest]` attribute, or should it be on by default for all endpoints? **Decision:** On by default for all endpoints. An attribute-based opt-out (`[NoHttpLog]`) could be added in a future iteration if needed.
- What `flowType` and `actionType` values should be used for HTTP-action logs? **Decision:** Use `FlowType.HttpAction` (new enum value) and `ActionType.HttpRequest` (new enum value) to clearly distinguish HTTP middleware logs from business-action logs.
- Should `eventId` be generated before or after masking? **Decision:** Always generate `eventId` first (a `Guid`), then mask the request/response data before publishing.
