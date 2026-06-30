# logger-e2e-smoke-test Specification

## Purpose
TBD - created by archiving change verify-logger-e2e-pipeline. Update Purpose after archive.
## Requirements
### Requirement: Kafka topics are created idempotently

The Kafka broker SHALL provide or use a mechanism to create topics without error if they already exist. Required topics are `skysim.action.logs` and `skysim.action.logs.dlq`.

#### Scenario: Topic creation script is idempotent

- **WHEN** `scripts/create-kafka-topics.sh` is run when topics already exist
- **THEN** the script SHALL exit with code 0 and not report an error

#### Scenario: Topic creation script creates missing topics

- **WHEN** `scripts/create-kafka-topics.sh` is run when topics do not exist
- **THEN** the script SHALL create both `skysim.action.logs` and `skysim.action.logs.dlq`

#### Scenario: Kafka consumer processes messages after topics are created

- **WHEN** Logger.Api is running with the Kafka consumer background service
- **AND** Kafka topics are created or already exist
- **THEN** the consumer SHALL continue processing messages from `skysim.action.logs` without requiring a restart

### Requirement: Guest checkout flow is traced correctly

When SampleService receives a checkout request without an Authorization header, the Logger.Client middleware SHALL log an HTTP request/response event with the provided flowId.

#### Scenario: Guest flow produces HTTP request log with guest checkout type in response

- **WHEN** a POST request is sent to `POST /api/checkout/esim` with `X-Flow-Id: e2e-guest-flow-001` and without an Authorization header
- **THEN** SampleService SHALL return a response with `checkoutType = "GUEST"` and `flowId = "e2e-guest-flow-001"`
- **AND** Logger.Client middleware SHALL publish an HTTP_REQUEST log event with `flowId = "e2e-guest-flow-001"` to Kafka

#### Scenario: Guest flow flowId appears in Logger.Api query results

- **WHEN** the Kafka consumer processes guest checkout messages for `e2e-guest-flow-001`
- **THEN** a GET request to `/api/log-flows?flowId=e2e-guest-flow-001` with a valid Bearer token SHALL return at least one flow with `flowId = "e2e-guest-flow-001"`

### Requirement: Authenticated checkout flow is traced correctly

When SampleService receives a checkout request with an Authorization header, the Logger.Client middleware SHALL log an HTTP request/response event with the provided flowId. SampleService does not validate or parse the token.

#### Scenario: Authenticated flow produces HTTP request log with authenticated checkout type in response

- **WHEN** a POST request is sent to `POST /api/checkout/esim` with `X-Flow-Id: e2e-auth-flow-001` and with a valid Keycloak Bearer token in the Authorization header
- **THEN** SampleService SHALL return a response with `checkoutType = "AUTHENTICATED"` and `flowId = "e2e-auth-flow-001"`
- **AND** Logger.Client middleware SHALL publish an HTTP_REQUEST log event with `flowId = "e2e-auth-flow-001"` to Kafka

#### Scenario: Authenticated flow flowId appears in Logger.Api query results

- **WHEN** the Kafka consumer processes authenticated checkout messages for `e2e-auth-flow-001`
- **THEN** a GET request to `/api/log-flows?flowId=e2e-auth-flow-001` with a valid Bearer token SHALL return at least one flow with `flowId = "e2e-auth-flow-001"`

### Requirement: Logger.Api query endpoints require Bearer token

The `/api/log-flows` and `/api/log-actions` endpoints SHALL reject requests without a valid Authorization Bearer token.

#### Scenario: Query endpoint returns 401 without token

- **WHEN** a GET request is sent to `/api/log-flows` without an Authorization header
- **THEN** Logger.Api SHALL return 401 Unauthorized

#### Scenario: Query endpoint returns 200 with valid Keycloak token

- **WHEN** a GET request is sent to `/api/log-flows` with a valid Keycloak Bearer token in the Authorization header
- **THEN** Logger.Api SHALL return 200 OK with a paginated response

### Requirement: Health endpoint remains accessible without token

The `/health` endpoint SHALL return 200 OK without requiring an Authorization header.

#### Scenario: Health endpoint returns 200 without token

- **WHEN** a GET request is sent to `/health` without an Authorization header
- **THEN** Logger.Api SHALL return 200 OK

### Requirement: Guest flow actions appear in /api/log-actions

After triggering a guest checkout and waiting for the Kafka consumer to process, the smoke test SHALL be able to query `/api/log-actions` by flowId and confirm action records exist.

#### Scenario: Guest flow actions appear in /api/log-actions

- **WHEN** the Kafka consumer has processed guest checkout messages for `e2e-guest-flow-001`
- **AND** a GET request is sent to `/api/log-actions?flowId=e2e-guest-flow-001` with a valid Bearer token
- **THEN** the response SHALL contain action items with `flowId = "e2e-guest-flow-001"`

### Requirement: Authenticated flow actions appear in /api/log-actions

After triggering an authenticated checkout and waiting for the Kafka consumer to process, the smoke test SHALL be able to query `/api/log-actions` by flowId and confirm action records exist.

#### Scenario: Authenticated flow actions appear in /api/log-actions

- **WHEN** the Kafka consumer has processed authenticated checkout messages for `e2e-auth-flow-001`
- **AND** a GET request is sent to `/api/log-flows/e2e-auth-flow-001/actions` with a valid Bearer token
- **THEN** the response SHALL contain action items with `flowId = "e2e-auth-flow-001"`

### Requirement: Postman collection automates the smoke test flow

The Postman collection `docs/postman/skysim-logger-e2e.postman_collection.json` SHALL contain 10 requests that automate the full smoke test flow.

#### Scenario: Collection contains Get Keycloak Token request

- **WHEN** the Postman collection is imported
- **THEN** it SHALL contain a request named `1. Get Keycloak Token`
- **AND** the request SHALL POST to `{{keycloak_base_url}}/realms/{{keycloak_realm}}/protocol/openid-connect/token`
- **AND** the request body SHALL include `username`, `password`, `grant_type=password`, and `client_id={{keycloak_client_id}}`
- **AND** the request's Tests script SHALL save `access_token` to the `access_token` collection variable on a 200 response

#### Scenario: Collection contains health and auth-gated requests

- **WHEN** the Postman collection is imported
- **THEN** it SHALL contain a request named `2. Logger.Api Health Check` that GETs `{{logger_api_base_url}}/health`
- **AND** it SHALL contain a request named `3. Logger.Api Log Flows Without Token` that GETs `{{logger_api_base_url}}/api/log-flows` without an Authorization header and expects 401

#### Scenario: Collection contains guest and authenticated checkout requests

- **WHEN** the Postman collection is imported
- **THEN** it SHALL contain a request named `4. SampleService Guest Checkout` that POSTs to `{{sample_service_base_url}}/api/checkout/esim` with `X-Flow-Id: {{guest_flow_id}}` and no Authorization header
- **AND** it SHALL contain a request named `5. SampleService Authenticated Checkout` that POSTs to `{{sample_service_base_url}}/api/checkout/esim` with `X-Flow-Id: {{auth_flow_id}}` and `Authorization: Bearer {{access_token}}`

#### Scenario: Collection contains Logger.Api query requests

- **WHEN** the Postman collection is imported
- **THEN** it SHALL contain `6. Logger.Api Log Flows With Token` that GETs `{{logger_api_base_url}}/api/log-flows` with Bearer auth
- **AND** it SHALL contain `7. Logger.Api Query Guest Flow By FlowId` that GETs `{{logger_api_base_url}}/api/log-flows/{{guest_flow_id}}`
- **AND** it SHALL contain `8. Logger.Api Query Auth Flow By FlowId` that GETs `{{logger_api_base_url}}/api/log-flows/{{auth_flow_id}}`
- **AND** it SHALL contain `9. Logger.Api Query Guest Flow Actions` that GETs `{{logger_api_base_url}}/api/log-flows/{{guest_flow_id}}/actions`
- **AND** it SHALL contain `10. Logger.Api Query Auth Flow Actions` that GETs `{{logger_api_base_url}}/api/log-flows/{{auth_flow_id}}/actions`

#### Scenario: Collection requests use Postman test scripts

- **WHEN** the Postman collection is imported
- **THEN** each request SHALL include a `test` script that validates the response
- **AND** the `Get Keycloak Token` test SHALL assert that `access_token` is present and is a non-empty string
- **AND** the `SampleService Guest Checkout` test SHALL assert that `checkoutType` is `GUEST` and `flowId` equals `{{guest_flow_id}}`
- **AND** the `SampleService Authenticated Checkout` test SHALL assert that `checkoutType` is `AUTHENTICATED` and `flowId` equals `{{auth_flow_id}}`

### Requirement: Postman environment file provides all required variables

The Postman environment file `docs/postman/skysim-logger-local.postman_environment.json` SHALL define all variables required for the smoke test.

#### Scenario: Environment contains Keycloak variables

- **WHEN** the Postman environment is imported
- **THEN** it SHALL contain `keycloak_base_url = http://localhost:8081`
- **AND** `keycloak_realm = skysim`
- **AND** `keycloak_client_id = skysim-logger-api`
- **AND** `username = logger_admin`
- **AND** `password = admin123`

#### Scenario: Environment contains service base URLs

- **WHEN** the Postman environment is imported
- **THEN** it SHALL contain `logger_api_base_url = http://localhost:5108`
- **AND** `sample_service_base_url = http://localhost:5000`

#### Scenario: Environment contains flow ID variables

- **WHEN** the Postman environment is imported
- **THEN** it SHALL contain `guest_flow_id = e2e-guest-flow-001`
- **AND** `auth_flow_id = e2e-auth-flow-001`
- **AND** `access_token` (initially empty, populated at runtime)

### Requirement: Smoke test documentation includes Postman demo section

The `docs/smoke-test.md` SHALL include a "Postman Demo Flow" section with instructions for using the Postman collection.

#### Scenario: Postman demo section covers import and setup

- **WHEN** the smoke-test.md is read
- **THEN** it SHALL include instructions to import `skysim-logger-e2e.postman_collection.json` and `skysim-logger-local.postman_environment.json`
- **AND** it SHALL instruct users to update `sample_service_base_url` to the port printed by SampleService console

#### Scenario: Postman demo section covers run order

- **WHEN** the smoke-test.md is read
- **THEN** it SHALL describe the correct run order: Get Keycloak Token → other requests
- **AND** it SHALL instruct to wait for Kafka consumer processing before querying flows

#### Scenario: Postman demo section covers verification steps

- **WHEN** the smoke-test.md is read
- **THEN** it SHALL instruct to verify guest checkout response has `checkoutType: "GUEST"` and `flowId: "e2e-guest-flow-001"`
- **AND** it SHALL instruct to verify authenticated checkout response has `checkoutType: "AUTHENTICATED"` and `flowId: "e2e-auth-flow-001"`
- **AND** it SHALL instruct to verify query results contain both flow IDs

