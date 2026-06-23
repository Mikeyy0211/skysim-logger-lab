# Capability: Logger Design Open Questions

## ADDED Requirements

### Requirement: Open questions are recorded as part of the design
The design SHALL record the open questions below so the mentor can resolve them before `implement-logger-backend` begins. Each question includes the proposed default and the rationale for picking it if the mentor does not object.

#### Scenario: Action types as enum or string?
- **WHEN** the mentor reviews the design
- **THEN** the question is recorded: "Should `actionType` be a C# enum (`LoggerActionType`) or a free string column?" — Proposed default: enum in code, free string in DB with a CHECK constraint accepting the canonical 11 values plus `OTHER` for forward-compat.

#### Scenario: Flow types beyond CHECKOUT_ESIM
- **WHEN** the mentor reviews the design
- **THEN** the question is recorded: "Should `flowType` be a free string or a closed enum in phase 1?" — Proposed default: free string + index, defer enum until a second flow type appears.

#### Scenario: Correlation ID producer
- **WHEN** the mentor reviews the design
- **THEN** the question is recorded: "Who generates `correlationId` — first service, KONG, or Logger?" — Proposed default: KONG/edge generates and propagates via `X-Correlation-Id`; first service may generate if absent.

#### Scenario: DLQ retention
- **WHEN** the mentor reviews the design
- **THEN** the question is recorded: "How long are messages retained in `skysim.action.logs.dlq`?" — Proposed default: out of scope for phase 1; document as operational follow-up.

#### Scenario: GUEST identification fallback
- **WHEN** the mentor reviews the design
- **THEN** the question is recorded: "If a GUEST flow has neither `customerEmail` nor `customerPhone`, how do we search?" — Proposed default: require at least one of `orderId`/`paymentId` on every message; reject otherwise.

#### Scenario: Sensitive field ownership
- **WHEN** the mentor reviews the design
- **THEN** the question is recorded: "Who owns the canonical deny-list — Logger or each producer?" — Proposed default: Logger owns; producers encouraged to mask early.

#### Scenario: Payload size cap
- **WHEN** the mentor reviews the design
- **THEN** the question is recorded: "What is the cap on `requestPayload`/`responsePayload` JSON size?" — Proposed default: 256 KB per column with `payload_truncated = true` on overflow.

#### Scenario: Logger API authentication in phase 1
- **WHEN** the mentor reviews the design
- **THEN** the question is recorded: "Should the Logger API require JWT in phase 1, or run with a dev bypass?" — Proposed default: `[Authorize]` placeholder with `Logger:Auth:DevBypass` flag, defaulting to `true` in `Development` and `false` in `Production`.

#### Scenario: Consumer deploy unit
- **WHEN** the mentor reviews the design
- **THEN** the question is recorded: "Should the Kafka consumer run in-process with the API or as a separate Worker Service?" — Proposed default: in-process BackgroundService for phase 1, separable later.

### Requirement: Resolutions are tracked in this spec
Each open question SHALL be resolved by the mentor and the resolution recorded in this spec under a `## Resolutions` section (to be added after review). Implementation SHALL NOT begin until every open question has either a confirmed default or an explicit override.

#### Scenario: Resolution is recorded
- **WHEN** the mentor confirms a proposed default
- **THEN** the question moves to `## Resolutions` with the chosen value and the date
