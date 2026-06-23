# Capability: Logger Database Design

## ADDED Requirements

### Requirement: The Logger uses a 3-table schema: log_flows, log_actions, log_action_details
The Logger database SHALL contain exactly three tables in phase 1: `log_flows` (one row per business flow), `log_actions` (one row per action/timeline step), and `log_action_details` (one row per action holding heavy JSON payloads).

#### Scenario: One flow produces one log_flows row
- **WHEN** the first action of a new `flowId` arrives
- **THEN** exactly one row is created in `log_flows` for that `flowId`

#### Scenario: One action produces one log_actions row
- **WHEN** a Kafka message is persisted successfully
- **THEN** exactly one row is created in `log_actions` with the message's `eventId`

#### Scenario: One action produces at most one log_action_details row
- **WHEN** a Kafka message is persisted successfully
- **THEN** exactly one row exists in `log_action_details` keyed by the new `log_actions.id`

### Requirement: log_flows stores the flow summary for fast search and filtering
The `log_flows` table SHALL store one row per business flow with the columns `flow_id` (PK), `flow_type`, `checkout_type`, `status`, `customer_email`, `customer_phone`, `user_id`, `order_id`, `payment_id`, `total_steps`, `success_steps`, `failed_steps`, `last_action_type`, `last_message`, `started_at`, `completed_at`, `created_at`, `updated_at`.

#### Scenario: Columns capture identity
- **WHEN** a flow is upserted
- **THEN** `flow_id` is unique and the row carries `flow_type`, `checkout_type`, `customer_email`, `customer_phone`, `user_id`, `order_id`, `payment_id` as appropriate

#### Scenario: Columns capture progress counters
- **WHEN** a new action is added to a flow
- **THEN** `total_steps` increments, and `success_steps` or `failed_steps` increments depending on the action's `status`

#### Scenario: Columns capture last-seen state
- **WHEN** a new action is added to a flow
- **THEN** `last_action_type`, `last_message`, `updated_at`, and (if terminal) `completed_at` are updated

### Requirement: log_actions stores each timeline step
The `log_actions` table SHALL store one row per action with the columns `id` (PK), `event_id` (UNIQUE), `flow_id` (FK), `step_order`, `service_name`, `action_type`, `status`, `message`, `error_code`, `error_message`, `request_time`, `response_time`, `duration_ms`, `correlation_id`, `created_at`, `updated_at`.

#### Scenario: event_id is unique
- **WHEN** the same `eventId` is presented twice
- **THEN** the second insert fails the unique constraint and the consumer treats it as an idempotent skip

#### Scenario: step_order is set per flow
- **WHEN** an action is inserted for a flow with N existing actions
- **THEN** `step_order = N + 1`

#### Scenario: flow_id references log_flows
- **WHEN** a row is inserted in `log_actions`
- **THEN** `flow_id` references an existing row in `log_flows` (FK constraint)

### Requirement: log_action_details stores heavy JSON payloads separately
The `log_action_details` table SHALL store one row per action with the columns `action_id` (PK and FK), `request_payload` (JSONB), `response_payload` (JSONB), `error_payload` (JSONB), `metadata` (JSONB), `payload_truncated` (boolean), `created_at`, `updated_at`.

#### Scenario: Heavy payloads are not loaded by list APIs
- **WHEN** `GET /api/log-flows` returns items
- **THEN** the response does not include `request_payload`, `response_payload`, or `error_payload`

#### Scenario: Heavy payloads are loaded by detail APIs
- **WHEN** `GET /api/log-actions/{actionId}` is called
- **THEN** the response includes masked `request_payload`, `response_payload`, and `error_payload`

#### Scenario: Payload truncation flag is set when size cap is exceeded
- **WHEN** an incoming payload exceeds 256 KB
- **THEN** the column stores the truncated payload and `payload_truncated = true`

### Requirement: Indexes cover the common search and filter fields
The schema SHALL include indexes on `log_flows`: `flow_id` (PK), `customer_email`, `customer_phone`, `user_id`, `order_id`, `payment_id`, `status`, `flow_type`, `checkout_type`, `created_at`, `completed_at`. On `log_actions`: `event_id` (UNIQUE), `flow_id`, `service_name`, `action_type`, `status`, `created_at`. On `log_action_details`: `action_id` (PK).

#### Scenario: List query by email uses the index
- **WHEN** `GET /api/log-flows?customerEmail=...` is executed
- **THEN** the planner uses the `idx_log_flows_customer_email` index

#### Scenario: Timeline by flow uses the flow_id index
- **WHEN** `GET /api/log-flows/{flowId}` is executed
- **THEN** the planner uses the `idx_log_actions_flow_id` index on `log_actions`

### Requirement: Naming and timestamp conventions follow workspace rules
Tables and columns SHALL use `snake_case`. Every table SHALL have `created_at` and `updated_at` of type `timestamptz` defaulting to `now()` at insert and updated by a trigger or by the application on update.

#### Scenario: snake_case naming is enforced
- **WHEN** the schema is applied
- **THEN** all tables and columns use `snake_case` (e.g. `customer_email`, `created_at`, `log_action_details`)

#### Scenario: Timestamps are stored in UTC
- **WHEN** a row is inserted
- **THEN** `created_at` is set to the current UTC time as `timestamptz`
