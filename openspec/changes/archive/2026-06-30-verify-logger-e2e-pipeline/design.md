# Design: verify-logger-e2e-pipeline

## Context

The Logger module is built across multiple components: Docker Compose infrastructure (PostgreSQL, Kafka, Keycloak), Logger.Api (ASP.NET Core Web API with Kafka consumer on port 5108), and SampleService (checkout trigger with Logger.Client middleware). Previous phases added JWT auth guards to Logger.Api query endpoints and local Keycloak integration. The existing `docs/smoke-test.md` covers HTTP middleware logging but not the end-to-end pipeline from SampleService checkout to Logger.Api query.

This design formalizes the E2E smoke test into a step-by-step guide with helper scripts so developers can verify the entire pipeline without needing to understand the full architecture.

## Goals / Non-Goals

**Goals:**
- Provide a single, repeatable E2E smoke test that proves the pipeline works from HTTP request to PostgreSQL persistence via Kafka.
- Ensure Kafka topics are created idempotently before running the test.
- Make the smoke test junior-friendly with clear commands, expected outcomes, and explicit flow IDs.
- Use explicit, named flow IDs so test results are easy to locate in query results.
- Provide a Postman collection and environment so the E2E demo can be run without typing curl commands.

**Non-Goals:**
- No frontend integration.
- No KONG API gateway integration.
- No role-based authorization.
- No database schema changes.
- No changes to Logger.Contracts, Logger.Client behavior, or Kafka consumer persistence logic.
- No JWT validation or token parsing in SampleService (it simulates guest/auth via header presence only).
- No Docker images for Logger.Api or SampleService.
- No complex orchestration tooling.
- No Postman collection for business action logging (this phase focuses on HTTP middleware logging only).

## Decisions

### 1. Kafka topic creation via shell script with kafka-topics --create-if-not-exists

The Kafka broker supports `--if-not-exists` flag on topic creation. Using this, `scripts/create-kafka-topics.sh` runs `kafka-topics.sh` for each required topic with the `--if-not-exists` flag. This is simpler than programmatic Kafka AdminClient logic and works directly against the Docker Compose Kafka container.

**Alternative considered: idempotency via exit code** — Checking if a topic exists first, then creating. More error-prone than `--if-not-exists`. Rejected.

### 2. Smoke test documented in docs/smoke-test.md

`docs/smoke-test.md` is updated to include a dedicated "End-to-End Pipeline Smoke Test" section. This keeps all smoke test documentation in one place rather than scattering it across multiple files.

### 3. Explicit flow IDs for test clarity

The smoke test sends checkout requests with the `X-Flow-Id` header set to explicit values (`e2e-guest-flow-001` and `e2e-auth-flow-001`). This makes it easy to filter query results and confirm the test flow was persisted, without needing to inspect timestamps or raw Kafka messages.

### 4. Single SampleService endpoint for both guest and authenticated flows

SampleService exposes only `POST /api/checkout/esim`. Guest vs. authenticated is determined by the presence of an `Authorization` header in the request — SampleService does not validate the token. The response's `checkoutType` field confirms which mode was active.

### 5. Port configuration

- **Logger.Api**: `http://localhost:5108` (from launchSettings.json `http` profile).
- **SampleService**: use the port printed by the `dotnet run` console output, or check launchSettings.json if available. The smoke test documentation refers to `<logger-api-port>` and `<sample-service-port>` as placeholders that developers replace with their actual running ports.

### 6. Scope of persistence verification

The smoke test verifies that the explicit flow IDs appear in `/api/log-flows` query results. The persisted `log_flows` records for SampleService HTTP requests have `checkoutType = null` and `userId = null` (because Logger.Client middleware captures HTTP events, not business actions, and does not extract JWT claims). This is expected and correct behavior for this phase. The smoke test does not assert on `checkoutType` or `userId` in persisted records.

### 7. Postman collection for E2E demo without curl

The smoke test is available as a Postman collection (`docs/postman/skysim-logger-e2e.postman_collection.json`) containing 10 requests in order:

1. `Get Keycloak Token` — POST to Keycloak, saves `access_token` via a Tests script
2. `Logger.Api Health Check` — verifies Logger.Api is up
3. `Logger.Api Log Flows Without Token` — expects 401, confirms auth is required
4. `SampleService Guest Checkout` — POST without Authorization, expects `checkoutType: "GUEST"`
5. `SampleService Authenticated Checkout` — POST with Bearer token, expects `checkoutType: "AUTHENTICATED"`
6. `Logger.Api Log Flows With Token` — paginated flow list
7. `Logger.Api Query Guest Flow By FlowId` — `GET /api/log-flows/{{guest_flow_id}}`
8. `Logger.Api Query Auth Flow By FlowId` — `GET /api/log-flows/{{auth_flow_id}}`
9. `Logger.Api Query Guest Flow Actions` — `GET /api/log-flows/{{guest_flow_id}}/actions`
10. `Logger.Api Query Auth Flow Actions` — `GET /api/log-flows/{{auth_flow_id}}/actions`

Each request includes Postman test scripts that validate the response. The environment file (`docs/postman/skysim-logger-local.postman_environment.json`) contains all variables including Keycloak credentials (`username: logger_admin`, `password: admin123`), base URLs, and explicit flow IDs.

**Alternative considered: Newman CLI in CI** — Running the collection via Newman in CI was considered but deferred. This phase focuses on local developer/demo use only. Rejected for now.

### 8. Database verification SQL queries

Direct PostgreSQL inspection supplements the REST API queries. The smoke-test.md includes three DB verification queries:

- `SELECT * FROM log_flows WHERE flow_id IN ('e2e-guest-flow-001', 'e2e-auth-flow-001');` — confirms both flows are persisted
- `SELECT * FROM log_actions WHERE flow_id IN ('e2e-guest-flow-001', 'e2e-auth-flow-001');` — confirms HTTP action records exist
- `SELECT d.* FROM log_action_details d JOIN log_actions a ON d.action_id = a.id WHERE a.flow_id IN ('e2e-guest-flow-001', 'e2e-auth-flow-001');` — confirms payloads are stored

These queries are for developer verification only and do not change any production code or schema.

## Risks / Trade-offs

- **[Risk]** If Kafka topics already exist with different partition or replication settings, `--if-not-exists` will not update them. **Mitigation:** Document that topics should be created fresh or deleted first if reconfiguration is needed.
- **[Risk]** The smoke test assumes Logger.Api and SampleService are already built. **Mitigation:** Document the `dotnet build` step before running the test.
- **[Risk]** Timing: the Kafka consumer processes messages asynchronously, so there may be a short delay before the smoke test flow appears in query results. **Mitigation:** The smoke test documents a wait period (e.g., 3–5 seconds) after sending requests before querying.
- **[Risk]** If Keycloak is not fully initialized when the smoke test runs, token acquisition fails. **Mitigation:** Document that Keycloak must be healthy before starting. The existing `setup-keycloak.sh` script handles initialization.
- **[Risk]** Postman collection requires manual import. **Mitigation:** The collection and environment files are JSON-based and easy to import via Postman's import button. Instructions are included in smoke-test.md.
- **[Risk]** SampleService port may vary across machines or runs. **Mitigation:** The environment file uses a default port (5000) but documents that users must update `sample_service_base_url` based on the port printed by `dotnet run`.
- **[Risk]** Postman test assertions may fail if API response structure changes. **Mitigation:** Tests are simple and focused on key fields (`checkoutType`, `flowId`). They serve as smoke tests, not strict contract tests.
