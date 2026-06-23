## Why

Phase 1 (Backend Foundation) of the Skysim Logger module needs a single, agreed technical design before any code is written. Today the team has documentation fragments (`docs/01–05`) but no canonical, mentor-reviewable artifact that ties together the Skysim system overview, the Logger module scope, the Kafka contract, the consumer flow, the PostgreSQL schema, the Middleware/Action Filter logging contract, and the Logger query API. Without this design we risk reworking the consumer, schema, and APIs during implementation.

This change is **design only**. It defines the technical design for the Logger module — including architecture overview, Kafka message contract, consumer processing flow, PostgreSQL `log_flows` / `log_actions` / `log_action_details` design, Middleware/Action Filter logging, Logger Query API draft, error handling, retry, idempotency, sensitive-data masking, and open questions for mentor review. No application code is introduced here; that work belongs to the subsequent `implement-logger-backend` change.

## What Changes

- **New** — Skysim system architecture overview (B2C/B2B/CMS → CDN/Gateway → KONG → Keycloak → microservices → Kafka → Logger → PostgreSQL) and explicit placement of the Logger service inside that architecture.
- **New** — Logger module scope statement: in-sflow (CHECKOUT_ESIM flow, Kafka consumer, persistence, query API, shared middleware logging) vs. out-of-scope (frontend, full multi-flow support, distributed tracing exporters).
- **New** — Kafka log message contract for topic `skysim.action.logs`: key = `flowId`, JSON value schema, required fields (`eventId`, `flowId`, `flowType`, `serviceName`, `actionType`, `status`, `createdAt`), optional fields, and sensitive-field masking rules.
- **New** — Kafka Consumer processing flow: validate → mask → idempotency check → upsert `log_flows` → insert `log_actions` → upsert `log_action_details` → manual offset commit, with retry and DLQ behavior.
- **New** — PostgreSQL schema design using 3 tables (`log_flows`, `log_actions`, `log_action_details`) with columns, indexes, and relationships.
- **New** — Middleware / Action Filter Logging contract (request/response/duration/correlationId/userId/exception) and how it differs from business action logs.
- **New** — Logger Query API draft: endpoints, query parameters, pagination, sorting, filtering, and response DTO shape.
- **New** — Cross-cutting design decisions: error handling, retry strategy, idempotency, DLQ, and sensitive-data masking.
- **New** — Open questions list for mentor review before implementation begins.

## Capabilities

### New Capabilities

- `skysim-architecture-overview`: Architecture overview of the Skysim system (frontend tiers, gateway, auth, microservices, Kafka, Logger, PostgreSQL) used to contextualize where the Logger module lives.
- `logger-module-scope`: Scope and responsibilities of the Logger module — which flows it supports, which services produce logs, and what it does not do in this phase.
- `kafka-log-message-contract`: Kafka topic name, message key, JSON value schema, required/optional fields, and masking rules for `skysim.action.logs`.
- `kafka-log-consumer-flow`: Kafka Consumer processing pipeline: validation, idempotency, persistence, retry, DLQ, and offset commit semantics.
- `logger-database-design`: PostgreSQL schema for `log_flows`, `log_actions`, `log_action_details` including columns, indexes, and relationships.
- `middleware-logging-contract`: Shared Middleware / Action Filter logging contract (technical logs) and its separation from business action logs.
- `logger-query-api`: Logger REST API contract for listing flows, getting flow detail, and getting action detail with filtering, sorting, and pagination.
- `logger-cross-cutting-concerns`: Error handling, retry, idempotency, DLQ, and sensitive-data masking policies applied consistently across Logger features.
- `logger-design-open-questions`: List of open questions and assumptions recorded for mentor review before implementation.

### Modified Capabilities

_None._ This is a greenfield design; there are no existing Logger capabilities in `openspec/specs/` whose requirements are being modified.

## Impact

- **OpenSpec change**: introduces `openspec/changes/define-logger-technical-design/` containing proposal, design, and capability specs. No existing specs are modified.
- **Documentation**: consolidates and aligns content previously spread across `docs/01-skysim-architecture.md`, `docs/02-checkout-esim-flow.md`, `docs/03-kafka-message-consumer-design.md`, `docs/04-logger-database-api-design.md`, and `docs/05-middleware-logging-week1-review.md`.
- **Backend code (deferred)**: this change does not introduce code; downstream change `implement-logger-backend` will implement against this design.
- **Frontend code (out of scope)**: no frontend impact; ReactJS log viewer consumes the API once the backend is implemented.
- **Infrastructure**: design is compatible with existing `infra/docker-compose.yml` (Kafka + PostgreSQL).
