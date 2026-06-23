# Capability: Logger Module Scope

## ADDED Requirements

### Requirement: The Logger module supports the CHECKOUT_ESIM flow in phase 1
The Logger SHALL ingest, persist, and serve log data for the `CHECKOUT_ESIM` flow, covering both `GUEST` and `AUTHENTICATED` checkout types.

#### Scenario: GUEST flow is traceable end-to-end
- **WHEN** a guest completes a checkout with no JWT but with `customerEmail` and `orderId`
- **THEN** every emitted action for that flow is persisted under one `log_flows.flow_id`, and the Logger Query API can return that flow filtered by `customerEmail` or `orderId`

#### Scenario: AUTHENTICATED flow is traceable end-to-end
- **WHEN** an authenticated user completes a checkout with a JWT and `userId`
- **THEN** every emitted action is persisted under one `log_flows.flow_id` with `userId` set, and the Logger Query API can return that flow filtered by `userId`

### Requirement: The Logger module owns ingestion, persistence, and query
The Logger SHALL be responsible for (a) consuming Kafka action events, (b) persisting them into PostgreSQL, and (c) exposing a REST API for the ReactJS log viewer.

#### Scenario: Ingestion path is owned by Logger
- **WHEN** a Kafka message arrives on `skysim.action.logs`
- **THEN** the Logger consumer is the sole writer that turns it into rows in `log_actions` and updates `log_flows`

#### Scenario: Query path is owned by Logger
- **WHEN** the ReactJS log viewer requests `GET /api/log-flows?customerEmail=...`
- **THEN** the request is served by the Logger API; no other service is required

### Requirement: The Logger module captures both business action logs and technical middleware logs
The Logger SHALL persist business action logs (via Kafka) AND technical middleware logs (HTTP request/response metadata) so an operator can join both views on `correlationId`/`flowId`.

#### Scenario: Business action log is ingested via Kafka
- **WHEN** a service publishes a business step event such as `PAYMENT_SUCCESS`
- **THEN** it is delivered through Kafka and stored in `log_actions`

#### Scenario: Technical middleware log is captured at the HTTP boundary
- **WHEN** any Skysim service handles an HTTP request
- **THEN** its middleware/filter emits a technical log entry containing `service`, `action`, `requestTime`, `responseTime`, `duration`, `requestData`, `responseData`, `userId`, `exception`, and `correlationId`

### Requirement: Out-of-scope items are explicitly listed
The Logger module in phase 1 SHALL NOT implement frontend UI, distributed tracing exporters, metrics exporters, multi-tenant isolation, or production alerting. These are tracked as future work.

#### Scenario: Frontend is out of scope for this change
- **WHEN** the mentor reviews the change
- **THEN** they find no ReactJS code or routing logic in this change; only Logger Query API contracts

#### Scenario: Tracing exporters are out of scope
- **WHEN** the design is reviewed
- **THEN** it does not commit to OpenTelemetry exporters, Jaeger, or Prometheus integration in phase 1
