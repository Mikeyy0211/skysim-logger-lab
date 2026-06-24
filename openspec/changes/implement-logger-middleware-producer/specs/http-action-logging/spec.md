## ADDED Requirements

### Requirement: HTTP Request/Response Capture

The system SHALL capture the following fields from every incoming HTTP request and make them available to the logging pipeline: HTTP method, request path (raw), query string, status code, request timestamp, response timestamp, duration in milliseconds, and correlation ID.

#### Scenario: Middleware captures request metadata
- **WHEN** a `GET /api/log-flows?status=Success` request arrives
- **THEN** the middleware records method=`GET`, path=`/api/log-flows`, queryString=`?status=Success`, and sets `requestTime` to the current UTC timestamp before passing control to the next middleware

#### Scenario: Middleware captures response status code and duration
- **WHEN** a request is processed and a response is generated with status code `200`
- **THEN** the middleware records statusCode=`200`, sets `responseTime` to the current UTC timestamp, computes `duration` as the elapsed milliseconds since `requestTime`, and these values are available to the logging pipeline

### Requirement: Request Body Buffering

The system SHALL make the request body available for reading multiple times within the same request pipeline by buffering it after the first read.

#### Scenario: Request body is readable after EnableBuffering
- **WHEN** a `POST /api/orders` request with a JSON body arrives
- **THEN** the `RequestBodyBufferingMiddleware` enables request body buffering, the body stream is seekable back to position 0, and downstream middleware or controllers can read the body

### Requirement: Response Body Capture

The system SHALL capture the response body bytes written during request processing so they can be included in the log event.

#### Scenario: Response body is captured without corrupting the response
- **WHEN** an endpoint writes a JSON response body
- **THEN** the middleware response wrapper copies the bytes to an internal buffer, the original response stream still delivers the bytes to the client unchanged, and the buffered copy is available for logging after the next middleware completes

### Requirement: Sensitive Data Masking

The system SHALL mask sensitive fields in the request body, response body, and error payload before publishing the log event, using the existing `SensitiveDataMasker`.

#### Scenario: Password field is masked in request body
- **WHEN** a request body contains `{ "email": "user@test.com", "password": "secret123" }`
- **THEN** after masking, the logged `requestData` contains `{ "email": "user@test.com", "password": "***" }`

#### Scenario: Access token is masked in response body
- **WHEN** a response body contains `{ "access_token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." }`
- **THEN** after masking, the logged `responseData` contains `{ "access_token": "***" }`

### Requirement: Correlation ID Handling

The system SHALL read the correlation ID from the `X-Correlation-ID` request header. If absent, it SHALL read from `X-Request-ID`. If neither is present, it SHALL generate a new `Guid` and add it to the response headers as `X-Correlation-ID`.

#### Scenario: Existing correlation ID is preserved
- **WHEN** a request arrives with header `X-Correlation-ID: abc-123`
- **THEN** the log event's `correlationId` is set to `abc-123`

#### Scenario: Correlation ID is generated when missing
- **WHEN** a request arrives with no `X-Correlation-ID` and no `X-Request-ID` header
- **THEN** a new `Guid` is generated, stored as `correlationId` in the log event, and added to the response headers as `X-Correlation-ID`

### Requirement: Failure Isolation

The system SHALL ensure that Kafka publish failures do not affect the HTTP response. When publishing fails, the error SHALL be logged and the request SHALL continue normally.

#### Scenario: Publish failure does not return HTTP error
- **WHEN** the Kafka broker is unreachable and a publish attempt fails
- **THEN** the middleware catches the exception, logs it at `Warning` level, and the HTTP response is returned to the client unchanged

### Requirement: Log Event Message Construction

The system SHALL build a `LogEventMessage` from the captured HTTP context with `flowType = FlowType.HttpAction`, `actionType = ActionType.HttpRequest`, and `serviceName` from configuration. Status SHALL be `Success` when the HTTP status code is in the 2xx range and `Failed` otherwise.

#### Scenario: Successful request produces Success status
- **WHEN** a request returns HTTP status `200 OK`
- **THEN** the log event `status` is set to `Success`

#### Scenario: Error request produces Failed status
- **WHEN** a request returns HTTP status `500 Internal Server Error`
- **THEN** the log event `status` is set to `Failed` and the `errorCode` is set to `500`

### Requirement: Async Fire-and-Forget Publishing

The system SHALL publish the log event asynchronously without awaiting the delivery confirmation before returning from the middleware. The HTTP pipeline SHALL NOT be blocked by Kafka delivery latency.

#### Scenario: HTTP response returns before Kafka delivery
- **WHEN** `LoggerMiddleware` invokes `producer.PublishAsync`
- **THEN** the middleware does not `await` the delivery confirmation before passing control back to the next middleware, so HTTP response time is unaffected by Kafka round-trip time
