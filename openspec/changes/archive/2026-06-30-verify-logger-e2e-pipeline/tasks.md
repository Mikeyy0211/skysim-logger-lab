# Tasks: verify-logger-e2e-pipeline

## 1. Create Kafka topic creation script

- [x] 1.1 Create `scripts/create-kafka-topics.sh` with idempotent topic creation for `skysim.action.logs` and `skysim.action.logs.dlq` using `kafka-topics.sh --create --if-not-exists`
- [x] 1.2 Make the script executable (`chmod +x`)
- [x] 1.3 Verify the script exits with code 0 when topics already exist

## 2. Update smoke test documentation

- [x] 2.1 Add "End-to-End Pipeline Smoke Test" section to `docs/smoke-test.md` covering: starting infra, ensuring Kafka topics exist, starting Logger.Api (port 5108) and SampleService, obtaining Keycloak token, sending guest checkout request (POST /api/checkout/esim without Authorization header), sending authenticated checkout request (POST /api/checkout/esim with Authorization header and Bearer token), querying Logger.Api with Bearer token, confirming guest flow in results (flowId = e2e-guest-flow-001), confirming authenticated flow in results (flowId = e2e-auth-flow-001)
- [x] 2.2 Use explicit flow IDs `e2e-guest-flow-001` and `e2e-auth-flow-001` in all test commands
- [x] 2.3 Include expected outcomes for each smoke test step
- [x] 2.4 Add a troubleshooting section for common smoke test failures

## 3. Add Postman collection and environment files

- [x] 3.1 Create `docs/postman/skysim-logger-e2e.postman_collection.json` with 10 requests covering the full smoke test flow
- [x] 3.2 Create `docs/postman/skysim-logger-local.postman_environment.json` with all required variables (Keycloak credentials, base URLs, flow IDs)
- [x] 3.3 Ensure `Get Keycloak Token` request has a Tests script that saves `access_token` to the `access_token` collection variable
- [x] 3.4 Ensure collection requests have test scripts that validate responses (checkoutType, flowId assertions)
- [x] 3.5 Validate the JSON files are valid JSON and conform to Postman collection v2.1 schema

## 4. Update smoke test documentation with Postman demo and DB queries

- [x] 4.1 Add "Postman Demo Flow" section to `docs/smoke-test.md` with import instructions, run order, and verification steps
- [x] 4.2 Add database verification SQL queries section with: log_flows query, log_actions query, log_action_details query (all using flow IDs)
- [x] 4.3 Update the expected outcomes table and troubleshooting section as needed

## 5. Update OpenSpec artifacts

- [x] 5.1 Update proposal.md with Postman collection/environment deliverables
- [x] 5.2 Update design.md with Postman collection design decisions and risks
- [x] 5.3 Update spec.md with Postman collection and environment requirements
- [x] 5.4 Update tasks.md with all new tasks (Postman, smoke-test updates, OpenSpec updates)

## 6. Build and validate

- [x] 6.1 Run `dotnet build backend/Skysim.Logger.sln` and confirm it passes
- [x] 6.2 Run `dotnet test backend/Skysim.Logger.sln` and confirm all tests pass
- [x] 6.3 Run `openspec validate verify-logger-e2e-pipeline --strict` and confirm it passes
