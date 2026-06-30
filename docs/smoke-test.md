# HTTP Logging Middleware Smoke Test

> **Note:** For authentication testing, see [Local Keycloak Setup](local-keycloak-setup.md).

## Prerequisites

- Docker Compose running (`docker compose up -d`)
- Kafka broker accessible at `localhost:9092`
- PostgreSQL accessible at `localhost:5432`
- `skysim.action.logs` topic exists in Kafka

## Verification Steps

### 1. Start the API

```bash
cd backend/Skysim.Logger.Api
dotnet run --urls "http://localhost:5000"
```

### 2. Trigger a request with JWT (authenticated user)

```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: smoke-test-jwt-001" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ1c2VyLTk5OSJ9.test" \
  -d '{"email":"test@example.com","password":"secret123","orderId":"ORD-JWT-001"}'
```

**Expected:** After consumer processes this, `user_id` column should contain `user-999`.

### 3. Trigger a simple request without JWT (anonymous)

```bash
curl -X POST http://localhost:5000/api/log-flows \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: smoke-test-anon-001" \
  -d '{"email":"guest@example.com","orderId":"ORD-GUEST-001"}'
```

**Expected:** After consumer processes this, `user_id` column should be NULL.

### 4. Trigger a health check (no body, with correlation ID)

```bash
curl -v http://localhost:5000/health \
  -H "X-Correlation-ID: smoke-test-002"
```

### 4. Verify Kafka consumer processes the messages

Check that the Kafka consumer has picked up the HTTP-action log events and stored them in PostgreSQL.

#### Check log_actions table for HttpRequest records

```sql
SELECT event_id, flow_id, action_type, status, message, user_id, created_at
FROM log_actions
WHERE action_type = 'HTTP_REQUEST'
ORDER BY created_at DESC
LIMIT 10;
```

**Expected:** At least three records — JWT request, anonymous request, and health check. JWT request should have `user_id = 'user-999'`. Anonymous request should have `user_id = NULL`.

#### Check correlation IDs are preserved

```sql
SELECT event_id, correlation_id, message, user_id
FROM log_actions
WHERE correlation_id IN ('smoke-test-jwt-001', 'smoke-test-anon-001')
ORDER BY created_at DESC;
```

**Expected:** Both correlation IDs appear in the `correlation_id` column.

#### Verify userId extraction from JWT

```sql
SELECT user_id, COUNT(*) as count
FROM log_actions
WHERE user_id IS NOT NULL
GROUP BY user_id;
```

**Expected:** Shows `user-999` for authenticated requests.

#### Verify anonymous user has null userId

```sql
SELECT correlation_id, user_id
FROM log_actions
WHERE correlation_id = 'smoke-test-anon-001';
```

**Expected:** `user_id` is NULL.

#### Verify sensitive fields are masked in log_action_details

```sql
SELECT lad.request_payload, lad.response_payload
FROM log_actions la
JOIN log_action_details lad ON lad.action_id = la.id
WHERE la.action_type = 'HTTP_REQUEST'
  AND la.message LIKE '%POST%api/log-flows%'
ORDER BY la.created_at DESC
LIMIT 1;
```

**Expected:** The `request_payload` JSON contains `"email":"test@example.com"` (unmasked) but `"password":"***"` (masked). It should NOT contain `secret123`.

### 5. Verify X-Correlation-ID response header

```bash
curl -s -D - http://localhost:5000/health -o /dev/null | grep -i "x-correlation-id"
```

**Expected:** The response includes `X-Correlation-ID` header.

### 6. Verify a new correlation ID is generated when none is provided

```bash
curl -s -D - http://localhost:5000/health -o /dev/null | grep -i "x-correlation-id"
```

**Expected:** A new `Guid` is returned in the `X-Correlation-ID` response header.

### 7. Verify userId extraction from JWT

After running the JWT request, check if userId was captured:

```bash
# Via API
curl "http://localhost:5000/api/log-flows?userId=user-999"
```

**Expected:** Returns flows where userId matches.

### 8. Verify 5xx errors produce Failed status

```bash
# Trigger a deliberate error (non-existent endpoint will return 404, not 500)
# To test 500, temporarily add a throwing endpoint or check logs for a real error
curl -s http://localhost:5000/api/nonexistent-endpoint -o /dev/null -w "%{http_code}"
```

### 9. Verify application logs for publish failures (when Kafka is intentionally down)

1. Stop Kafka: `docker compose stop kafka`
2. Make a request: `curl http://localhost:5000/health`
3. Check application logs — should contain `Warning` entries about failed Kafka publish:

```
warn: Skysim.Logger.Api.Middlewares.LoggerMiddleware[0]
      Kafka publish failed for LogEventMessage. EventId=<guid>, CorrelationId=<id>
```

**Expected:** API returns `200 OK` to the client despite Kafka being down. No HTTP error is returned.

### 10. Tear down

```bash
docker compose down
```

## Troubleshooting

| Symptom | Likely Cause | Fix |
|---|---|---|
| No HTTP logs in DB | Kafka consumer not running | Check consumer service logs |
| Correlation ID missing | Middleware not registered | Check `Program.cs` middleware order |
| Sensitive data not masked | `SensitiveDataMasker` not registered | Check DI registration |
| API returns 500 | Producer constructor throws | Check Kafka broker is reachable |
| Messages not in Kafka | Producer config wrong | Check `appsettings.json` `Kafka:Producer` section |
| userId always NULL | JWT authentication not configured | Check `AddAuthentication()` in Program.cs |
| userId incorrect | Claim type mismatch | Check `ExtractUserId` claims order |

---

# End-to-End Pipeline Smoke Test

> **Note:** This test verifies the complete Logger pipeline from SampleService checkout HTTP request → Kafka → Logger.Api consumer → PostgreSQL → authenticated query API.

## Prerequisites

- Docker Compose running (`docker compose up -d`)
- Kafka broker accessible at `localhost:9092`
- PostgreSQL accessible at `localhost:5432`
- Keycloak accessible at `localhost:8081`
- Logger.Api and SampleService built (`dotnet build`)
- Scripts directory in PATH or run from repo root

## Step 1: Start Infrastructure

```bash
cd infra
docker compose up -d
```

Wait for all containers to be healthy:

```bash
docker compose ps
```

**Expected:** All services show "healthy" status.

## Step 2: Ensure Kafka Topics Exist

```bash
./scripts/create-kafka-topics.sh
```

**Expected:** Script exits with code 0. Topics `skysim.action.logs` and `skysim.action.logs.dlq` are ready.

## Step 3: Start Logger.Api

```bash
cd backend/Skysim.Logger.Api
dotnet run
```

**Expected:** Logger.Api starts on port 5108 (or as configured). Kafka consumer background service initializes.

## Step 4: Start SampleService

In a new terminal:

```bash
cd backend/Skysim.SampleService
dotnet run
```

**Expected:** SampleService starts. Note the port printed in console output (e.g., `http://localhost:5000` or `http://localhost:5100`).

## Step 5: Obtain Keycloak Token

```bash
./scripts/get-token.sh
```

**Expected:** Outputs a Bearer token string. Save it for use in subsequent steps.

Alternatively, obtain a token via curl:

```bash
TOKEN_RESPONSE=$(curl -s -X POST http://localhost:8081/realms/skysim/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=skysim-client" \
  -d "client_secret=skysim-secret" \
  -d "username=admin" \
  -d "password=admin")

TOKEN=$(echo $TOKEN_RESPONSE | jq -r '.access_token')
echo $TOKEN
```

## Step 6: Send Guest Checkout Request

Replace `<sample-service-port>` with the actual port from Step 4.

```bash
curl -X POST http://localhost:<sample-service-port>/api/checkout/esim \
  -H "Content-Type: application/json" \
  -H "X-Flow-Id: e2e-guest-flow-001" \
  -d '{"email":"guest@test.com","phone":"+1234567890"}'
```

**Expected Response:**
```json
{
  "flowId": "e2e-guest-flow-001",
  "checkoutType": "GUEST",
  "status": "SUCCESS",
  "message": "Checkout processed"
}
```

**Expected:** Logger.Client middleware publishes HTTP_REQUEST log event to Kafka.

## Step 7: Send Authenticated Checkout Request

```bash
curl -X POST http://localhost:<sample-service-port>/api/checkout/esim \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer <your-token>" \
  -H "X-Flow-Id: e2e-auth-flow-001" \
  -d '{"email":"auth@test.com","phone":"+9876543210","userId":"user-001"}'
```

**Expected Response:**
```json
{
  "flowId": "e2e-auth-flow-001",
  "checkoutType": "AUTHENTICATED",
  "status": "SUCCESS",
  "message": "Checkout processed"
}
```

**Expected:** Logger.Client middleware publishes HTTP_REQUEST log event to Kafka with the flowId.

## Step 8: Wait for Kafka Consumer Processing

```bash
echo "Waiting for Kafka consumer to process messages..."
sleep 5
```

**Note:** Processing time may vary based on consumer lag. Increase wait time if needed.

## Step 9: Query Guest Flow from Logger.Api

```bash
curl -X GET "http://localhost:5108/api/log-flows/e2e-guest-flow-001" \
  -H "Authorization: Bearer <your-token>"
```

**Expected Response:**
```json
{
  "flowId": "e2e-guest-flow-001",
  "flowType": "CHECKOUT_ESIM",
  "status": "Success",
  "checkoutType": null,
  "userId": null,
  ...
}
```

**Note:** `checkoutType` and `userId` may be `null` because Logger.Client middleware captures HTTP events (not business actions extracted from JWT claims). This is expected behavior for this smoke test.

## Step 10: Query Authenticated Flow from Logger.Api

```bash
curl -X GET "http://localhost:5108/api/log-flows/e2e-auth-flow-001" \
  -H "Authorization: Bearer <your-token>"
```

**Expected Response:** Contains a flow record with `flowId = "e2e-auth-flow-001"`.

## Step 11: Query Guest Flow Actions

```bash
curl -X GET "http://localhost:5108/api/log-flows/e2e-guest-flow-001/actions" \
  -H "Authorization: Bearer <your-token>"
```

**Expected Response:**
```json
{
  "items": [
    {
      "eventId": "...",
      "flowId": "e2e-guest-flow-001",
      "actionType": "HTTP_REQUEST",
      "status": "Success",
      "message": "POST /api/checkout/esim",
      ...
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 1,
  "totalPages": 1
}
```

## Step 12: Query Authenticated Flow Actions

```bash
curl -X GET "http://localhost:5108/api/log-flows/e2e-auth-flow-001/actions" \
  -H "Authorization: Bearer <your-token>"
```

**Expected Response:** Contains action items with `flowId = "e2e-auth-flow-001"`.

## Step 13: Verify Health Endpoint Without Token

```bash
curl -s http://localhost:5108/health
```

**Expected Response:**
```json
{"status":"Healthy"}
```

## Step 14: Verify Query Endpoints Require Token

```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:5108/api/log-flows
```

**Expected:** `401`

## Step 15: Tear Down

```bash
# Stop services (Ctrl+C in their terminals)
# Or stop all containers
docker compose -f infra/docker-compose.yml down
```

## Expected Outcomes Summary

| Step | Verification | Expected Result |
|------|--------------|-----------------|
| 1 | Infrastructure healthy | All containers "healthy" |
| 2 | Kafka topics created | Both topics ready, script exits 0 |
| 5 | Guest checkout response | `checkoutType: "GUEST"`, `flowId: "e2e-guest-flow-001"` |
| 7 | Authenticated checkout response | `checkoutType: "AUTHENTICATED"`, `flowId: "e2e-auth-flow-001"` |
| 9 | Guest flow in API | Flow with `flowId: "e2e-guest-flow-001"` in results |
| 10 | Auth flow in API | Flow with `flowId: "e2e-auth-flow-001"` in results |
| 11 | Guest flow actions | Action items with `flowId: "e2e-guest-flow-001"` |
| 12 | Auth flow actions | Action items with `flowId: "e2e-auth-flow-001"` |
| 13 | Health without token | `200 OK`, `{"status":"Healthy"}` |
| 14 | Query without token | `401 Unauthorized` |

## Troubleshooting

| Symptom | Likely Cause | Fix |
|---------|--------------|-----|
| Topics creation fails | Kafka broker not ready | Wait for Kafka to be healthy, then retry |
| Checkout returns 500 | SampleService can't reach Kafka | Check Kafka broker address in SampleService config |
| Token acquisition fails | Keycloak not initialized | Run `./scripts/setup-keycloak.sh` |
| Query returns 401 | Token expired or invalid | Obtain a fresh token via `./scripts/get-token.sh` |
| No flows in query results | Kafka consumer not running | Check Logger.Api console for consumer errors |
| No flows in query results | Consumer processing delay | Wait longer (10-15 seconds) and retry |
| Partial flows (only guest or only auth) | One of the checkout requests failed | Check SampleService logs and HTTP responses |
| `checkoutType` is null | Logger.Client captures HTTP, not business events | This is expected; HTTP middleware doesn't extract JWT claims |

## Quick Reference: All Commands

```bash
# 1. Start infra
cd infra && docker compose up -d && cd ..

# 2. Create topics
./scripts/create-kafka-topics.sh

# 3. Get token
TOKEN=$(./scripts/get-token.sh)

# 4. Guest checkout
curl -X POST http://localhost:<port>/api/checkout/esim \
  -H "Content-Type: application/json" \
  -H "X-Flow-Id: e2e-guest-flow-001" \
  -d '{"email":"guest@test.com"}'

# 5. Auth checkout
curl -X POST http://localhost:<port>/api/checkout/esim \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Flow-Id: e2e-auth-flow-001" \
  -d '{"email":"auth@test.com"}'

# 6. Query guest flow
curl -X GET "http://localhost:5108/api/log-flows/e2e-guest-flow-001" \
  -H "Authorization: Bearer $TOKEN"

# 7. Query auth flow
curl -X GET "http://localhost:5108/api/log-flows/e2e-auth-flow-001" \
  -H "Authorization: Bearer $TOKEN"
```

---

# Postman Demo Flow

> **Note:** For developers who prefer a GUI over curl commands, the Postman collection provides a fully automated alternative to the smoke test steps above.

## What is the Postman Collection?

The Postman collection (`docs/postman/skysim-logger-e2e.postman_collection.json`) contains 10 requests that automate the complete smoke test flow. Each request includes test scripts that validate responses automatically.

## Import Collection and Environment

1. Open Postman
2. Click **Import** button (top left)
3. Drag or select `docs/postman/skysim-logger-e2e.postman_collection.json`
4. Click **Import** again
5. Drag or select `docs/postman/skysim-logger-local.postman_environment.json`
6. In the top-right corner, select **Skysim Logger Local Development** from the environment dropdown

## Configure SampleService Base URL

After starting SampleService, note the port it prints in the console (e.g., `http://localhost:5000`).

In Postman:
1. Click the environment dropdown (top right)
2. Click the **Edit** button (eye icon)
3. Find `sample_service_base_url` and update the value to match your SampleService port
4. Click **Save**

## Run the Smoke Test in Postman

### Step 1: Run Get Keycloak Token

Select **1. Get Keycloak Token** and click **Send**.

**Expected:** 200 OK. The `access_token` variable is automatically saved.

### Step 2: Verify Logger.Api is Running

Select **2. Logger.Api Health Check** and click **Send**.

**Expected:** 200 OK, `{"status":"Healthy"}`

### Step 3: Verify Auth is Required

Select **3. Logger.Api Log Flows Without Token** and click **Send**.

**Expected:** 401 Unauthorized

### Step 4: Send Guest Checkout

Select **4. SampleService Guest Checkout** and click **Send**.

**Expected:** 200 OK, `checkoutType: "GUEST"`, `flowId: "e2e-guest-flow-001"`

### Step 5: Send Authenticated Checkout

Select **5. SampleService Authenticated Checkout** and click **Send**.

**Expected:** 200 OK, `checkoutType: "AUTHENTICATED"`, `flowId: "e2e-auth-flow-001"`

### Step 6: Query All Log Flows

Select **6. Logger.Api Log Flows With Token** and click **Send**.

**Expected:** 200 OK, paginated response with `items` array

### Step 7: Query Guest Flow Detail

Select **7. Logger.Api Query Guest Flow By FlowId** and click **Send**.

**Expected:** 200 OK, `flowId: "e2e-guest-flow-001"`

### Step 8: Query Auth Flow Detail

Select **8. Logger.Api Query Auth Flow By FlowId** and click **Send**.

**Expected:** 200 OK, `flowId: "e2e-auth-flow-001"`

### Step 9: Query Guest Flow Actions

Select **9. Logger.Api Query Guest Flow Actions** and click **Send**.

**Expected:** 200 OK, `items` array with action records where `flowId: "e2e-guest-flow-001"`

### Step 10: Query Auth Flow Actions

Select **10. Logger.Api Query Auth Flow Actions** and click **Send**.

**Expected:** 200 OK, `items` array with action records where `flowId: "e2e-auth-flow-001"`

## Optional: Run Collection via Newman CLI

Install Newman and run the collection from command line:

```bash
npm install -g newman
newman run docs/postman/skysim-logger-e2e.postman_collection.json \
  -e docs/postman/skysim-logger-local.postman_environment.json
```

---

# Database Verification Queries

> **Note:** These queries allow direct inspection of the PostgreSQL database. Connect using `psql` or a GUI tool like pgAdmin or DBeaver.

## Connect to PostgreSQL

```bash
docker exec -it skysim-postgres psql -U skysim -d skysim_logger
```

Or via local psql:

```bash
psql -h localhost -p 5432 -U skysim -d skysim_logger
```

## Query: All Test Flows in log_flows

```sql
SELECT
    id,
    flow_id,
    flow_type,
    checkout_type,
    status,
    customer_email,
    customer_phone,
    user_id,
    order_id,
    payment_id,
    total_steps,
    success_steps,
    failed_steps,
    last_action_type,
    last_message,
    started_at,
    completed_at,
    created_at,
    updated_at
FROM log_flows
WHERE flow_id IN ('e2e-guest-flow-001', 'e2e-auth-flow-001')
ORDER BY created_at DESC;
```

**Expected:** Two rows — one for `e2e-guest-flow-001` and one for `e2e-auth-flow-001`.

## Query: All Test Actions in log_actions

```sql
SELECT
    id,
    event_id,
    flow_id,
    step_order,
    service_name,
    action_type,
    status,
    message,
    error_code,
    error_message,
    user_id,
    correlation_id,
    request_time,
    response_time,
    duration_ms,
    created_at,
    updated_at
FROM log_actions
WHERE flow_id IN ('e2e-guest-flow-001', 'e2e-auth-flow-001')
ORDER BY created_at DESC;
```

**Expected:** Action records for both guest and authenticated checkout flows.

## Query: Test Action Details (Request/Response Payloads)

```sql
SELECT
    d.id,
    d.action_id,
    d.request_payload,
    d.response_payload,
    d.metadata,
    d.created_at
FROM log_action_details d
JOIN log_actions a ON d.action_id = a.id
WHERE a.flow_id IN ('e2e-guest-flow-001', 'e2e-auth-flow-001')
ORDER BY d.created_at DESC;
```

**Expected:** Detail records with `request_payload` and `response_payload` JSON columns.

## Query: Verify Sensitive Data is Masked

```sql
SELECT
    d.request_payload,
    d.response_payload
FROM log_action_details d
JOIN log_actions a ON d.action_id = a.id
WHERE a.flow_id IN ('e2e-guest-flow-001', 'e2e-auth-flow-001')
LIMIT 1;
```

**Expected:** The `request_payload` JSON should NOT contain raw passwords, tokens, or other sensitive fields. Sensitive fields should be masked (e.g., `"password": "***"`).
