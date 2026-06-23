# Capability: Logger Cross-Cutting Concerns

## ADDED Requirements

### Requirement: Error handling is layered and explicit
The Logger SHALL classify errors as Validation, Transient, or Permanent and SHALL treat them differently across ingestion and API layers.

#### Scenario: Validation errors skip retry
- **WHEN** a Kafka message fails schema validation
- **THEN** the consumer publishes it to DLQ without retry

#### Scenario: Transient errors retry with backoff
- **WHEN** a database connection error occurs during ingestion
- **THEN** the consumer retries up to 5 times with exponential backoff

#### Scenario: API validation errors return HTTP 400
- **WHEN** a Logger API request has an invalid query parameter
- **THEN** the response is HTTP 400 with a validation error DTO

### Requirement: Retry uses bounded in-process exponential backoff
The retry policy SHALL be configurable with parameters `maxAttempts` (default 5), `initialDelayMs` (default 200), `backoffMultiplier` (default 2.0), and `maxDelayMs` (default 5000). After `maxAttempts`, the message SHALL go to DLQ.

#### Scenario: Backoff schedule is 200, 400, 800, 1600, 3200 ms
- **WHEN** retries 1..5 are triggered with default config
- **THEN** delays are 200ms, 400ms, 800ms, 1600ms, 3200ms

#### Scenario: Configurable max attempts
- **WHEN** `Logger:Kafka:Retry:MaxAttempts = 3`
- **THEN** the consumer makes at most 3 attempts before DLQ

### Requirement: Idempotency is enforced at the database, not in application memory
Idempotency SHALL rely on the `UNIQUE (event_id)` constraint on `log_actions`. Application-level de-duplication MAY exist as a fast path but SHALL NOT be the only mechanism.

#### Scenario: Race condition is handled by DB
- **WHEN** two consumer instances process the same `eventId` concurrently
- **THEN** exactly one insert succeeds; the other receives a unique-violation error and is treated as an idempotent skip

### Requirement: Sensitive data masking is centralized and tested
A single `SensitiveDataMasker` SHALL own the deny-list and SHALL be called from (a) the Kafka consumer before persistence, (b) the Kafka consumer before DLQ publish, and (c) the middleware before payload capture. The deny-list SHALL live in `Logger.Core/Common/SensitiveFields.cs`.

#### Scenario: Deny-list contains the workspace-mandated fields
- **WHEN** the deny-list is read
- **THEN** it includes `password`, `access_token`, `refresh_token`, `authorization`, `otp`, `cardNumber`, `cvv`, `paymentSecret`, `secret`, `token` (case-insensitive)

#### Scenario: Masking is recursive
- **WHEN** a nested object contains a sensitive key at any depth
- **THEN** every matching leaf value is replaced with `"***"` while structure is preserved

#### Scenario: Masker is unit-tested
- **WHEN** the test suite runs
- **THEN** there is at least one test per sensitive field verifying masking at top level, nested level, and array element level

### Requirement: Dead-letter topic name is fixed
The DLQ topic SHALL be named `skysim.action.logs.dlq`. Messages SHALL be produced with the original message key and the masked JSON payload, plus the headers documented in `kafka-log-consumer-flow`.

#### Scenario: DLQ topic name is constant
- **WHEN** the consumer publishes a dead-letter message
- **THEN** it uses topic `skysim.action.logs.dlq` and no other topic

### Requirement: Observability is logged at structured level
The Logger SHALL use `ILogger<T>` with structured logging. Every consumer step, retry, idempotent skip, DLQ publish, and API request SHALL emit at least one structured log entry with a stable shape.

#### Scenario: Consumer logs each message
- **WHEN** a message is processed
- **THEN** the consumer logs `{ eventId, flowId, actionType, status, durationMs, outcome }` where `outcome ∈ { persisted, idempotent_skip, dlq, retry }`

#### Scenario: API logs each request
- **WHEN** an API request completes
- **THEN** the middleware logs `{ service, action, statusCode, durationMs, correlationId, userId }`
