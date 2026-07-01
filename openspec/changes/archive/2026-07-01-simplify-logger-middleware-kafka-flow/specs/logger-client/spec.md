# logger-client Specification (Simplified)

This is a delta specification showing the simplified middleware requirements per PM feedback.

## MODIFIED Requirements

### Requirement: Logger Middleware shall capture HTTP context and publish to Kafka

The `LoggerMiddleware` SHALL intercept HTTP requests and responses, capture basic HTTP telemetry, and publish a simplified log message to Kafka. The middleware SHALL NOT understand business logic or create business action sequences.

#### Scenario: Middleware publishes HTTP log for successful request

- **GIVEN** a backend service has registered `LoggerMiddleware` in its pipeline
- **AND** a client sends an HTTP POST request to `/api/orders` with body `{"email":"test@example.com","orderId":"ORD-001"}`
- **AND** the request includes `X-Flow-Id: my-flow-123` header
- **WHEN** the request completes successfully with status code 201
- **THEN** `LoggerMiddleware` SHALL publish a log message to Kafka topic `skysim.action.logs`
- **AND** the message SHALL have `flowId` set to `"my-flow-123"`
- **AND** the message SHALL have `method` set to `"POST"`
- **AND** the message SHALL have `path` set to `"/api/orders"`
- **AND** the message SHALL have `statusCode` set to `201`
- **AND** the message SHALL have `durationMs` greater than `0`
- **AND** the message SHALL have `sourceService` set from `X-Source-Service` header if present

#### Scenario: Middleware publishes HTTP log for failed request

- **GIVEN** a backend service has registered `LoggerMiddleware` in its pipeline
- **AND** a client sends an HTTP GET request to `/api/orders/invalid-id`
- **WHEN** the downstream handler returns HTTP 404
- **THEN** `LoggerMiddleware` SHALL publish a log message with `statusCode` set to `404`
- **AND** the message SHALL have `status` set to `"Failed"`

#### Scenario: Middleware captures error information when exception occurs

- **GIVEN** a backend service has registered `LoggerMiddleware` in its pipeline
- **AND** a client sends an HTTP POST request to `/api/orders`
- **WHEN** the downstream handler throws an unhandled `ArgumentException`
- **THEN** `LoggerMiddleware` SHALL publish a log message with `errorCode` set to `"500"`
- **AND** the message SHALL have `errorMessage` set to the exception message
- **AND** the middleware SHALL re-throw the original exception to allow normal error handling

#### Scenario: Middleware extracts flowId from X-Flow-Id header

- **GIVEN** a backend service has registered `LoggerMiddleware` in its pipeline
- **AND** a client sends an HTTP request to `/api/orders` with header `X-Flow-Id: my-flow-abc`
- **WHEN** the request completes
- **THEN** `LoggerMiddleware` SHALL use `"my-flow-abc"` as the `flowId`
- **AND** the response SHALL include `X-Flow-Id` header with the same value

#### Scenario: Middleware extracts flowId from X-Correlation-Id header as fallback

- **GIVEN** a backend service has registered `LoggerMiddleware` in its pipeline
- **AND** a client sends an HTTP request to `/api/orders` with header `X-Correlation-Id: corr-456`
- **AND** no `X-Flow-Id` header is present
- **WHEN** the request completes
- **THEN** `LoggerMiddleware` SHALL use `"corr-456"` as the `flowId`

#### Scenario: Middleware extracts flowId from X-Request-Id header as fallback

- **GIVEN** a backend service has registered `LoggerMiddleware` in its pipeline
- **AND** a client sends an HTTP request to `/api/orders` with header `X-Request-ID: req-789`
- **AND** no `X-Flow-Id` or `X-Correlation-Id` header is present
- **WHEN** the request completes
- **THEN** `LoggerMiddleware` SHALL use `"req-789"` as the `flowId`

#### Scenario: Middleware generates flowId when no correlation header is present

- **GIVEN** a backend service has registered `LoggerMiddleware` in its pipeline
- **AND** a client sends an HTTP request to `/api/orders` with no correlation headers
- **WHEN** the request completes
- **THEN** `LoggerMiddleware` SHALL generate a new GUID for `flowId`
- **AND** the response SHALL include `X-Flow-Id` header with the generated GUID

#### Scenario: Middleware masks sensitive fields in request body

- **GIVEN** a backend service has registered `LoggerMiddleware` with sensitive field masking
- **AND** a client sends an HTTP POST request to `/api/auth/login` with body `{"password":"secret123","username":"john"}`
- **WHEN** the request body is captured and masked
- **THEN** the published log message SHALL have `requestBody` containing `"***"` instead of the password value
- **AND** the `username` value SHALL NOT be masked

#### Scenario: Middleware limits request body size

- **GIVEN** a backend service has registered `LoggerMiddleware`
- **AND** a client sends an HTTP POST request with a 100KB JSON body
- **WHEN** the request body is captured
- **THEN** the middleware SHALL store `"[too large]"` if body exceeds 32KB limit

#### Scenario: Middleware limits response body size

- **GIVEN** a backend service has registered `LoggerMiddleware`
- **AND** a client sends an HTTP GET request that returns a 1MB JSON response
- **WHEN** the response is captured
- **THEN** the middleware SHALL store `"[too large]"` if response exceeds 32KB limit

#### Scenario: Middleware does not break business request if Kafka publish fails

- **GIVEN** a backend service has registered `LoggerMiddleware` in its pipeline
- **AND** the Kafka producer is unavailable
- **AND** a client sends an HTTP request to `/api/orders`
- **WHEN** the request completes
- **THEN** the business request SHALL succeed normally
- **AND** `LoggerMiddleware` SHALL log a warning about the Kafka publish failure
- **AND** the original exception (if any) SHALL NOT be swallowed

#### Scenario: Middleware does not understand business logic

- **GIVEN** a backend service has registered `LoggerMiddleware` in its pipeline
- **AND** a client sends an HTTP POST request to `/api/checkout`
- **WHEN** the request completes
- **THEN** `LoggerMiddleware` SHALL NOT create business action types such as `ORDER_CREATED`, `PAYMENT_SUCCESS`, or `EMAIL_SENT`
- **AND** `LoggerMiddleware` SHALL ONLY publish HTTP-level logging messages
- **AND** Business action logging SHALL be handled separately by `BusinessActionLogger`

#### Scenario: Middleware captures source service from header

- **GIVEN** a backend service has registered `LoggerMiddleware` in its pipeline
- **AND** a client sends an HTTP request to `/api/orders` with header `X-Source-Service: WebApp`
- **WHEN** the request completes
- **THEN** the published log message SHALL have `sourceService` set to `"WebApp"`

---

### Requirement: Logger Client shall publish simplified log messages to Kafka

The `KafkaLogProducer` SHALL publish log messages with flattened HTTP fields directly in the message, without nested `RequestData`/`ResponseData` structures.

#### Scenario: Producer publishes message with flattened HTTP fields

- **GIVEN** a `KafkaLogProducer` is configured with valid Kafka bootstrap servers
- **AND** a log message is created with `eventId`, `flowId`, `method`, `path`, `statusCode`, `durationMs`
- **WHEN** `producer.PublishAsync(message)` is called
- **THEN** the Kafka message SHALL contain flattened fields at the top level
- **AND** the Kafka message SHALL NOT contain nested `RequestData` or `ResponseData` objects

#### Scenario: Producer uses flowId as message key

- **GIVEN** a `KafkaLogProducer` is configured with valid Kafka bootstrap servers
- **AND** a log message is created with `flowId` = `"order-flow-001"`
- **WHEN** `producer.PublishAsync(message)` is called
- **THEN** the Kafka message key SHALL be `"order-flow-001"`

#### Scenario: Producer retries on transient Kafka failures

- **GIVEN** a `KafkaLogProducer` is configured with `retryMaxAttempts` = 3
- **AND** the first two Kafka produce calls fail with a transient error
- **AND** the third Kafka produce call succeeds
- **WHEN** `producer.PublishAsync(message)` is called
- **THEN** the producer SHALL retry up to 3 times
- **AND** the message SHALL be delivered successfully on the third attempt

#### Scenario: Producer logs warning after max retries

- **GIVEN** a `KafkaLogProducer` is configured with `retryMaxAttempts` = 2
- **AND** every Kafka produce call fails with a transient error
- **WHEN** `producer.PublishAsync(message)` is called
- **THEN** the producer SHALL attempt exactly 2 times
- **AND** the producer SHALL log a warning with eventId and flowId
- **AND** the method SHALL return without throwing

---

### Requirement: Logger Client shall mask sensitive data simply

The Logger Client SHALL provide a `SensitiveDataMasker` that masks sensitive fields in JSON strings by replacing values with `"***"`.

#### Scenario: SensitiveDataMasker masks standard sensitive fields

- **GIVEN** a `SensitiveDataMasker` is instantiated
- **WHEN** `MaskJson` is called on JSON containing `password`, `token`, `accessToken`, `refreshToken`, `authorization`, `secret`, `apiKey`
- **THEN** all sensitive field values SHALL be replaced with `"***"`

#### Scenario: SensitiveDataMasker handles non-JSON input gracefully

- **GIVEN** a `SensitiveDataMasker` is instantiated
- **AND** a plain text string or invalid JSON is provided
- **WHEN** `MaskJson(input)` is called
- **THEN** the result SHALL be returned unchanged without throwing

#### Scenario: SensitiveDataMasker handles null or empty input

- **GIVEN** a `SensitiveDataMasker` is instantiated
- **AND** a `null` or empty string is provided
- **WHEN** `MaskJson(input)` is called
- **THEN** the result SHALL be `null` or empty string respectively
