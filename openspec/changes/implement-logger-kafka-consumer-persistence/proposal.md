## Why

The approved Logger technical design (`define-logger-technical-design`) defines a complete Kafka consumer pipeline, 3-table PostgreSQL schema, and persistence semantics. Phase 2 backend implementation must now convert that design into runnable code: a .NET 8 BackgroundService that consumes `skysim.action.logs`, validates and masks every message, persists to `log_flows` / `log_actions` / `log_action_details` under a single DB transaction, and commits Kafka offsets only after the DB commit succeeds. This is the foundational ingestion path — nothing else (Query API, Middleware Logging, Frontend) can work without it.

## What Changes

- New `LogEventMessage` DTO mirroring the Kafka contract with all required and optional fields, plus enum properties for `FlowType`, `CheckoutType`, `Status`, and `DetailType`.
- `KafkaLogConsumerService`: a .NET 8 `BackgroundService` that polls `skysim.action.logs` under consumer group `skysim-logger-consumer`, processes one message at a time per partition, and uses manual offset commits.
- Message validation: required-field presence, enum value validation (flowType, checkoutType, status), timestamp format, and `eventId` non-empty check. Invalid messages go straight to `skysim.action.logs.dlq` without retry.
- Sensitive-data masker: recursive JSON traversal that replaces values under deny-list keys with `"***"` before persistence.
- Idempotency: `eventId` uniqueness enforced by a UNIQUE constraint on `log_actions.event_id`. Duplicate messages are skipped with an idempotent log entry and offset is still committed.
- PostgreSQL persistence with EF Core: `log_flows` upserted by `flow_id`, `log_actions` inserted per event, `log_action_details` upserted per action — all inside one `DbContext.SaveChangesAsync()` call.
- Retry with bounded exponential backoff (5 attempts) for transient DB/broker failures. After N retries exhausted, message is published to `skysim.action.logs.dlq` with failure headers, then offset is committed.
- EF Core entities for `LogFlow`, `LogAction`, `LogActionDetail` with proper column mapping, indexes, and relationships.
- Basic unit tests covering: required-field validation, enum validation, idempotency behavior, masker, and persistence (upsert/insert paths).

## Capabilities

### New Capabilities

- `kafka-log-consumer`: Implements the approved 8-step consumer pipeline (parse → validate → mask → idempotency check → upsert flow → insert action → upsert details → commit offset) as a `BackgroundService`. Handles DLQ routing, bounded retry, and manual offset commit. Consumer group: `skysim-logger-consumer`.
- `log-event-dto`: Defines `LogEventMessage` as the canonical C# representation of the Kafka message contract. Includes `FlowType`, `CheckoutType`, `Status`, and `DetailType` enums with string serialization.
- `log-flow-persistence`: EF Core entity and repository logic for `log_flows` (upsert by `flow_id`, progress counters, last-action tracking, terminal status detection).
- `log-action-persistence`: EF Core entity and repository logic for `log_actions` (insert per event, step order assignment, `event_id` uniqueness).
- `log-action-detail-persistence`: EF Core entity and repository logic for `log_action_details` (upsert by `action_id`, JSONB payload storage for request, response, error, and metadata. Payload truncation is deferred to a later phase..
- `sensitive-data-masker`: Recursive JSON masker with deny-list covering `password`, `access_token`, `refresh_token`, `authorization`, `otp`, `cardNumber`, `cvv`, `paymentSecret`, `secret`, `token`.
- `consumer-retry-dlq`: Retry policy (5 attempts, exponential backoff 200ms–3200ms) and DLQ producer for `skysim.action.logs.dlq` with `failure_reason`, `failed_at`, `consumer_attempt` headers.
- `kafka-consumer-tests`: xUnit test suite covering validation, idempotency, masking, and persistence happy/edge paths.

### Modified Capabilities

_(None — no existing capability requirements change in this phase.)_

## Impact

**Backend (.NET 8 / ASP.NET Core):**

- New files: DTOs (`LogEventMessage`, enums), EF Core entities (`LogFlow`, `LogAction`, `LogActionDetail`), consumer service (`KafkaLogConsumerService`), masker (`SensitiveDataMasker`), retry/DLQ helpers, repository layer, `appsettings.json` additions for Kafka/DB connection.
- Modified files: `Program.cs` (DI registration for DbContext, hosted service, masker), `Skysim.Logger.sln` (add new projects if separated).
- Out of scope: Controllers, Middleware logging, Kafka Producer, Query API endpoints.

**Database (PostgreSQL):**

- New tables: `log_flows`, `log_actions`, `log_action_details` with all columns, indexes, and constraints defined in `logger-database-design/spec.md`.
- The EF Core migration scripts are the deliverable — applied via `dotnet ef database update` during local dev.

**Kafka:**

- Reads from: `skysim.action.logs` (topic must exist; provisioned by `infra/docker-compose.yml`).
- Writes to: `skysim.action.logs.dlq` (dead-letter topic; provisioned alongside main topic).

**Testing:**

- xUnit project with tests for: `LogEventMessageValidator`, `SensitiveDataMasker`, `KafkaLogConsumerService` (happy path + idempotency + validation failure), and repository/fixture tests against an in-memory or test-container PostgreSQL.

**Not affected in this change:**

- Middleware/Action Filter logging.
- Kafka Producer.
- Logger Query API (GET /api/log-flows, GET /api/log-flows/{flowId}, GET /api/log-actions/{actionId}).
- Frontend ReactJS components.
