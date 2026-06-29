# HTTP Logging Middleware Smoke Test

This document describes end-to-end verification steps to confirm the HTTP logging middleware and Kafka producer are working correctly.

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
