## ADDED Requirements

### Requirement: Logger Client shall provide reusable HTTP logging middleware

The Logger Client SHALL provide a `LoggerMiddleware` component that intercepts HTTP requests and responses, captures telemetry data, and publishes a structured `LogEventMessage` to Kafka. The middleware SHALL be usable by any ASP.NET Core backend service without coupling to `Skysim.Logger.Api`.

#### Scenario: Middleware publishes log event for successful HTTP request

- **GIVEN** a backend service has registered `LoggerMiddleware` in its pipeline
- **AND** a client sends an HTTP POST request with JSON body to `/api/orders` with `Content-Type: application/json`
- **AND** the request includes `X-Flow-Id: my-flow-123` header
- **AND** the request body is valid JSON: `{"email":"test@example.com","orderId":"ORD-001"}`
- **AND** the downstream handler returns HTTP 201 with JSON response body: `{"orderId":"ORD-001","status":"created"}`
- **WHEN** the request completes successfully with status code 201
- **THEN** `LoggerMiddleware` SHALL publish a `LogEventMessage` to Kafka topic `skysim.action.logs`
- **AND** the message SHALL have `flowId` set to `"my-flow-123"`
- **AND** the message SHALL have `actionType` set to `"HttpRequest"`
- **AND** the message SHALL have `status` set to `"Success"`
- **AND** the message SHALL have `requestData.body.email` set to `"test@example.com"` (email is NOT a sensitive field, so it is preserved)
- **AND** the message SHALL have `requestData.body.orderId` set to `"ORD-001"`
- **AND** the message SHALL have `responseData.body.orderId` set to `"ORD-001"`
- **AND** the message SHALL have `duration` greater than 0

#### Scenario: Middleware publishes log event for failed HTTP request

- **GIVEN** a backend service has registered `LoggerMiddleware` in its pipeline
- **AND** a client sends an HTTP GET request to `/api/orders/invalid-id`
- **WHEN** the downstream handler returns HTTP 404
- **THEN** `LoggerMiddleware` SHALL publish a `LogEventMessage` with `status` set to `"Failed"`
- **AND** the message SHALL have `responseData.statusCode` set to 404

#### Scenario: Middleware publishes log event when exception is thrown

- **GIVEN** a backend service has registered `LoggerMiddleware` in its pipeline
- **AND** a client sends an HTTP POST request to `/api/orders`
- **WHEN** the downstream handler throws an unhandled `ArgumentException`
- **THEN** `LoggerMiddleware` SHALL publish a `LogEventMessage` with `status` set to `"Failed"`
- **AND** the message SHALL have `errorMessage` set to the exception message
- **AND** the message SHALL have `exception` set to the full exception stack trace
- **AND** the message SHALL have `errorCode` set to `"500"`

#### Scenario: Middleware extracts flowId from X-Flow-Id header

- **GIVEN** a backend service has registered `LoggerMiddleware` in its pipeline
- **AND** a client sends an HTTP request to `/api/orders` with header `X-Flow-Id: my-flow-abc`
- **WHEN** the request completes
- **THEN** `LoggerMiddleware` SHALL use `"my-flow-abc"` as the `flowId`
- **AND** the response SHALL include `X-Correlation-ID: my-flow-abc` header

#### Scenario: Middleware extracts flowId from X-Correlation-Id header as fallback

- **GIVEN** a backend service has registered `LoggerMiddleware` in its pipeline
- **AND** a client sends an HTTP request to `/api/orders` with header `X-Correlation-Id: corr-456`
- **AND** no `X-Flow-Id` header is present
- **WHEN** the request completes
- **THEN** `LoggerMiddleware` SHALL use `"corr-456"` as the `flowId`

#### Scenario: Middleware extracts flowId from X-Request-ID header as fallback

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
- **AND** the response SHALL include `X-Correlation-ID` header with the generated GUID

#### Scenario: Middleware masks sensitive fields in request body

- **GIVEN** a backend service has registered `LoggerMiddleware` with `SensitiveDataMasker`
- **AND** a client sends an HTTP POST request to `/api/auth/login` with JSON body: `{"password":"secret123","username":"john"}`
- **WHEN** the request body is read and masked before publishing
- **THEN** the published `LogEventMessage` SHALL have `requestData.body.password` set to `"***"`
- **AND** the published `LogEventMessage` SHALL have `requestData.body.username` set to `"john"`

#### Scenario: Middleware masks sensitive fields in query string

- **GIVEN** a backend service has registered `LoggerMiddleware` with `SensitiveDataMasker`
- **AND** a client sends an HTTP GET request to `/api/search?password=secret&q=phone`
- **WHEN** the request is processed
- **THEN** the published `LogEventMessage` SHALL have `requestData.query.password` set to `"***"`
- **AND** the published `LogEventMessage` SHALL have `requestData.query.q` set to `"phone"`

#### Scenario: Middleware limits response body size to 64KB

- **GIVEN** a backend service has registered `LoggerMiddleware`
- **AND** a client sends an HTTP GET request that returns a 1MB JSON response body
- **WHEN** the response is captured
- **THEN** the published `LogEventMessage` SHALL have `responseData` containing only `{"statusCode": 200}` without the full body

#### Scenario: Middleware omits response body for binary content types

- **GIVEN** a backend service has registered `LoggerMiddleware`
- **AND** a client sends an HTTP GET request that returns `Content-Type: application/octet-stream`
- **WHEN** the response is captured
- **THEN** the published `LogEventMessage` SHALL have `responseData` containing only `{"statusCode": 200}` without body

#### Scenario: Middleware handles non-JSON request body gracefully

- **GIVEN** a backend service has registered `LoggerMiddleware`
- **AND** a client sends an HTTP POST request to `/api/upload` with `Content-Type: text/plain` and body `"plain text content"`
- **WHEN** the request is processed
- **THEN** `LoggerMiddleware` SHALL publish a `LogEventMessage` without crashing
- **AND** the message SHALL have `requestData.body` set to `"plain text content"` (treated as plain string)

---

### Requirement: Logger Client shall publish log events to Kafka

The Logger Client SHALL provide a `IKafkaLogProducer` interface and `KafkaLogProducer` implementation that serializes `LogEventMessage` to JSON and publishes it to the `skysim.action.logs` Kafka topic, using `flowId` as the message key.

#### Scenario: Producer publishes message with flowId as key

- **GIVEN** a `KafkaLogProducer` is configured with valid Kafka bootstrap servers
- **AND** a `LogEventMessage` is created with `eventId` = `Guid.NewGuid()` and `flowId` = `"order-flow-001"`
- **WHEN** `producer.PublishAsync(message)` is called
- **THEN** the Kafka message key SHALL be `"order-flow-001"`

#### Scenario: Producer uses eventId as key when flowId is empty

- **GIVEN** a `KafkaLogProducer` is configured with valid Kafka bootstrap servers
- **AND** a `LogEventMessage` is created with `eventId` = `Guid.Parse("12345678-1234-1234-1234-123456789012")` and `flowId` = `null`
- **WHEN** `producer.PublishAsync(message)` is called
- **THEN** the Kafka message key SHALL be `"12345678-1234-1234-1234-123456789012"`

#### Scenario: Producer retries on transient Kafka failures

- **GIVEN** a `KafkaLogProducer` is configured with `retryMaxAttempts` = 3 and `retryBaseDelayMs` = 100
- **AND** the first two Kafka produce calls fail with a transient error
- **AND** the third Kafka produce call succeeds
- **WHEN** `producer.PublishAsync(message)` is called
- **THEN** the producer SHALL retry up to 3 times with exponential backoff
- **AND** the message SHALL be delivered successfully on the third attempt

#### Scenario: Producer gives up after max retries and logs warning

- **GIVEN** a `KafkaLogProducer` is configured with `retryMaxAttempts` = 2
- **AND** every Kafka produce call fails with a transient error
- **WHEN** `producer.PublishAsync(message)` is called
- **THEN** the producer SHALL attempt exactly 2 times
- **AND** the producer SHALL log a warning with eventId and flowId
- **AND** the method SHALL return without throwing

#### Scenario: Producer sets serviceName from constructor parameter

- **GIVEN** a `KafkaLogProducer` is instantiated with `serviceName` = `"OrderService"`
- **AND** a `LogEventMessage` with `serviceName` = `null` is published
- **WHEN** `producer.PublishAsync(message)` is called
- **THEN** the published message SHALL have `serviceName` set to `"OrderService"`

#### Scenario: Producer handles serialization failure gracefully

- **GIVEN** a `KafkaLogProducer` is configured
- **AND** a `LogEventMessage` contains a property that throws during JSON serialization
- **WHEN** `producer.PublishAsync(message)` is called
- **THEN** the producer SHALL log a warning with the eventId
- **AND** the method SHALL return without throwing

---

### Requirement: Logger Client shall mask sensitive data before publishing

The Logger Client SHALL provide a `ISensitiveDataMasker` interface and `SensitiveDataMasker` implementation that recursively traverses JSON objects and replaces the values of sensitive fields with `"***"`. The set of sensitive field names SHALL be sourced from `Skysim.Logger.Contracts.Constants.SensitiveFieldNames`.

#### Scenario: SensitiveDataMasker masks sensitive fields in flat JSON object

- **GIVEN** a `SensitiveDataMasker` is instantiated using field values from `SensitiveFieldNames` in `Skysim.Logger.Contracts`
- **AND** a JSON string is provided: `{"username":"john","password":"secret","token":"abc123"}`
- **WHEN** `MaskJson(json)` is called
- **THEN** the result SHALL be `{"username":"john","password":"***","token":"***"}`
- **AND** the `"username"` value SHALL NOT be masked

#### Scenario: SensitiveDataMasker masks nested sensitive fields

- **GIVEN** a `SensitiveDataMasker` is instantiated
- **AND** a JSON string is provided: `{"user":{"name":"john","password":"secret"},"cardNumber":"4111111111111111"}`
- **WHEN** `MaskJson(json)` is called
- **THEN** the result SHALL be `{"user":{"name":"john","password":"***"},"cardNumber":"***"}`

#### Scenario: SensitiveDataMasker masks sensitive fields in JSON arrays

- **GIVEN** a `SensitiveDataMasker` is instantiated
- **AND** a JSON string is provided: `[{"type":"login","password":"pwd1"},{"type":"login","password":"pwd2"}]`
- **WHEN** `MaskJson(json)` is called
- **THEN** both array items SHALL have `password` masked to `"***"`

#### Scenario: SensitiveDataMasker preserves non-sensitive fields

- **GIVEN** a `SensitiveDataMasker` is instantiated
- **AND** a JSON string is provided: `{"email":"test@example.com","phone":"+1234567890","orderId":"ORD-001"}`
- **WHEN** `MaskJson(json)` is called
- **THEN** the result SHALL preserve all field values unchanged

#### Scenario: SensitiveDataMasker handles empty string input

- **GIVEN** a `SensitiveDataMasker` is instantiated
- **AND** an empty string is provided
- **WHEN** `MaskJson("")` is called
- **THEN** the result SHALL be an empty string

#### Scenario: SensitiveDataMasker handles null input

- **GIVEN** a `SensitiveDataMasker` is instantiated
- **AND** a `null` string is provided
- **WHEN** `MaskJson(null)` is called
- **THEN** the result SHALL be `null`

#### Scenario: SensitiveDataMasker handles invalid JSON gracefully

- **GIVEN** a `SensitiveDataMasker` is instantiated
- **AND** an invalid JSON string is provided: `"not valid json {"`
- **WHEN** `MaskJson(invalidJson)` is called
- **THEN** the result SHALL be the original invalid JSON string unchanged

#### Scenario: SensitiveDataMasker masks all standard sensitive field names

- **GIVEN** a `SensitiveDataMasker` is instantiated using field values from `SensitiveFieldNames`
- **WHEN** `MaskJson` is called on JSON containing all sensitive fields: `"password"`, `"access_token"`, `"refresh_token"`, `"authorization"`, `"otp"`, `"cardNumber"`, `"cvv"`, `"paymentSecret"`, `"secret"`, `"token"`
- **THEN** all sensitive field values SHALL be replaced with `"***"`

#### Scenario: SensitiveDataMasker is case-insensitive for field names

- **GIVEN** a `SensitiveDataMasker` is instantiated
- **AND** a JSON string is provided: `{"PASSWORD":"secret","Token":"abc","CARDNUMBER":"4111111111111111"}`
- **WHEN** `MaskJson(json)` is called
- **THEN** all three field values SHALL be masked to `"***"`

---

### Requirement: Logger Client shall handle request body buffering internally

The Logger Client SHALL enable request body buffering inside `LoggerMiddleware` so that the request body can be read without requiring a separate `RequestBodyBufferingMiddleware` in the pipeline. The body SHALL remain readable by downstream handlers after `LoggerMiddleware` reads it.

#### Scenario: Middleware enables request body buffering without separate middleware

- **GIVEN** a backend service has registered only `LoggerMiddleware` in its pipeline (no `RequestBodyBufferingMiddleware`)
- **AND** a client sends an HTTP POST request to `/api/orders` with JSON body
- **WHEN** `LoggerMiddleware` reads the request body
- **THEN** the body SHALL be readable because `EnableBuffering()` was called internally
- **AND** downstream handlers SHALL still receive the request body with position reset to 0

#### Scenario: Middleware can read request body multiple times

- **GIVEN** a backend service has registered only `LoggerMiddleware` in its pipeline
- **AND** a client sends an HTTP POST request to `/api/orders` with JSON body `{"orderId":"ORD-001"}`
- **WHEN** the downstream handler reads the request body (e.g., model binding)
- **THEN** the downstream handler SHALL successfully deserialize the body because the position was reset to 0

---

### Requirement: Logger Client shall support authenticated and anonymous requests

The Logger Client SHALL extract `userId` from JWT claims when the request is authenticated. When the request is not authenticated, `userId` SHALL be `null`.

#### Scenario: Middleware extracts userId from sub claim

- **GIVEN** a backend service has registered `LoggerMiddleware` in its pipeline
- **AND** a client sends an authenticated HTTP request with a valid JWT bearer token
- **AND** the JWT contains claim `sub` with value `"user-789"`
- **WHEN** the request completes
- **THEN** `LoggerMiddleware` SHALL publish a `LogEventMessage` with `userId` set to `"user-789"`

#### Scenario: Middleware extracts userId from NameIdentifier claim

- **GIVEN** a backend service has registered `LoggerMiddleware` in its pipeline
- **AND** a client sends an authenticated HTTP request with a valid JWT bearer token
- **AND** the JWT contains claim `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier` with value `"user-abc"`
- **AND** the JWT does not contain a `sub` claim
- **WHEN** the request completes
- **THEN** `LoggerMiddleware` SHALL publish a `LogEventMessage` with `userId` set to `"user-abc"`

#### Scenario: Middleware extracts userId from custom userId claim

- **GIVEN** a backend service has registered `LoggerMiddleware` in its pipeline
- **AND** a client sends an authenticated HTTP request with a valid JWT bearer token
- **AND** the JWT contains a custom claim `userId` with value `"user-xyz"`
- **AND** the JWT does not contain `sub` or `NameIdentifier` claims
- **WHEN** the request completes
- **THEN** `LoggerMiddleware` SHALL publish a `LogEventMessage` with `userId` set to `"user-xyz"`

#### Scenario: Middleware sets userId to null for anonymous requests

- **GIVEN** a backend service has registered `LoggerMiddleware` in its pipeline
- **AND** a client sends an HTTP request without authentication
- **WHEN** the request completes
- **THEN** `LoggerMiddleware` SHALL publish a `LogEventMessage` with `userId` set to `null`

#### Scenario: Middleware sets userId to null when claim value is empty

- **GIVEN** a backend service has registered `LoggerMiddleware` in its pipeline
- **AND** a client sends an authenticated HTTP request with a JWT bearer token
- **AND** the JWT contains a `sub` claim with an empty string value
- **WHEN** the request completes
- **THEN** `LoggerMiddleware` SHALL publish a `LogEventMessage` with `userId` set to `null`

---

### Requirement: Logger Client shall remain independent from Logger API, Infrastructure, Common, and SampleService

The `Skysim.Logger.Client` project SHALL NOT have any project references to `Skysim.Logger.Api`, `Skysim.Logger.Infrastructure`, `Skysim.Logger.Common`, or any sample service. The library SHALL be usable by any backend microservice that references only `Skysim.Logger.Contracts`.

#### Scenario: Logger Client has no dependency on Logger.Api

- **GIVEN** the project file `Skysim.Logger.Client.csproj` is examined
- **WHEN** all `<ProjectReference>` elements are listed
- **THEN** there SHALL be no reference to `Skysim.Logger.Api`

#### Scenario: Logger Client has no dependency on Logger.Infrastructure

- **GIVEN** the project file `Skysim.Logger.Client.csproj` is examined
- **WHEN** all `<ProjectReference>` elements are listed
- **THEN** there SHALL be no reference to `Skysim.Logger.Infrastructure`

#### Scenario: Logger Client has no dependency on Logger.Common

- **GIVEN** the project file `Skysim.Logger.Client.csproj` is examined
- **WHEN** all `<ProjectReference>` elements are listed
- **THEN** there SHALL be no reference to `Skysim.Logger.Common`

#### Scenario: Logger Client references only Contracts

- **GIVEN** the project file `Skysim.Logger.Client.csproj` is examined
- **WHEN** all `<ProjectReference>` elements are listed
- **THEN** there SHALL be exactly one reference to `Skysim.Logger.Contracts`

#### Scenario: Logger Client uses FrameworkReference for ASP.NET Core types

- **GIVEN** the project file `Skysim.Logger.Client.csproj` is examined
- **WHEN** the `<FrameworkReference>` elements are listed
- **THEN** there SHALL be `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
- **AND** there SHALL be no direct NuGet package references for ASP.NET Core types
