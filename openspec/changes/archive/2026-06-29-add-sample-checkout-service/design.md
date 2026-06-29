## Context

The Logger module has been refactored to separate concerns:
- `Skysim.Logger.Contracts` provides shared constants and `LogEventMessage`
- `Skysim.Logger.Client` provides reusable client-side components (`LoggerMiddleware`, `KafkaLogProducer`, `SensitiveDataMasker`)
- `Skysim.Logger.Api` consumes logs from Kafka and persists them to PostgreSQL

The existing `LoggerMiddleware` captures HTTP request/response logs and publishes them to the `skysim.action.logs` Kafka topic. The middleware already supports:
- Flow ID extraction from headers (`X-Flow-Id`, `X-Correlation-Id`, `X-Request-ID`) or auto-generation
- Request/response body capture with size limiting
- Sensitive data masking
- Response body size limiting (64KB max)

This design specifies a minimal sample service that proves the cross-service integration works.

## Goals / Non-Goals

**Goals:**
- Add a minimal `Skysim.Logger.SampleService` project that demonstrates external service integration with `Logger.Client`
- Prove that `Logger.Client` can be used outside `Logger.Api`
- Show how `LoggerMiddleware` captures HTTP logs from a different service
- Verify that logs published by `SampleService` can be consumed by `Logger.Api` via Kafka
- Provide an easy-to-test demo endpoint with Swagger UI

**Non-Goals:**
- Implement multi-step business action logging (ORDER_CREATED, PAYMENT_REQUESTED, etc.)
- Implement BusinessLogPublisher, BusinessActionFilter, CheckoutBusinessEvent, or any manual Kafka publishing
- Add JWT validation, authentication, or authorization
- Extract userId from JWT claims (deferred to future enhancement)
- Add database persistence for `SampleService`
- Create multiple business action endpoints or a full workflow
- Modify `Logger.Api`, `Logger.Client`, or `Logger.Contracts` behavior
- Add frontend changes

## Decisions

### Decision 1: HTTP Request/Response Logging Only

This phase demonstrates HTTP request/response logging for a checkout eSIM endpoint using `LoggerMiddleware`. The middleware automatically captures:
- Request method, path, query string, headers, and body
- Response status code and body
- Duration
- Flow ID (from header or generated)
- Service name

Checkout metadata (customerEmail, customerPhone, packageCode, quantity, orderId, checkoutType) is captured through the request/response payloads as JSON fields, which `LoggerMiddleware` already captures automatically.

**Alternatives considered:**
- Manually publish additional Kafka messages with business context: Deferred to a future enhancement. This phase focuses on proving the middleware/producer integration works for HTTP logging.
- Extend LoggerMiddleware to add explicit metadata mapping: Deferred. The existing middleware captures request/response bodies which contain the checkout data.

### Decision 2: Checkout Type from Authorization Header Presence

The endpoint `POST /api/checkout/esim` checks for the presence of an `Authorization` header:
- If `Authorization` header exists: `checkoutType = AUTHENTICATED`
- If `Authorization` header is absent: `checkoutType = GUEST`

**No JWT validation is performed.** This is a simple header presence check, not token validation.

**Alternatives considered:**
- Extract and validate JWT to set `checkoutType` and `userId`: Deferred to `add-logger-api-auth-integration`. This phase focuses on proving the logging infrastructure works.
- Always set `checkoutType = GUEST`: Rejected as it would not demonstrate the integration point for authenticated requests.

### Decision 3: Minimal Endpoint Response

The endpoint returns a simple success response with `flowId`, `orderId`, `checkoutType`, `status`, and `message`. No real checkout logic is executed.

The response body includes the checkout metadata, which will be captured by `LoggerMiddleware` for logging.

**Alternatives considered:**
- Simulate a multi-step checkout workflow: Deferred to a future enhancement. This change focuses on proving the middleware/producer integration.
- Return a 404 or error response: Rejected as the goal is to prove successful logging works end-to-end.

### Decision 4: Swagger for Demo

Add Swagger/OpenAPI to `SampleService` in Development environment so the checkout endpoint can be tested easily without external tools.

**Alternatives considered:**
- Require external tools like Postman: Rejected as Swagger provides a better developer experience for quick testing.
- Skip API documentation: Rejected as it helps verify the endpoint works during development.

### Decision 5: Project Reference Constraints

`SampleService` may only reference:
- `Skysim.Logger.Client`
- `Skysim.Logger.Contracts`

`SampleService` must NOT reference:
- `Skysim.Logger.Api`
- `Skysim.Logger.Infrastructure`
- `Skysim.Logger.Common`

This constraint proves that `Logger.Client` is a standalone, reusable library.

### Decision 6: Seed X-Flow-Id Before LoggerMiddleware for Consistent FlowId

SampleService MUST ensure a single shared flowId is used by both LoggerMiddleware and CheckoutController. If LoggerMiddleware generates a flowId independently and CheckoutController also generates one independently, the flowId in the response may not match the flowId stored in Logger.Api/PostgreSQL.

To solve this, SampleService adds a local `FlowIdSeedingMiddleware` before `LoggerMiddleware` in the pipeline:
1. If the request does not contain `X-Flow-Id`, generate a new flowId GUID
2. Set `Request.Headers["X-Flow-Id"]` to that value
3. If `X-Flow-Id` already exists, keep it unchanged
4. CheckoutController reads the flowId from `Request.Headers["X-Flow-Id"]` and returns that same value in `CheckoutEsimResponse`
5. LoggerMiddleware reads the same `X-Flow-Id` and publishes logs with the matching flowId

This approach:
- Does NOT modify `Skysim.Logger.Client`
- Is implemented as a local middleware specific to SampleService
- Ensures response flowId matches logged flowId
- Allows callers to provide their own flowId if desired

**Alternatives considered:**
- Have LoggerMiddleware expose a way to set the generated flowId back to HttpContext: Rejected as it would modify Logger.Client.
- Have CheckoutController call a shared FlowIdService to generate/consume flowId: Over-engineered for a demo service. Local middleware is simpler.
- Accept that flowIds may differ: Rejected as this breaks the ability to correlate response with logs.

## Risks / Trade-offs

[Risk] LoggerMiddleware default excluded paths include `/api/log-flows` and `/api/log-actions`

These exclusions are designed for `Logger.Api`. Since `SampleService` has different endpoints (`/api/checkout/*`), these exclusions will not affect `SampleService` traffic.

[Risk] HTTP logging only - no business action logging

This phase only captures HTTP request/response logs. Multi-step business action logging (ORDER_CREATED, etc.) is not implemented. This is intentional to keep the phase focused. Future enhancements can add business action logging using the same `Logger.Client` components.

[Risk] Fire-and-forget Kafka publishing may silently fail

The `LoggerMiddleware` catches exceptions during publishing and logs warnings instead of failing the request. Transient Kafka failures do not affect the HTTP response.

[Risk] No JWT validation means userId extraction is not tested

User ID extraction from JWT claims is not tested in this phase. The `checkoutType` is determined by header presence only. JWT validation and userId extraction are deferred to `add-logger-api-auth-integration`.

[Risk] FlowId mismatch between response and log (mitigated by Decision 6)

Without the `FlowIdSeedingMiddleware`, LoggerMiddleware might generate one flowId and CheckoutController might generate another. This would cause the flowId in the response to differ from the flowId stored in Kafka/PostgreSQL. The seeding middleware ensures both use the same flowId.

## Project Structure

```
backend/Skysim.Logger.SampleService/
├── Skysim.Logger.SampleService.csproj
├── Program.cs
├── appsettings.json
├── Middlewares/
│   └── FlowIdSeedingMiddleware.cs
├── Controllers/
│   └── CheckoutController.cs
└── DTOs/
    ├── CheckoutEsimRequest.cs
    └── CheckoutEsimResponse.cs
```

## Configuration

`appsettings.json` includes:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Producer": {
      "Acks": "all",
      "RetryMaxAttempts": 3,
      "RetryBaseDelayMs": 100
    }
  },
  "Logger": {
    "ServiceName": "sample-checkout-service"
  }
}
```

## Open Questions

1. **Should SampleService expose a health endpoint?** - Not required for this phase. If needed for container orchestration, it can be added later.

2. **Should we add integration tests that verify logs reach Kafka?** - Deferred to a future change (`verify-logger-e2e-pipeline`) that sets up the full E2E test infrastructure.

3. **Should we add JWT validation?** - Deferred to `add-logger-api-auth-integration`. This phase focuses on proving the logging infrastructure works without authentication.
