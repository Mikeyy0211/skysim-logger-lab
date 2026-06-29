# Logger API Responsibilities Specification

## Purpose
Define the explicit responsibilities, boundaries, and structural organization of `Skysim.Logger.Api` as the server-side Logger Service.

## ADDED Requirements

### Requirement: Logger API shall own server-side Kafka consumption

The `Skysim.Logger.Api` project SHALL contain the Kafka consumer service that consumes log events from the `skysim.action.logs` topic.

#### Scenario: Kafka consumer is located in Consumers folder

- **GIVEN** the file structure of `Skysim.Logger.Api` is examined
- **WHEN** the `KafkaLogConsumerService.cs` file is located
- **THEN** it SHALL be located in the `Consumers/` folder

#### Scenario: Kafka consumer reads from skysim.action.logs topic

- **GIVEN** the `KafkaLogConsumerService` is configured
- **WHEN** messages arrive on the `skysim.action.logs` Kafka topic
- **THEN** the consumer SHALL process each message according to its idempotency and persistence rules

#### Scenario: Kafka consumer commits offset only after successful persistence

- **GIVEN** a valid `LogEventMessage` is consumed from Kafka
- **WHEN** the message is successfully persisted to PostgreSQL
- **THEN** the consumer SHALL commit the Kafka offset only after successful persistence

---

### Requirement: Logger API shall validate log events before persistence

The `Skysim.Logger.Api` project SHALL contain validators to ensure incoming log events meet schema and business rules.

#### Scenario: LogEventMessageValidator validates required fields

- **GIVEN** a `LogEventMessage` is received from Kafka
- **WHEN** validation runs
- **THEN** the validator SHALL check that `eventId`, `flowId`, `actionType`, `status`, and `createdAt` are present and valid

#### Scenario: Invalid messages are sent to DLQ

- **GIVEN** a `LogEventMessage` fails validation
- **WHEN** the validation completes
- **THEN** the invalid message SHALL be published to the `skysim.action.logs.dlq` topic

---

### Requirement: Logger API shall persist logs to PostgreSQL

The `Skysim.Logger.Api` project SHALL orchestrate persistence of log flows, log actions, and log action details to PostgreSQL.

#### Scenario: Consumer persists log flow

- **GIVEN** a valid `LogEventMessage` is consumed
- **WHEN** the message represents a new flow
- **THEN** the API SHALL create a new `LogFlow` record in the database

#### Scenario: Consumer persists log action

- **GIVEN** a valid `LogEventMessage` is consumed
- **WHEN** the message is processed
- **THEN** the API SHALL create a new `LogAction` record linked to the flow

#### Scenario: Consumer persists action details

- **GIVEN** a valid `LogEventMessage` contains request/response data
- **WHEN** the message is processed
- **THEN** the API SHALL create a `LogActionDetail` record with the payloads

#### Scenario: Consumer handles duplicate events idempotently

- **GIVEN** a `LogEventMessage` with an `eventId` that already exists in the database
- **WHEN** the message is consumed
- **THEN** the API SHALL skip persistence without error (idempotency)

---

### Requirement: Logger API shall publish failed messages to DLQ

The `Skysim.Logger.Api` project SHALL publish unprocessable messages to a dead-letter topic for investigation.

#### Scenario: DLQ publisher is located in Kafka folder

- **GIVEN** the file structure of `Skysim.Logger.Api` is examined
- **WHEN** the `DlqPublisher.cs` file is located
- **THEN** it SHALL be located in the `Kafka/` folder

#### Scenario: Failed messages are published to skysim.action.logs.dlq

- **GIVEN** a message fails processing (validation, transient error, or max retries exceeded)
- **WHEN** the failure is recorded
- **THEN** the `DlqPublisher` SHALL publish the message to the `skysim.action.logs.dlq` topic

---

### Requirement: Logger API shall expose query APIs for log flows and actions

The `Skysim.Logger.Api` project SHALL provide REST APIs for querying log flows and log actions.

#### Scenario: GET /api/log-flows returns paginated flows

- **GIVEN** the `LogFlowsController` is implemented
- **WHEN** a client sends `GET /api/log-flows` with pagination parameters
- **THEN** the API SHALL return a paginated list of `LogFlowSummaryDto`

#### Scenario: GET /api/log-flows/{flowId} returns flow with actions

- **GIVEN** the `LogFlowsController` is implemented
- **WHEN** a client sends `GET /api/log-flows/{flowId}`
- **THEN** the API SHALL return a `LogFlowDetailDto` including all associated `LogActionDto`

#### Scenario: GET /api/log-actions/{actionId} returns action details

- **GIVEN** the `LogActionsController` is implemented
- **WHEN** a client sends `GET /api/log-actions/{actionId}`
- **THEN** the API SHALL return a `LogActionDetailsDto` including request/response payloads

#### Scenario: Query parameters filter flows correctly

- **GIVEN** the `LogFlowsController` is implemented
- **WHEN** a client sends `GET /api/log-flows?customerEmail=test@example.com&status=SUCCESS`
- **THEN** the API SHALL filter results by the provided query parameters

---

### Requirement: Logger API shall not contain client-side logging implementation files

The `Skysim.Logger.Api` project SHALL NOT contain implementation files for middleware, Kafka producers, or sensitive data maskers used by client services. These implementations belong to `Skysim.Logger.Client`.

**Note:** The Api project may temporarily reference `Skysim.Logger.Client` if Program.cs uses Client services (e.g., `LoggerMiddleware`, `ISensitiveDataMasker`, `IKafkaLogProducer`). However, the Api project must not contain the source files for these components.

#### Scenario: LoggerMiddleware source is not in Api project

- **GIVEN** the project structure of `Skysim.Logger.Api` is examined
- **WHEN** the file system is searched for `LoggerMiddleware.cs`
- **THEN** no `LoggerMiddleware.cs` SHALL exist in the `Skysim.Logger.Api` project
- **AND** `LoggerMiddleware.cs` SHALL exist only in `Skysim.Logger.Client`

#### Scenario: KafkaLogProducer source is not in Api project

- **GIVEN** the project structure of `Skysim.Logger.Api` is examined
- **WHEN** the file system is searched for `KafkaLogProducer.cs`
- **THEN** no `KafkaLogProducer.cs` SHALL exist in the `Skysim.Logger.Api` project
- **AND** `KafkaLogProducer.cs` SHALL exist only in `Skysim.Logger.Client`

#### Scenario: SensitiveDataMasker source is not in Api project

- **GIVEN** the project structure of `Skysim.Logger.Api` is examined
- **WHEN** the file system is searched for `SensitiveDataMasker.cs`
- **THEN** no `SensitiveDataMasker.cs` SHALL exist in the `Skysim.Logger.Api` project
- **AND** `SensitiveDataMasker.cs` SHALL exist only in `Skysim.Logger.Client`

---

### Requirement: Logger API cleanup shall preserve public API routes

The `Skysim.Logger.Api` project SHALL maintain the same public API routes after cleanup.

#### Scenario: LogFlowsController routes are unchanged

- **GIVEN** the `LogFlowsController` is implemented
- **WHEN** the controller routes are examined
- **THEN** the routes SHALL be `GET /api/log-flows` and `GET /api/log-flows/{flowId}`

#### Scenario: LogActionsController routes are unchanged

- **GIVEN** the `LogActionsController` is implemented
- **WHEN** the controller routes are examined
- **THEN** the route SHALL be `GET /api/log-actions/{actionId}/details`

#### Scenario: API response DTOs remain unchanged

- **GIVEN** the API response models are examined
- **WHEN** responses from `GET /api/log-flows`, `GET /api/log-flows/{flowId}`, and `GET /api/log-actions/{actionId}` are validated
- **THEN** the response shapes SHALL match the existing DTO definitions

---

### Requirement: Logger API shall have clean project dependencies

The `Skysim.Logger.Api` project SHALL reference only the necessary projects.

#### Scenario: Api references Contracts

- **GIVEN** the `Skysim.Logger.Api.csproj` is examined
- **WHEN** the project references are listed
- **THEN** there SHALL be a reference to `Skysim.Logger.Contracts`

#### Scenario: Api references Infrastructure

- **GIVEN** the `Skysim.Logger.Api.csproj` is examined
- **WHEN** the project references are listed
- **THEN** there SHALL be a reference to `Skysim.Logger.Infrastructure`

#### Scenario: Api may reference Client temporarily

- **GIVEN** the `Skysim.Logger.Api.csproj` is examined
- **WHEN** the project references are listed
- **THEN** a reference to `Skysim.Logger.Client` MAY exist if Program.cs uses Client services
- **AND** the source files for those services SHALL NOT exist in the Api project

#### Scenario: Api may reference Common temporarily

- **GIVEN** the `Skysim.Logger.Api.csproj` is examined
- **WHEN** the project references are listed
- **THEN** a reference to `Skysim.Logger.Common` MAY exist if server-side helpers are still used
- **AND** the reference SHALL be removed when all Common dependencies are migrated to Api

---

### Requirement: Logger API shall organize files by responsibility

The `Skysim.Logger.Api` project SHALL organize source files into folders that reflect their responsibilities.

#### Scenario: Consumer files are in Consumers folder

- **GIVEN** the folder structure is examined
- **WHEN** the `Consumers/` folder is located
- **THEN** it SHALL contain `KafkaLogConsumerService.cs`

#### Scenario: DLQ and Kafka options are in Kafka folder

- **GIVEN** the folder structure is examined
- **WHEN** the `Kafka/` folder is located
- **THEN** it SHALL contain `DlqPublisher.cs`, `KafkaConsumerOptions.cs`, and server-side Kafka helpers

#### Scenario: Validators are in Validators folder

- **GIVEN** the folder structure is examined
- **WHEN** the `Validators/` folder is located
- **THEN** it SHALL contain validator classes for request/response validation

#### Scenario: Query classes are in Contracts/Queries folder

- **GIVEN** the folder structure is examined
- **WHEN** the `Contracts/Queries/` folder is located
- **THEN** it SHALL contain query parameter classes like `LogFlowListQuery.cs` and `LogActionListQuery.cs`

#### Scenario: Response DTOs are in Contracts/DTOs folder

- **GIVEN** the folder structure is examined
- **WHEN** the `Contracts/DTOs/` folder is located
- **THEN** it SHALL contain response DTOs like `LogFlowSummaryDto.cs`, `LogFlowDetailDto.cs`, `LogActionDto.cs`, and `LogActionDetailsDto.cs`

---

### Requirement: Logger API shall keep DLQ and retry behavior unchanged

The `Skysim.Logger.Api` project SHALL maintain the same DLQ publishing and retry logic.

#### Scenario: DLQ topic name remains skysim.action.logs.dlq

- **GIVEN** the DLQ configuration is examined
- **WHEN** a failed message is published
- **THEN** the topic name SHALL be `skysim.action.logs.dlq`

#### Scenario: Retry policy uses exponential backoff

- **GIVEN** a transient failure occurs during message processing
- **WHEN** the retry policy is applied
- **THEN** the system SHALL retry with exponential backoff as configured in `RetryPolicyFactory`

#### Scenario: Messages exceeding max retries go to DLQ

- **GIVEN** a message has failed the maximum number of retry attempts
- **WHEN** the retry policy is exhausted
- **THEN** the message SHALL be published to the DLQ topic

---

### Requirement: Logger API shall keep persistence and idempotency behavior unchanged

The `Skysim.Logger.Api` project SHALL maintain the same persistence logic and idempotency guarantees.

#### Scenario: Duplicate events are detected by eventId

- **GIVEN** a `LogEventMessage` with `eventId` = "event-123" is processed successfully
- **WHEN** another message with the same `eventId` = "event-123" arrives
- **THEN** the second message SHALL be skipped without creating duplicate database records

#### Scenario: Flow statistics are updated correctly

- **GIVEN** a new action is added to an existing flow
- **WHEN** the action is persisted
- **THEN** the flow's `totalSteps`, `successSteps`, or `failedSteps` SHALL be updated accordingly
