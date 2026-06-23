## Context

The Skysim Logger lab is being built to a defined roadmap (Backend 70% / Frontend 30%). Five reference docs already exist (`docs/01`–`docs/05`) covering architecture, the CHECKOUT_ESIM flow, the Kafka consumer, the database/API design, and a Week 1 middleware-logging review. They are useful but not committed as a single technical design, and the project has no existing Logger code beyond the empty ASP.NET Core shell at `backend/Skysim.Logger.Api/Program.cs`.

The current state:

- `Skysim.Logger.sln` exists with the `Skysim.Logger.Api` shell project but no domain, infra, or consumer code.
- `infra/docker-compose.yml` provisions Kafka and PostgreSQL for local development.
- `frontend/skysim-logger-web` exists but is empty stub; no API is wired yet.
- `openspec/specs/` is empty; `openspec/changes/` is empty.
- The project rule file at `.cursor/rules/rule-skysim.mdc` already encodes the high-level conventions (topic name, message key, table names, action types, masking rules, API endpoints).

Constraints:

- This change is **design only**. No C# or TypeScript code is introduced. Implementation lives in a follow-on change.
- The design must remain implementable by a single developer within the probation roadmap.
- Conventions from the workspace rules and `openspec/config.yaml` are binding (snake_case DB, `eventId` idempotency, `flowId` correlation, etc.).
- The Logger must trace flows for both GUEST and AUTHENTICATED checkout types.

Stakeholders:

- **Mentor**: reviews this design before implementation begins.
- **Backend developer**: implements `implement-logger-backend` against this design.
- **Frontend developer**: depends on the API contract defined here for the ReactJS log viewer.

## Goals / Non-Goals

**Goals:**

- Produce a single, mentor-reviewable technical design for the Skysim Logger module covering architecture overview, scope, Kafka contract, consumer flow, PostgreSQL schema, Middleware/Action Filter logging, Logger Query API, cross-cutting concerns, and open questions.
- Define the Kafka log message contract precisely enough that any service producer can publish compliant messages.
- Define the 3-table PostgreSQL schema (`log_flows`, `log_actions`, `log_action_details`) with columns, indexes, and relationships.
- Define the consumer pipeline including validation, idempotency, persistence, retry, DLQ, and manual offset commit semantics.
- Define the Middleware/Action Filter logging contract and clearly separate it from business action logs.
- Draft the Logger Query API contract (endpoints, parameters, pagination, sorting, DTO shape).
- Capture open questions and assumptions explicitly so the mentor can resolve them before implementation.

**Non-Goals:**

- Implementing any C# or TypeScript code.
- Building the ReactJS log viewer UI.
- Implementing distributed tracing exporters, metrics exporters, or a full observability stack.
- Supporting flows other than CHECKOUT_ESIM in this phase (the schema and consumer should remain open to adding more later).
- Authentication/authorization of the Logger API itself (assumed to live behind KONG Gateway + JWT in production; this phase treats the API as internal-only).
- Schema migration tooling beyond what EF Core / Dapper provides natively.

## Decisions

### Decision 1 — Use a 3-table Logger schema (log_flows / log_actions / log_action_details)

- **Why**: Matches workspace rules and `openspec/config.yaml` `database` rules. Keeps list APIs fast (no JSONB scan) and detail APIs full (payload only on demand).
- **Alternatives**: Single-table denormalized design (rejected: list/detail tradeoff harder), 4-table with a separate `log_users` (rejected: user identity lives in Keycloak claims; not needed in phase 1).
- **Consequence**: Every Kafka message produces one row in `log_actions` and one upsert in `log_flows`. Heavy payloads go only to `log_action_details`.

### Decision 2 — Use `flowId` as the Kafka message key

- **Why**: Ensures all events of one CHECKOUT_ESIM flow land on the same partition → ordered processing → simpler consumer reasoning and predictable retries.
- **Alternatives**: `eventId` (rejected: loses ordering across the flow), random key (rejected: no partitioning guarantee).
- **Consequence**: Partition count must be sized for throughput; the consumer is single-threaded per partition.

### Decision 3 — Use `eventId` as the idempotency key in `log_actions`

- **Why**: Required by workspace rules. Producers may retry; Kafka guarantees at-least-once. We must not double-insert.
- **Implementation**: UNIQUE constraint on `log_actions.event_id`. On conflict (Postgres `ON CONFLICT (event_id) DO NOTHING`), the consumer skips payload upsert and only commits offset.
- **Consequence**: Duplicate messages are safe; the `log_flows` summary is recomputed from the (de-duplicated) `log_actions` set on each new event.

### Decision 4 — Manual offset commit, AFTER successful DB transaction

- **Why**: Workspace rule. Never commit Kafka offset before persisting to PostgreSQL.
- **Implementation**: The consumer commits the offset only after the DB transaction (action insert + flow upsert + details upsert) commits. Failures → no commit → redelivery.
- **Consequence**: At-least-once semantics; idempotency (Decision 3) handles duplicates.

### Decision 5 — Retry with bounded in-process attempts, then DLQ topic

- **Why**: Workspace rules require explicit retry and a DLQ topic name `skysim.action.logs.dlq`.
- **Implementation**: Transient failures (DB connection drop, Kafka producer unavailable) trigger up to N in-process retries with exponential backoff. After N failures the original message is published to `skysim.action.logs.dlq` with a `failure_reason` header, then offset is committed. Poison messages (schema-invalid, missing required field) are sent straight to DLQ without retry.
- **Consequence**: Consumer never gets stuck on one bad message.

### Decision 6 — Sensitive data masking happens at the producer AND at the consumer boundary

- **Why**: Defense in depth. We cannot trust producers fully and we must not leak secrets to PostgreSQL.
- **Implementation**: The Logger exposes a `SensitiveDataMasker` with a deny-list (`password`, `access_token`, `refresh_token`, `authorization`, `otp`, `cardNumber`, `cvv`, `paymentSecret`, `secret`, `token`) that recursively replaces values with `"***"` in any JSON tree before persistence and before DLQ publish.
- **Consequence**: Database never holds raw secrets. Logged payloads are safe to expose in detail APIs (still gated by API auth).

### Decision 7 — Middleware/Action Filter logs are TECHNICAL, business action logs are BUSINESS

- **Why**: Workspace rules explicitly distinguish them.
- **Implementation**: The middleware captures every HTTP request to/from any Skysim service (including Logger itself) and writes a technical row (requestTime, responseTime, duration, statusCode, correlationId, userId, exception). Business action logs (ORDER_CREATED, PAYMENT_SUCCESS, etc.) are published by services to Kafka and ingested via the consumer flow. The two streams are not mixed.
- **Consequence**: Tracing a checkout requires joining by `correlationId`/`flowId` across both streams; the Logger Query API exposes both surfaces.

### Decision 8 — Kafka consumer is a .NET 8 BackgroundService inside the same Logger API process (phase 1)

- **Why**: Simpler ops, single deployable, and acceptable for phase 1 throughput. Matches probation scope.
- **Alternatives**: Separate Worker Service project (rejected for phase 1: extra deployable, extra config), external consumer (out of probation scope).
- **Consequence**: API and consumer share configuration, logging, and DI. We can split them later without changing the contract.

### Decision 9 — EF Core for persistence, Dapper only if EF proves too heavy

- **Why**: EF Core gives migrations, change tracking, and `ON CONFLICT` via raw SQL for the flow upsert. Keeps the lab familiar.
- **Consequence**: We use `Npgsql.EntityFrameworkCore.PostgreSQL` and add one raw SQL for the `log_flows` upsert keyed on `flow_id`.

### Decision 10 — Logger Query API uses a stable, frontend-friendly DTO shape

- **Why**: ReactJS + TypeScript needs typed contracts; pagination/sorting/filtering must match the workspace rule.
- **Implementation**: List endpoints return `{ items, page, pageSize, totalItems, totalPages }`. Heavy payloads are excluded from list responses; `GET /api/log-actions/{actionId}` returns the full detail including masked payloads.
- **Consequence**: Frontend can render lists quickly and load detail on demand.

## Risks / Trade-offs

- **[Risk] Phase 1 couples consumer and API in one process** → Mitigation: design the consumer behind an `ILogIngestionService` interface so it can be extracted to a Worker Service later without contract changes.
- **[Risk] Idempotency relies on `eventId` being globally unique** → Mitigation: document the requirement in the Kafka contract; reject messages without `eventId` and send them to DLQ.
- **[Risk] Flow summary recompute on each event is O(N) per flow** → Mitigation: flows are bounded (typically <10 actions) so this is acceptable in phase 1; revisit if high-cardinality flows appear.
- **[Risk] Sensitive-field masker must be kept in sync with new secret-like fields** → Mitigation: centralize the deny-list in `Logger.Core/Common/SensitiveFields.cs` and add a unit test for each new field.
- **[Risk] DLQ growth is unbounded** → Mitigation: out of scope for phase 1; add a TODO and revisit during backend implementation (operational concern, not a design blocker).
- **[Risk] Manual offset commit + at-least-once means duplicate flow upserts possible** → Mitigation: flow upsert is idempotent on `flow_id`; summary is recomputed from current `log_actions` rows so re-applying an event is a no-op.
- **[Risk] GUEST checkout has no `userId`** → Mitigation: schema treats `userId` as nullable; queries that filter by `userId` are explicitly GUEST-aware; default search falls back to email/phone/orderId/paymentId.
- **[Risk] Masking may break producer JSON shape** → Mitigation: masker operates on the parsed JSON tree and only replaces string leaves whose key matches the deny-list; structure is preserved.

## Migration Plan

This change introduces no code and no deployed artifacts; there is no migration plan for runtime. The "migration" here is documentation:

1. Review `proposal.md`, `design.md`, and `specs/` with the mentor.
2. Resolve all open questions in `specs/logger-design-open-questions/spec.md`.
3. On approval, close this change (`/opsx:archive`) and open `implement-logger-backend` using this design as its source of truth.
4. Implement in the order defined in `tasks.md`: contract → schema → migrations → consumer → validation/idempotency → persistence → middleware → query APIs → tests → local demo.

Rollback: since no code ships, rollback is "edit the docs" — archive this change without merging if mentor requests changes, then revise.

## Open Questions

These are intentionally surfaced for mentor review. Each is also recorded in `specs/logger-design-open-questions/spec.md`.

1. **Action types final list** — the workspace rule lists 11 action types; should we codify them as a C# enum (`LoggerActionType`) or as a string column with a CHECK constraint? Phase 1 recommendation: enum in code, free string in DB.
2. **Flow types beyond CHECKOUT_ESIM** — should `flowType` be a free string or an enum in phase 1? Recommendation: free string + index, keep open.
3. **Correlation ID propagation** — is `correlationId` produced by the first service (Order) or by an upstream component (KONG)? Recommendation: produce at KONG/edge, propagate via header `X-Correlation-Id`.
4. **DLQ retention** — how long do we retain messages in `skysim.action.logs.dlq`? Recommendation: local dev = no retention; production out of scope.
5. **GUEST identification** — when both `email` and `phone` are missing on a GUEST flow, how do we search? Recommendation: require at least one of `orderId`/`paymentId` to be present on every Kafka message.
6. **Sensitive field ownership** — who owns the canonical deny-list (Logger or each producer)? Recommendation: Logger owns it; producers are encouraged to mask early but the Logger re-masks before persistence.
7. **Action payload size** — cap on `requestPayload`/`responsePayload` JSON size? Recommendation: 256 KB per column with a `payload_truncated` boolean if exceeded.
8. **Outbox from Logger to Postgres** — should the consumer write to an outbox table for downstream consumers of log data? Recommendation: not in phase 1.
9. **Authentication on Logger Query API** — the workspace rule says it works "behind KONG Gateway and JWT". Do we add a stub JWT validator in phase 1? Recommendation: add an `[Authorize]` placeholder with a dev-bypass config flag; mentor to confirm.
