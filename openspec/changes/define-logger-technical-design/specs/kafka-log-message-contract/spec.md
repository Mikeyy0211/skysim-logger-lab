# Capability: Kafka Log Message Contract

## ADDED Requirements

### Requirement: All business action events are published to a single topic with a deterministic key
The Kafka topic for business action events SHALL be `skysim.action.logs`. The message key SHALL be `flowId` as a UTF-8 string so that all events of one flow land on the same partition and are processed in order.

#### Scenario: Producer uses flowId as the message key
- **WHEN** a service publishes a business action event
- **THEN** it sets the Kafka record key to the `flowId` of the running flow

#### Scenario: Topic name is fixed
- **WHEN** the Logger consumer subscribes
- **THEN** it subscribes to `skysim.action.logs` and no other topic

### Requirement: The message value is a JSON document with required and optional fields
The message value SHALL be a JSON object. The required fields are `eventId`, `flowId`, `flowType`, `serviceName`, `actionType`, `status`, `createdAt`. The optional fields are `checkoutType`, `userId`, `customerEmail`, `customerPhone`, `orderId`, `paymentId`, `message`, `requestTime`, `responseTime`, `duration`, `requestData`, `responseData`, `errorCode`, `errorMessage`, `exception`, `correlationId`.

#### Scenario: Required fields are validated by the consumer
- **WHEN** a Kafka message arrives missing one of `eventId`, `flowId`, `flowType`, `serviceName`, `actionType`, `status`, or `createdAt`
- **THEN** the consumer rejects it as invalid and publishes it to `skysim.action.logs.dlq` with a `failure_reason=validation_error` header, then commits the offset

#### Scenario: Optional fields may be null
- **WHEN** a GUEST checkout emits a message with no `userId`
- **THEN** the consumer accepts the message and persists `userId` as NULL

### Requirement: Status values are restricted to a closed set
The `status` field SHALL be one of `SUCCESS`, `FAILED`, `IN_PROGRESS`. Any other value SHALL be treated as a validation error.

#### Scenario: Known status is accepted
- **WHEN** `status` is `SUCCESS`
- **THEN** the consumer accepts and persists the message

#### Scenario: Unknown status is rejected
- **WHEN** `status` is `WEIRD`
- **THEN** the consumer sends the message to DLQ with `failure_reason=invalid_status`

### Requirement: Action types follow the canonical list
The `actionType` field SHALL be one of `ORDER_CREATED`, `PAYMENT_REQUESTED`, `PAYMENT_SUCCESS`, `PROVIDER_REQUESTED`, `ESIM_ACTIVATED`, `EMAIL_SENT`, `ORDER_FAILED`, `PAYMENT_FAILED`, `PROVIDER_FAILED`, `ESIM_ACTIVATION_FAILED`, `EMAIL_FAILED`. New types may be added later but require a schema version bump.

#### Scenario: Known action type is accepted
- **WHEN** `actionType` is `PAYMENT_SUCCESS`
- **THEN** the consumer persists it as-is

#### Scenario: Unknown action type is rejected
- **WHEN** `actionType` is `FOO_BAR`
- **THEN** the consumer sends the message to DLQ with `failure_reason=invalid_action_type`

### Requirement: Timestamps use ISO-8601 UTC
`createdAt`, `requestTime`, and `responseTime` SHALL be ISO-8601 UTC strings (e.g. `2026-06-22T07:00:00.000Z`). The consumer SHALL normalize them to UTC `timestamp with time zone` on insert.

#### Scenario: Timestamp is normalized
- **WHEN** a producer sends `createdAt = "2026-06-22T07:00:00.000Z"`
- **THEN** it is stored as a UTC `timestamptz` value

#### Scenario: Non-UTC string is rejected
- **WHEN** `createdAt` is `"22/06/2026 07:00"`
- **THEN** the message is sent to DLQ with `failure_reason=invalid_timestamp`

### Requirement: Sensitive fields must be masked before publication
Producers SHALL mask values for keys `password`, `access_token`, `refresh_token`, `authorization`, `otp`, `cardNumber`, `cvv`, `paymentSecret`, `secret`, `token` by replacing them with `"***"` in any JSON subtree of `requestData`, `responseData`, and `errorPayload`.

#### Scenario: Producer masks a token
- **WHEN** a producer serializes `requestData = { "headers": { "authorization": "Bearer ey..." } }`
- **THEN** the published JSON contains `"authorization": "***"`

#### Scenario: Consumer re-masks if a producer forgot
- **WHEN** a message arrives with raw sensitive values
- **THEN** the consumer applies masking before persistence and before DLQ publish
