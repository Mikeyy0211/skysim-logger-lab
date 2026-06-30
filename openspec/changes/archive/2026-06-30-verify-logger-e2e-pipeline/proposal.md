# Proposal: verify-logger-e2e-pipeline

## Why

Manual verification of the Logger pipeline requires navigating multiple documentation sources, running commands in the right sequence, and manually confirming results. Developers need a single, repeatable smoke test that proves the entire flow works locally — from HTTP request to PostgreSQL persistence — without needing to understand the full architecture or run ad-hoc queries.

## What Changes

- Add an idempotent Kafka topic creation script (`scripts/create-kafka-topics.sh`) that creates `skysim.action.logs` and `skysim.action.logs.dlq` if they do not exist.
- Update `docs/smoke-test.md` to include a structured end-to-end smoke test that covers:
  - Starting infrastructure (Docker Compose)
  - Ensuring Kafka topics exist
  - Starting Logger.Api (port 5108) and SampleService
  - Obtaining a Keycloak token
  - Sending a guest checkout request to SampleService (`POST /api/checkout/esim` without Authorization header)
  - Sending an authenticated checkout request to SampleService (`POST /api/checkout/esim` with Authorization header)
  - Querying Logger.Api authenticated endpoints (`/api/log-flows`) with Bearer token
  - Confirming the explicit flow IDs appear in query results
  - Confirming actions can be queried from `/api/log-actions`
- Add Postman collection and environment files for an easy, repeatable E2E demo:
  - `docs/postman/skysim-logger-e2e.postman_collection.json` — 10 requests covering the full smoke test flow
  - `docs/postman/skysim-logger-local.postman_environment.json` — environment with all required variables
- Add database verification SQL queries to smoke-test.md for direct PostgreSQL inspection.

## Capabilities

### New Capabilities

- `logger-e2e-smoke-test`: Structured, step-by-step smoke test for the complete Logger pipeline. Uses SampleService's single `POST /api/checkout/esim` endpoint for both guest and authenticated flows, driven by the presence or absence of an Authorization header. The test verifies HTTP logging through Kafka to PostgreSQL, and query API access with Keycloak Bearer token. Explicit flow IDs (`e2e-guest-flow-001`, `e2e-auth-flow-001`) make results easy to locate.
- `logger-e2e-postman-collection`: A ready-to-use Postman collection (`skysim-logger-e2e.postman_collection.json`) with 10 requests that automate the full smoke test flow. The collection includes tests that verify responses and automatically saves the Keycloak access token for chained requests.
- `logger-e2e-postman-environment`: A Postman environment file (`skysim-logger-local.postman_environment.json`) with all required variables, including Keycloak credentials, base URLs, and explicit flow IDs.

## Impact

- **Scripts**: New idempotent Kafka topic creation script.
- **Documentation**: Updated `docs/smoke-test.md` with complete E2E smoke test flow, Postman demo section, and database verification queries.
- **Postman**: New collection (`docs/postman/skysim-logger-e2e.postman_collection.json`) and environment (`docs/postman/skysim-logger-local.postman_environment.json`) files.
- **Specs**: New spec `logger-e2e-smoke-test` captures requirements for the smoke test steps and expected outcomes.
- **Existing Specs**: `logger-api-auth` spec remains the source of truth for JWT/Bearer token behavior on Logger.Api query endpoints. This change adds no new requirements to existing capabilities.
