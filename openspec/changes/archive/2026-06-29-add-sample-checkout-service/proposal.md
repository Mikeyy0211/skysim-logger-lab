## Why

The Logger module currently lacks a sample producer service to demonstrate that `Skysim.Logger.Client` can be used by external services to publish logs to Kafka. While `Logger.Client` has been extracted and tested, there is no integration point showing how a real service (like a checkout service) would use the middleware and producer to log HTTP requests and responses.

This change creates a minimal demo service that proves:
- `Logger.Client` can be used outside `Logger.Api`
- `LoggerMiddleware` captures HTTP request/response logs from another service
- Logs published by `SampleService` can be consumed by `Logger.Api` via Kafka

## What Changes

- Add a new `Skysim.Logger.SampleService` project that exposes a demo checkout eSIM endpoint
- Configure `LoggerMiddleware` from `Skysim.Logger.Client` to capture HTTP request/response logs
- Configure `KafkaLogProducer` from `Skysim.Logger.Client` to publish logs to Kafka
- Expose `POST /api/checkout/esim` endpoint that returns a mock response
- Determine checkout type (GUEST/AUTHENTICATED) based on Authorization header presence
- Add the new project to `Skysim.Logger.sln`
- Add Swagger/OpenAPI for easy manual testing in Development environment

## Capabilities

### New Capabilities

- `sample-checkout-service`: A minimal demo service that demonstrates external service integration with Logger.Client. This capability covers the new `Skysim.Logger.SampleService` project, its HTTP logging behavior, dependency constraints, and demo endpoint.

### Modified Capabilities

- No existing capabilities are modified.

## Impact

**New Projects:**
- `backend/Skysim.Logger.SampleService/` - New minimal checkout demo service

**Modified Projects:**
- `backend/Skysim.Logger.sln` - Added reference to SampleService project

**Kafka:**
- `SampleService` publishes HTTP request/response logs to `skysim.action.logs` topic via `LoggerMiddleware`

**Dependencies:**
- `Skysim.Logger.SampleService` references `Skysim.Logger.Client`
- `Skysim.Logger.SampleService` references `Skysim.Logger.Contracts`
- `Skysim.Logger.SampleService` does NOT reference `Skysim.Logger.Api`, `Skysim.Logger.Infrastructure`, or `Skysim.Logger.Common`

## Out of Scope

The following are explicitly NOT implemented in this phase:

- Multi-step business action logging (ORDER_CREATED, PAYMENT_REQUESTED, etc.)
- BusinessLogPublisher, BusinessActionFilter, CheckoutBusinessEvent, or any manual Kafka publishing beyond HTTP middleware
- JWT validation or authentication
- userId extraction from JWT claims (deferred to `add-logger-api-auth-integration`)
- Database or EF Core
- Real checkout, payment, provider, notification, or order service logic
- Modifications to Logger.Api, Logger.Client, or Logger.Contracts behavior
- Changes to Kafka topic names or LogEventMessage contract
