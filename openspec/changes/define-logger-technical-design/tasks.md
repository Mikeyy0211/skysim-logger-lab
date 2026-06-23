# Tasks: define-logger-technical-design

This change is **design only**. No application code is produced by these tasks. The tasks below produce the artifacts the mentor reviews, and they pre-wire the implementation tasks that will live in the follow-on `implement-logger-backend` change. Each task is verifiable.

## 1. Architecture & Scope Artifacts

- [ ] 1.1 Write `proposal.md` describing Why / What Changes / Capabilities / Impact for the Logger technical design
- [ ] 1.2 Write `specs/skysim-architecture-overview/spec.md` covering tiers (frontend, gateway, auth, microservices, Kafka, Logger, PostgreSQL) and the Logger's place in the system
- [ ] 1.3 Write `specs/logger-module-scope/spec.md` defining in-scope (CHECKOUT_ESIM, GUEST/AUTHENTICATED, ingestion, persistence, query, middleware logging) and out-of-scope items

## 2. Kafka Contract

- [ ] 2.1 Write `specs/kafka-log-message-contract/spec.md` defining topic `skysim.action.logs`, message key `flowId`, JSON value schema, required/optional fields, status and action-type closed sets, timestamp format, and masking rules
- [ ] 2.2 Cross-check the Kafka contract against `docs/03-kafka-message-consumer-design.md` and reconcile any discrepancies in the spec

## 3. Consumer Flow

- [ ] 3.1 Write `specs/kafka-log-consumer-flow/spec.md` defining the BackgroundService host, the 8-step pipeline (parse → validate → mask → idempotency → upsert flow → insert action → upsert details → commit), manual offset commit, retry policy, and DLQ behavior
- [ ] 3.2 Document retry parameters (`maxAttempts`, `initialDelayMs`, `backoffMultiplier`, `maxDelayMs`) and DLQ headers (`failure_reason`, `failed_at`, `consumer_attempt`)

## 4. Database Design

- [ ] 4.1 Write `specs/logger-database-design/spec.md` defining the three tables `log_flows`, `log_actions`, `log_action_details` with columns, types, FKs, UNIQUE on `event_id`, and payload size cap
- [ ] 4.2 Document every required index (email, phone, userId, orderId, paymentId, status, flowType, checkoutType, createdAt, completedAt, flowId, serviceName, actionType) and which query uses which index

## 5. Middleware / Action Filter Logging

- [ ] 5.1 Write `specs/middleware-logging-contract/spec.md` covering the field set (`service`, `action`, `requestTime`, `responseTime`, `duration`, `requestData`, `responseData`, `userId`, `exception`, `correlationId`), the separation from business action logs, and `X-Correlation-Id` propagation

## 6. Logger Query API

- [ ] 6.1 Write `specs/logger-query-api/spec.md` defining the three endpoints, query parameters, pagination envelope, sort fields, DTO shape, validation errors, and the dev-bypass auth placeholder

## 7. Cross-Cutting Concerns

- [ ] 7.1 Write `specs/logger-cross-cutting-concerns/spec.md` covering error classification (Validation / Transient / Permanent), retry policy, idempotency at the database, centralized masking, DLQ topic, and structured logging shape

## 8. Design & Open Questions

- [ ] 8.1 Write `design.md` covering Context, Goals/Non-Goals, Decisions (with rationale and alternatives), Risks/Trade-offs, Migration Plan, and Open Questions
- [ ] 8.2 Write `specs/logger-design-open-questions/spec.md` recording every open question and proposed default for mentor review

## 9. Verification

- [ ] 9.1 Run `openspec validate "define-logger-technical-design" --strict` and confirm no errors
- [ ] 9.2 Run `openspec status --change "define-logger-technical-design"` and confirm all artifacts are `done` and `applyRequires` is satisfied
- [ ] 9.3 Cross-check every capability listed in `proposal.md` against `specs/` and confirm one spec file per capability

## 10. Mentor Review Handoff

- [ ] 10.1 Send the design change to the mentor with a short Vietnamese summary highlighting: scope, key decisions (10 in design.md), open questions needing answers
- [ ] 10.2 On approval, run `/opsx:archive define-logger-technical-design` and open the follow-on `implement-logger-backend` change using this design as source of truth
