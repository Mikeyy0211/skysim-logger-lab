# Capability: Kafka Log Consumer Flow

## ADDED Requirements

### Requirement: The consumer runs as a .NET 8 BackgroundService inside the Logger API process
The Kafka consumer SHALL run as an `IHostedService`/`BackgroundService` registered in the same ASP.NET Core process as the Logger API in phase 1. It SHALL subscribe to `skysim.action.logs` under consumer group `skysim-logger-consumer`.

#### Scenario: Consumer starts with the host
- **WHEN** the Logger API host starts
- **THEN** the consumer service starts, joins the consumer group, and begins polling `skysim.action.logs`

#### Scenario: Consumer is single-threaded per partition
- **WHEN** the consumer reads messages from partition P
- **THEN** it processes one message at a time on the partition's dedicated task

### Requirement: The consumer follows a strict processing pipeline per message
For each Kafka message the consumer SHALL execute the steps in this exact order: (1) parse JSON, (2) validate required fields, (3) apply sensitive-data masking, (4) check idempotency by `eventId`, (5) upsert `log_flows`, (6) insert `log_actions`, (7) upsert `log_action_details`, (8) commit the Kafka offset.

#### Scenario: Happy path persists all three rows
- **WHEN** a valid new message arrives
- **THEN** the consumer creates/updates one `log_flows` row, inserts one `log_actions` row, upserts one `log_action_details` row, and commits the offset

#### Scenario: Validation failure short-circuits to DLQ
- **WHEN** a message fails step 2
- **THEN** steps 3–8 are skipped, the message is published to `skysim.action.logs.dlq`, and the offset is committed

### Requirement: Idempotency uses eventId as a unique key
The consumer SHALL treat `log_actions.event_id` as the unique idempotency key. If a row with the same `eventId` already exists, the consumer SHALL skip persistence and only commit the offset.

#### Scenario: Duplicate message is dropped
- **WHEN** the consumer receives a message whose `eventId` already exists in `log_actions`
- **THEN** it logs an `idempotent_skip` event, does not re-insert, and commits the offset

#### Scenario: Unique constraint enforces idempotency at the database
- **WHEN** the consumer attempts to insert a duplicate `eventId` (race condition)
- **THEN** the database rejects it with a unique-violation error which the consumer treats as an idempotent skip

### Requirement: Kafka offset is committed only after a successful database commit
The consumer SHALL use manual offset commits. The commit SHALL happen only after the database transaction containing the `log_actions` insert has committed successfully.

#### Scenario: Successful DB commit leads to offset commit
- **WHEN** the database transaction commits
- **THEN** the consumer stores the offset in Kafka with `enable.auto.commit=false`

#### Scenario: DB failure leads to no offset commit
- **WHEN** the database transaction fails (e.g. connection drop)
- **THEN** the consumer does NOT commit; on next poll the broker redelivers the message

### Requirement: Transient failures trigger bounded retry; permanent failures go to DLQ
The consumer SHALL retry transient errors (database connection, broker unavailability) up to N=5 times with exponential backoff (200ms, 400ms, 800ms, 1600ms, 3200ms). After N retries, the consumer SHALL publish the original message to `skysim.action.logs.dlq` with a `failure_reason` header and commit the offset. Validation errors SHALL skip retry and go directly to DLQ.

#### Scenario: Transient DB error is retried
- **WHEN** the database throws a connection exception on insert
- **THEN** the consumer retries up to 5 times with exponential backoff before giving up

#### Scenario: Exhausted retries go to DLQ
- **WHEN** all 5 retries fail
- **THEN** the original message is published to `skysim.action.logs.dlq` with header `failure_reason=db_unavailable` and the offset is committed

#### Scenario: Validation error skips retry
- **WHEN** a message has an unknown `status`
- **THEN** it is published to DLQ immediately without retrying

### Requirement: DLQ messages retain original key, payload, and failure metadata
Messages published to `skysim.action.logs.dlq` SHALL preserve the original message key, the original JSON payload (after masking), and Kafka headers `failure_reason`, `failed_at`, and `consumer_attempt`.

#### Scenario: DLQ headers are populated
- **WHEN** a message is dead-lettered
- **THEN** it carries headers `failure_reason=<reason>`, `failed_at=<UTC ISO-8601>`, `consumer_attempt=<integer>`

#### Scenario: DLQ payload is masked
- **WHEN** a message containing a raw `access_token` is dead-lettered
- **THEN** the DLQ payload contains `"access_token": "***"`
