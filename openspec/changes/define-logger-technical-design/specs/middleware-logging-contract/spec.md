# Capability: Middleware / Action Filter Logging Contract

## ADDED Requirements

### Requirement: Every Skysim service exposes a reusable logging middleware/filter
Every Skysim backend service SHALL register a shared HTTP logging middleware or ASP.NET Core action filter that emits a technical log entry for every inbound HTTP request, including requests served by the Logger API itself.

#### Scenario: Middleware captures the request
- **WHEN** an HTTP request enters any Skysim service
- **THEN** the middleware records `service`, `action` (route template or `controller.action`), `requestTime`, and a freshly-generated `correlationId` if the request does not carry one

#### Scenario: Middleware captures the response
- **WHEN** the service produces a response
- **THEN** the middleware records `responseTime`, `duration`, HTTP `statusCode`, and a serialized (masked) snapshot of `requestData` and `responseData`

### Requirement: Technical logs and business action logs are stored separately
Technical middleware logs SHALL NOT be persisted into `log_actions`. Business action logs SHALL be the only rows in `log_actions`. The two streams MAY be joined by `correlationId` and `flowId` for tracing.

#### Scenario: Business action log goes through Kafka
- **WHEN** a service completes a business step (e.g. `PAYMENT_SUCCESS`)
- **THEN** it publishes to `skysim.action.logs`; this is the only path into `log_actions`

#### Scenario: Technical middleware log stays in its own surface
- **WHEN** a service handles an HTTP request
- **THEN** it writes a technical log entry (in phase 1, this surface is captured by the Logger service's own middleware on its own routes; the contract is defined here so other services can adopt the same shape when added later)

### Requirement: Sensitive fields are masked in technical logs
The middleware SHALL apply the same sensitive-data masking rules as the Kafka consumer before any payload leaves the request boundary.

#### Scenario: Authorization header is masked
- **WHEN** a request arrives with `Authorization: Bearer ey...`
- **THEN** the recorded `requestData.headers.authorization` is `"***"`

#### Scenario: Nested password field is masked
- **WHEN** a request body is `{ "user": { "password": "hunter2" } }`
- **THEN** the recorded `requestData` shows `"password": "***"`

### Requirement: correlationId is propagated across services
The middleware SHALL accept `correlationId` from the incoming `X-Correlation-Id` header if present, otherwise generate a new one. The same `correlationId` SHALL be set on the outbound response header.

#### Scenario: Incoming correlationId is preserved
- **WHEN** a request arrives with `X-Correlation-Id: abc-123`
- **THEN** the technical log entry records `correlationId = "abc-123"` and the response header echoes `X-Correlation-Id: abc-123`

#### Scenario: Missing correlationId is generated
- **WHEN** a request arrives without `X-Correlation-Id`
- **THEN** the middleware generates a UUIDv4, records it on the log entry, and sets it on the response header

### Requirement: Exceptions are captured with stack and type
When the request pipeline throws, the middleware SHALL record the exception type, message, and stack trace into the technical log entry without breaking the response flow.

#### Scenario: Unhandled exception is logged
- **WHEN** a controller throws an unhandled exception
- **THEN** the middleware records `exception = { type, message, stackTrace }`, lets the global exception handler produce the user-facing response, and does not swallow the exception

### Requirement: Log entry fields are stable and well-typed
The middleware SHALL emit entries with the fields `service` (string), `action` (string), `requestTime` (UTC ISO-8601), `responseTime` (UTC ISO-8601), `duration` (integer milliseconds), `requestData` (object, masked), `responseData` (object, masked), `userId` (string|null), `exception` (object|null), `correlationId` (UUID string), and `statusCode` (integer).

#### Scenario: Field shape is stable
- **WHEN** a downstream consumer reads a technical log entry
- **THEN** the JSON shape contains every field above with the documented types and `null` for absent values
