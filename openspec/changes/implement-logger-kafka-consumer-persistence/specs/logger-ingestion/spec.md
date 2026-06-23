# logger-ingestion Specification Delta

## ADDED Requirements

### Requirement: Kafka log consumer shall consume action log events

The Logger Service SHALL run a .NET 8 `BackgroundService` that consumes Kafka messages from topic `skysim.action.logs` under consumer group `skysim-logger-consumer`.

#### Scenario: Consumer receives a valid log event
- **GIVEN** Kafka topic `skysim.action.logs` contains a valid log event message
- **WHEN** the Logger consumer polls the topic
- **THEN** the consumer SHALL deserialize the message into `LogEventMessage`
- **AND** continue the processing pipeline without crashing

#### Scenario: Consumer uses manual offset commit
- **GIVEN** the consumer receives a Kafka message
- **WHEN** the message is processed successfully and persisted to PostgreSQL
- **THEN** the consumer SHALL commit the Kafka offset manually
- **AND** the consumer SHALL NOT rely on Kafka auto-commit

---

### Requirement: Log event message shall follow the approved Kafka contract

The system SHALL define `LogEventMessage` as the canonical DTO for Kafka log events.

The DTO SHALL include required fields:
- `eventId`
- `flowId`
- `flowType`
- `serviceName`
- `actionType`
- `status`
- `createdAt`

The DTO MAY include optional fields:
- `checkoutType`
- `userId`
- `customerEmail`
- `customerPhone`
- `orderId`
- `paymentId`
- `message`
- `requestTime`
- `responseTime`
- `duration`
- `requestData`
- `responseData`
- `errorCode`
- `errorMessage`
- `exception`
- `correlationId`

#### Scenario: Valid message passes validation
- **GIVEN** a log event contains all required fields with valid values
- **WHEN** the validator checks the message
- **THEN** validation SHALL succeed

#### Scenario: Missing required field fails validation
- **GIVEN** a log event is missing `eventId`, `flowId`, `serviceName`, `actionType`, `status`, or `createdAt`
- **WHEN** the validator checks the message
- **THEN** validation SHALL fail
- **AND** the message SHALL be routed to the dead-letter topic

---

### Requirement: Flow identifier shall be a string correlation identifier

The system SHALL treat `flowId` as a non-empty string correlation identifier with maximum length 100.

The system SHALL NOT require `flowId` to be a GUID.

#### Scenario: Flow ID is a business correlation string
- **GIVEN** a log event has `flowId` value `test-flow-id-001`
- **WHEN** the validator checks the message
- **THEN** validation SHALL succeed if all other required fields are valid

#### Scenario: Flow ID is missing
- **GIVEN** a log event has an empty `flowId`
- **WHEN** the validator checks the message
- **THEN** validation SHALL fail

---

### Requirement: Event identifier shall enforce idempotency

The system SHALL use `eventId` to prevent duplicate Kafka message processing.

The `log_actions.event_id` column SHALL have a unique constraint.

#### Scenario: First message with event ID is processed
- **GIVEN** no `log_actions` row exists for `eventId`
- **WHEN** the consumer processes the message
- **THEN** the system SHALL persist the log action
- **AND** commit the Kafka offset after the database transaction succeeds

#### Scenario: Duplicate message with same event ID is skipped
- **GIVEN** a `log_actions` row already exists for `eventId`
- **WHEN** Kafka redelivers the same message
- **THEN** the consumer SHALL skip persistence
- **AND** commit the Kafka offset
- **AND** log the outcome as `idempotent_skip`

---

### Requirement: Logger persistence shall use three PostgreSQL tables

The system SHALL persist consumed log events into three PostgreSQL tables:
- `log_flows`
- `log_actions`
- `log_action_details`

The system SHALL use snake_case table and column names.

The system SHALL store `flow_id` as `varchar(100)`.

The system SHALL store `event_id` as `uuid`.

#### Scenario: Valid log event creates flow and action records
- **GIVEN** a valid log event is consumed
- **WHEN** the persistence pipeline runs
- **THEN** the system SHALL upsert one `log_flows` row by `flow_id`
- **AND** insert one `log_actions` row for the event
- **AND** insert or update one `log_action_details` row when payload data exists

#### Scenario: Flow summary is updated
- **GIVEN** a log flow already exists
- **WHEN** a new action for the same `flowId` is persisted
- **THEN** the system SHALL update `total_steps`
- **AND** update `success_steps` or `failed_steps`
- **AND** update `last_action_type`
- **AND** update `last_message`
- **AND** update `updated_at`

---

### Requirement: Database transaction shall be atomic per consumed message

The system SHALL persist each consumed Kafka message inside a single database transaction.

The system SHALL commit the Kafka offset only after the database transaction succeeds.

#### Scenario: Database transaction succeeds
- **GIVEN** a valid Kafka message is consumed
- **WHEN** `log_flows`, `log_actions`, and `log_action_details` are persisted successfully
- **THEN** the database transaction SHALL commit
- **AND** the Kafka offset SHALL be committed

#### Scenario: Database transaction fails
- **GIVEN** a valid Kafka message is consumed
- **WHEN** persistence fails before transaction commit
- **THEN** the database transaction SHALL roll back
- **AND** the Kafka offset SHALL NOT be committed before retry or DLQ handling

---

### Requirement: Sensitive data shall be masked before persistence

The system SHALL recursively mask sensitive fields before storing request, response, error, or metadata payloads.

The deny-list SHALL include:
- `password`
- `access_token`
- `refresh_token`
- `authorization`
- `otp`
- `cardNumber`
- `cvv`
- `paymentSecret`
- `secret`
- `token`

Sensitive values SHALL be replaced with `"***"`.

#### Scenario: Sensitive field exists at top level
- **GIVEN** payload contains `password`
- **WHEN** the sensitive data masker processes the payload
- **THEN** the `password` value SHALL be replaced with `"***"`

#### Scenario: Sensitive field exists in nested object
- **GIVEN** payload contains nested `authorization` or `token`
- **WHEN** the sensitive data masker processes the payload
- **THEN** the sensitive value SHALL be replaced with `"***"`
- **AND** non-sensitive fields SHALL remain unchanged

---

### Requirement: Invalid messages shall be routed to DLQ

The system SHALL route invalid Kafka messages to topic `skysim.action.logs.dlq`.

Invalid messages SHALL NOT be persisted into PostgreSQL.

#### Scenario: Message cannot be parsed as JSON
- **GIVEN** Kafka contains a malformed JSON message
- **WHEN** the consumer attempts to deserialize the message
- **THEN** the consumer SHALL publish the original message to `skysim.action.logs.dlq`
- **AND** include failure reason `parse_error`
- **AND** commit the Kafka offset after DLQ publish succeeds

#### Scenario: Message fails validation
- **GIVEN** Kafka contains a message with missing required fields or invalid enum values
- **WHEN** validation fails
- **THEN** the consumer SHALL publish the message to `skysim.action.logs.dlq`
- **AND** include the validation failure reason
- **AND** SHALL NOT insert rows into PostgreSQL

---

### Requirement: Transient failures shall use bounded retry

The system SHALL retry transient database or Kafka broker failures with bounded exponential backoff.

The retry policy SHALL use:
- maximum attempts: 5
- initial delay: 200ms
- backoff multiplier: 2.0
- maximum delay: 3200ms

#### Scenario: Transient database failure recovers
- **GIVEN** PostgreSQL temporarily fails during persistence
- **WHEN** the retry policy retries the operation
- **THEN** the system SHALL retry up to the configured maximum attempts
- **AND** persist the message if a retry succeeds
- **AND** commit the Kafka offset only after successful persistence

#### Scenario: Transient failure exhausts retries
- **GIVEN** persistence keeps failing after all retry attempts
- **WHEN** the retry policy is exhausted
- **THEN** the system SHALL publish the message to `skysim.action.logs.dlq`
- **AND** include a failure reason
- **AND** commit the Kafka offset after DLQ publish succeeds

---

### Requirement: This change shall exclude middleware, query API, and frontend

This change SHALL NOT implement Middleware Logging, Kafka Producer for business services, Logger Query API, authentication, or ReactJS frontend.

#### Scenario: Implementation stays within ingestion scope
- **GIVEN** this change is applied
- **WHEN** generated tasks are implemented
- **THEN** no controller endpoints for log search SHALL be added
- **AND** no reusable HTTP logging middleware SHALL be added
- **AND** no ReactJS frontend files SHALL be added
```
