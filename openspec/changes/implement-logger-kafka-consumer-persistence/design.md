# Design: implement-logger-kafka-consumer-persistence

## Context

The approved Logger technical design (`define-logger-technical-design`) establishes a Kafka consumer pipeline, a 3-table PostgreSQL schema, and persistence semantics. This change implements Phase 2: converting the design into runnable .NET 8 code. The solution currently contains only an empty ASP.NET Core API stub (`Skysim.Logger.Api`) with no domain, infrastructure, or consumer code. The `infra/docker-compose.yml` already provisions PostgreSQL 16 (`skysim_postgres_data`, port 5432) and Kafka (`skysim-kafka`, port 9092, topic auto-creation enabled). All conventions from the workspace rules are binding: snake_case DB columns, `eventId` idempotency, `flowId` correlation, manual offset commit after DB commit, and the 10-field sensitive-field deny-list.

**Current state:** `Skysim.Logger.sln` → `Skysim.Logger.Api` (stub). No DbContext, no consumer, no entities, no tests.

**Constraints:** Single-developer lab pace; no separate Worker Service (consumer lives in the API process); use EF Core for persistence; use `Confluent.Kafka` for the consumer; use `Polly` for retry; use xUnit for tests; no Query API, no Middleware Logging, no Kafka Producer in this phase.

**Stakeholders:** Backend developer (implements against this design), Mentor (reviews output).

---

## Goals / Non-Goals

**Goals:**

- Implement the 8-step Kafka consumer pipeline (`BackgroundService`) that reads `skysim.action.logs` under consumer group `skysim-logger-consumer`.
- Define `LogEventMessage` DTO with `FlowType`, `CheckoutType`, `Status`, and `DetailType` enums, plus all optional fields, matching the Kafka contract exactly.
- Implement validation (required fields, enum values, ISO-8601 timestamps) with clear DLQ routing for validation errors.
- Implement `SensitiveDataMasker` (recursive JSON traversal, deny-list of 10 fields, `"***"` replacement).
- Implement idempotency: UNIQUE constraint on `log_actions.event_id`, skip-and-commit for duplicates.
- Implement PostgreSQL persistence: `log_flows` upsert by `flow_id`, `log_actions` insert per event, `log_action_details` upsert by `action_id`, all inside one `DbContext.SaveChangesAsync()` transaction.
- Implement manual Kafka offset commit only after successful DB transaction.
- Implement bounded retry (5 attempts, exponential backoff 200ms–3200ms) for transient failures, DLQ publish after exhausted retries.
- Create EF Core entities and DbContext with all three tables, indexes, and column mappings.
- Add EF Core migration script for the 3-table schema.
- Add `appsettings.json` entries for Kafka consumer config, PostgreSQL connection (read from `ConnectionStrings:DefaultConnection`), retry config.
- Write unit tests covering: required-field validation, enum validation, idempotency skip, masker behavior, persistence upsert/insert paths.

**Non-Goals:**

- Middleware / Action Filter logging.
- Kafka Producer.
- Logger Query API (GET /api/log-flows, GET /api/log-flows/{flowId}, GET /api/log-actions/{actionId}).
- Frontend ReactJS components.
- Authentication / JWT on APIs.
- Separate Worker Service project.
- DLQ consumer or DLQ monitoring.
- Metrics / OpenTelemetry export.
- Payload size cap enforcement (truncation flag) — deferred to Phase 3.

---

## Decisions

### Decision 1 — Use Confluent.Kafka with manual offset commit

- **Why**: `Confluent.Kafka` is the standard .NET client. `enable.auto.commit=false` with explicit `consumer.Commit()` after DB commit gives us the at-least-once guarantee required by the design.
- **Alternative**: `Microsoft.Extensions.Hosting` Kafka integration (not mature enough); custom wrapper (overkill). Confluent.Kafka is battle-tested.
- **Consequence**: Offset is stored locally in the consumer group state on the broker; after redelivery the same message arrives with the same offset.

### Decision 2 — One `DbContext.SaveChangesAsync()` per message inside an explicit transaction

- **Why**: Guarantees atomicity across the three tables. If any insert fails, the entire unit of work rolls back and the consumer does not commit the offset.
- **Implementation**: `await using var tx = await _db.Database.BeginTransactionAsync(ct);` → upsert flow → insert action → upsert details → `await tx.CommitAsync(ct);` → `consumer.Commit(consumeResult)`.
- **Consequence**: A single Kafka message always produces either all three rows or none. Duplicate (idempotent skip) is safe because the flow upsert is idempotent by `flow_id`.

### Decision 3 — `log_flows` upsert uses raw SQL via EF Core `ExecuteSqlRawAsync`

- **Why**: EF Core's `Update()` changes the entity state to `Modified` which causes an UPDATE even on first insert (extra round trip). The upsert is simpler and more efficient as raw SQL: `INSERT ... ON CONFLICT (flow_id) DO UPDATE SET ...`.
- **Alternative**: Use `Add()` then catch `DbUpdateException` with PostgresException code `23505` — this works but requires an extra round-trip on conflict.
- **Consequence**: Raw SQL lives in `LogFlowRepository.UpsertAsync()`. It is parameterized to prevent injection.

### Decision 4 — `step_order` is derived from `COUNT(log_actions WHERE flow_id = X) + 1` at insert time

- **Why**: Simpler than maintaining a sequence or counter column. At typical flow sizes (<10 actions) the count query is negligible.
- **Alternative**: Add a `step_counter` column on `log_flows` incremented atomically — rejected: adds coupling between tables.
- **Consequence**: On a duplicate (idempotent skip), the count is not incremented because the insert is skipped.

### Decision 5 — Use `Polly` for retry policy with a custom delegate inside the consumer loop

- **Why**: Polly is the standard .NET resilience library. A policy wrapping `SaveChangesAsync` + `CommitAsync` handles transient DB errors. Policy is configured from `appsettings.json` via `IOptions<T>`.
- **Implementation**: `var retryPolicy = Policy.Handle<DbException>().WaitAndRetryAsync(maxAttempts, attempt => TimeSpan.FromMilliseconds(initialDelayMs * Math.Pow(multiplier, attempt - 1)));`
- **Consequence**: Retry is per-message. The DLQ is published only after the policy gives up, not on every retry failure.

### Decision 6 — Enum serialization: string in Kafka JSON, string in PostgreSQL `varchar`

- **Why**: Matches the Kafka contract (string values like `"SUCCESS"`, not integers). The PostgreSQL columns are `varchar` not integer so the schema is self-documenting. EF Core maps enums to `varchar` via value converters.
- **Alternative**: Integer enums in DB (faster, less readable) — rejected per design requirement for human-readable DB values.
- **Consequence**: `System.Text.Json` handles string enum serialization out of the box with `[JsonStringEnumConverter]`.

### Decision 7 — Payload truncation deferred to Phase 3

- **Why**: Implementing truncation adds complexity (check size on every field, potentially truncate JSON partially). The happy path (small payloads) works without it. 256 KB is generous; most payloads are < 10 KB.
- **Consequence**: No `payload_truncated` column or logic in this change. Add in Phase 3.

### Decision 8 — Test project uses xUnit with an in-memory SQLite provider for repository tests

- **Why**: In-memory SQLite supports EF Core with minimal friction. Repository/unit tests don't need a real PostgreSQL container. Integration tests (if added later) can use Testcontainers.
- **Alternative**: Real PostgreSQL in tests — adds CI complexity and startup time for this phase.
- **Consequence**: `Microsoft.EntityFrameworkCore.Sqlite` is used for repository tests with SQLite in-memory mode. Consumer/service tests use Moq without requiring a real database. Production uses Npgsql.

---

## Data Flow

```
Producer Service
    │
    │  Kafka: topic=skysim.action.logs, key=flowId (UTF-8)
    ▼
Kafka Broker  ───────────────────────────────► skysim.action.logs.dlq
    │  (dead-letter topic)
    │
KafkaConsumerService.ConsumeAsync()
    │
    ├─ 1. Deserialize JSON → LogEventMessage
    │       └─ Failure → PublishToDlq(reason="parse_error") → Commit()
    │
    ├─ 2. ValidateRequiredFields(message)
    │       └─ Failure → PublishToDlq(reason="validation_error") → Commit()
    │
    ├─ 3. ValidateEnumValues(message)
    │       └─ Failure → PublishToDlq(reason="invalid_<field>") → Commit()
    │
    ├─ 4. SensitiveDataMasker.Mask(message)
    │       └─ Recursive JSON traversal, replace deny-list leaves
    │
    ├─ 5. Check idempotency: log_actions WHERE event_id = X EXISTS?
    │       └─ YES → Log idempotent_skip → Commit()
    │
    ├─ 6. BeginTransaction()
    │       │
    │       ├─ 6a. Upsert log_flows (ON CONFLICT flow_id DO UPDATE)
    │       │         - total_steps += 1
    │       │         - success_steps or failed_steps += 1
    │       │         - last_action_type, last_message, updated_at
    │       │         - completed_at if terminal status
    │       │
    │       ├─ 6b. Insert log_actions (step_order = COUNT + 1)
    │       │         - event_id UNIQUE → duplicate → rollback + idempotent skip
    │       │
    │       ├─ 6c. Upsert log_action_details (ON CONFLICT action_id DO UPDATE)
    │       │         - request_payload, response_payload, error_payload
    │       │         - metadata
    │       │
    │       └─ CommitTransaction()
    │
    ├─ 7. consumer.Commit(consumeResult)  ← ONLY after DB commit
    │
    └─ 8. Structured log: { eventId, flowId, actionType, status, durationMs, outcome }
```

---

## Database Schema

All tables use `snake_case` naming and `timestamptz` for timestamps (UTC).

### log_flows

| Column | Type | Constraints |
|---|---|---|
| id | uuid | PK, DEFAULT gen_random_uuid() |
| flow_id | varchar(100) | UNIQUE, NOT NULL |
| flow_type | varchar(50) | NOT NULL |
| checkout_type | varchar(20) | NULL |
| status | varchar(20) | NOT NULL |
| customer_email | varchar(255) | NULL |
| customer_phone | varchar(30) | NULL |
| user_id | varchar(100) | NULL |
| order_id | varchar(100) | NULL |
| payment_id | varchar(100) | NULL |
| total_steps | integer | NOT NULL DEFAULT 0 |
| success_steps | integer | NOT NULL DEFAULT 0 |
| failed_steps | integer | NOT NULL DEFAULT 0 |
| last_action_type | varchar(50) | NULL |
| last_message | text | NULL |
| started_at | timestamptz | NOT NULL |
| completed_at | timestamptz | NULL |
| created_at | timestamptz | NOT NULL DEFAULT now() |
| updated_at | timestamptz | NOT NULL DEFAULT now() |

**Indexes:** `idx_log_flows_customer_email` (customer_email), `idx_log_flows_customer_phone` (customer_phone), `idx_log_flows_user_id` (user_id), `idx_log_flows_order_id` (order_id), `idx_log_flows_payment_id` (payment_id), `idx_log_flows_status` (status), `idx_log_flows_flow_type` (flow_type), `idx_log_flows_checkout_type` (checkout_type), `idx_log_flows_created_at` (created_at), `idx_log_flows_completed_at` (completed_at).

### log_actions

| Column | Type | Constraints |
|---|---|---|
| id | uuid | PK, DEFAULT gen_random_uuid() |
| event_id | uuid | UNIQUE, NOT NULL |
| flow_id | varchar(100)  | FK → log_flows(flow_id), NOT NULL |
| step_order | integer | NOT NULL |
| service_name | varchar(50) | NOT NULL |
| action_type | varchar(50) | NOT NULL |
| status | varchar(20) | NOT NULL |
| message | text | NULL |
| error_code | varchar(50) | NULL |
| error_message | text | NULL |
| request_time | timestamptz | NULL |
| response_time | timestamptz | NULL |
| duration_ms | integer | NULL |
| correlation_id | varchar(100) | NULL |
| created_at | timestamptz | NOT NULL DEFAULT now() |
| updated_at | timestamptz | NOT NULL DEFAULT now() |

**Indexes:** `idx_log_actions_event_id` (event_id UNIQUE), `idx_log_actions_flow_id` (flow_id), `idx_log_actions_service_name` (service_name), `idx_log_actions_action_type` (action_type), `idx_log_actions_status` (status), `idx_log_actions_created_at` (created_at).

### log_action_details

| Column | Type | Constraints |
|---|---|---|
| id | uuid | PK, DEFAULT gen_random_uuid() |
| action_id | uuid | FK → log_actions(id), UNIQUE, NOT NULL |
| request_payload | jsonb | NULL |
| response_payload | jsonb | NULL |
| error_payload | jsonb | NULL |
| metadata | jsonb | NULL |
| created_at | timestamptz | NOT NULL DEFAULT now() |
| updated_at | timestamptz | NOT NULL DEFAULT now() |

**Indexes:** `idx_log_action_details_action_id` (action_id UNIQUE).

---

## Kafka Configuration

```json
{
  "Kafka": {
    "Consumer": {
      "BootstrapServers": "localhost:9092",
      "Topic": "skysim.action.logs",
      "ConsumerGroup": "skysim-logger-consumer",
      "AutoOffsetReset": "earliest",
      "EnableAutoCommit": false,
      "EnableAutoCommitStore": false,
      "MaxPollIntervalMs": 300000,
      "SessionTimeoutMs": 45000
    },
    "Producer": {
      "BootstrapServers": "localhost:9092",
      "Acks": "all"
    },
    "Retry": {
      "MaxAttempts": 5,
      "InitialDelayMs": 200,
      "BackoffMultiplier": 2.0,
      "MaxDelayMs": 3200
    },
    "DlqTopic": "skysim.action.logs.dlq"
  }
}
```

---

## Project Structure

```
backend/
  Skysim.Logger.Api/
    Program.cs                         ← DI registration for DbContext, hosted service, masker
    appsettings.json                   ← Kafka, PostgreSQL connection, retry config
    Contracts/
      DTOs/
        LogEventMessage.cs            ← Kafka message DTO + enums (FlowType, CheckoutType, Status, DetailType)
        LogEventMessageValidator.cs   ← FluentValidation rules
    Domain/
      Entities/
        LogFlow.cs
        LogAction.cs
        LogActionDetail.cs
      Enums/
        FlowType.cs
        CheckoutType.cs
        Status.cs
        DetailType.cs
    Infrastructure/
      Persistence/
        LoggerDbContext.cs
        Repositories/
          ILogFlowRepository.cs
          LogFlowRepository.cs
          ILogActionRepository.cs
          LogActionRepository.cs
          ILogActionDetailRepository.cs
          LogActionDetailRepository.cs
      Kafka/
        KafkaLogConsumerService.cs    ← BackgroundService, 8-step pipeline
        IDlqPublisher.cs
        DlqPublisher.cs
        KafkaConsumerOptions.cs       ← IOptions pattern for Kafka config
    Common/
      SensitiveFields.cs              ← Deny-list constant (10 fields)
      SensitiveDataMasker.cs         ← Recursive JSON masker
      RetryPolicyFactory.cs           ← Polly retry policy builder
  Skysim.Logger.Api.Tests/
    Skysim.Logger.Api.Tests.csproj
    LogEventMessageValidatorTests.cs
    SensitiveDataMaskerTests.cs
    KafkaLogConsumerServiceTests.cs   ← Mock Confluent.Kafka IConsumer, in-memory DB
    RepositoryTests/
      LogFlowRepositoryTests.cs
      LogActionRepositoryTests.cs
```

---

## Retry & DLQ Behavior

| Failure Type | Retry? | DLQ? | Offset Commit? |
|---|---|---|---|
| JSON parse error | No | Yes (`parse_error`) | Yes (commit) |
| Missing required field | No | Yes (`validation_error`) | Yes (commit) |
| Invalid enum value | No | Yes (`invalid_<field>`) | Yes (commit) |
| Invalid timestamp format | No | Yes (`invalid_timestamp`) | Yes (commit) |
| DB connection error | Yes (5x, exp. backoff) | After exhausted | Yes (after DLQ) |
| DB constraint violation | No | No | Yes (idempotent skip) |
| Kafka broker unavailable | Yes (5x, exp. backoff) | After exhausted | Yes (after DLQ) |

DLQ headers added to every dead-letter message:

| Header Key | Value |
|---|---|
| `failure_reason` | `parse_error` \| `validation_error` \| `invalid_<field>` \| `db_unavailable` \| `broker_unavailable` |
| `failed_at` | ISO-8601 UTC timestamp |
| `consumer_attempt` | Integer attempt number at time of DLQ |

---

## Risks / Trade-offs

- **[Risk] `step_order` count query is O(N) per action** → Mitigation: flows are bounded (<10 steps); count query is sub-millisecond. Revisit if high-cardinality flows appear.
- **[Risk] Raw SQL upsert bypasses EF Core change tracking** → Mitigation: the raw SQL is in a dedicated repository method with parameterized inputs; EF Core's `DbContext` is still used for `Insert` of `log_actions` and `log_action_details`.
- **[Risk] In-memory SQLite cannot test PostgreSQL-specific features** → Mitigation: repository tests use in-memory for logic; a dedicated integration test project (Phase 3+) can use Testcontainers for real PostgreSQL.
- **[Risk] DLQ growth is unbounded** → Mitigation: out of scope for Phase 2; add monitoring + retention policy as an operational concern in Phase 3.
- **[Risk] No transaction isolation level specified** → Mitigation: default PostgreSQL isolation (`Read Committed`) is sufficient; no dirty reads expected.
- **[Risk] `consumer.Commit()` called after DB commit — if commit throws, offset is not committed** → Mitigation: wrap in try/finally with logging; a subsequent poll will redeliver the same message and the idempotency check handles it.
- **[Risk] Sensitive masker operates on `LogEventMessage` object tree, not raw JSON bytes** → Mitigation: masker traverses the deserialized DTO; if a field is null or not a container type, it is skipped. Works correctly for `requestData`, `responseData`, `errorPayload` which are `JsonElement` or `object`.

---

## Migration Plan

**Step 1 — Prerequisites:** Confirm PostgreSQL 16 and Kafka are running via `docker compose -f infra/docker-compose.yml up -d`. Verify `skysim.action.logs` topic exists (auto-created by Kafka if `KAFKA_CFG_AUTO_CREATE_TOPICS_ENABLE=true`).

**Step 2 — Add packages to `Skysim.Logger.Api.csproj`:**
```
Confluent.Kafka (latest stable)
Npgsql.EntityFrameworkCore.PostgreSQL
Microsoft.EntityFrameworkCore.Design
Polly
FluentValidation
```

**Step 3 — Create migration:** `dotnet ef migrations add InitialCreate --output-dir Infrastructure/Persistence/Migrations` — produces the 3-table schema. Apply with `dotnet ef database update`.

**Step 4 — Implement in task order (see tasks.md):** Contract → Entities → DbContext → Repositories → Masker → Consumer → DI → Tests.

**Step 5 — Run tests:** `dotnet test backend/Skysim.Logger.Api.Tests/`. All tests must pass.

**Step 6 — Local smoke test:**
1. Start API: `dotnet run --project backend/Skysim.Logger.Api`
2. Produce a test message to `skysim.action.logs` using `kafka-console-producer` or a small script
3. Query `log_flows` / `log_actions` in PostgreSQL to verify rows exist
4. Verify offset advances on restart (no duplicate rows)

**Rollback:** If a migration is bad, `dotnet ef database rollforward/rollback` or manually `DROP TABLE` the three tables. No data is lost since logs are append-only and replayable from Kafka retention.

---

## Open Questions

1. **EF Core migration generation** — Should migrations live in the `Skysim.Logger.Api` project or in a dedicated `Skysim.Logger.Migrations` project? Recommendation: keep in `Skysim.Logger.Api` for Phase 2 simplicity.
2. **DLQ retention policy** — How long should `skysim.action.logs.dlq` retain messages in local dev? Recommendation: no retention limit in Phase 2; Kafka default (7 days) is fine.
3. **Kafka `MaxPollIntervalMs` tuning** — For a consumer that processes one message at a time with a DB transaction, 5 minutes default may be too tight if DB transactions are slow. Recommendation: set to 600,000 ms (10 minutes) in `appsettings.Development.json` and keep 300,000 ms in production defaults.
4. **Missing `eventId` — send to DLQ or log and skip?** Recommendation: send to DLQ (as per the consumer spec). The producer made a mistake and the ops team needs visibility into the bad message.
5. **Terminal status detection** — When should `completed_at` be set on `log_flows`? Recommendation: when `status` is `SUCCESS` or `FAILED` AND `actionType` is one of the terminal types: `ORDER_FAILED`, `PAYMENT_FAILED`, `PROVIDER_FAILED`, `ESIM_ACTIVATION_FAILED`, `EMAIL_FAILED`, `ESIM_ACTIVATED` (ESIM_ACTIVATED is terminal-success). All other actions are in-progress. Confirm with mentor.
