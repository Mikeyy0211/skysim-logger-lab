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

### 2. Trigger a simple request with sensitive data

```bash
curl -X POST http://localhost:5000/api/log-flows \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: smoke-test-001" \
  -d '{"email":"test@example.com","password":"secret123","orderId":"ORD-SMOKE"}'
```

### 3. Trigger a health check (no body, with correlation ID)

```bash
curl -v http://localhost:5000/health \
  -H "X-Correlation-ID: smoke-test-002"
```

### 4. Verify Kafka consumer processes the messages

Check that the Kafka consumer has picked up the HTTP-action log events and stored them in PostgreSQL.

#### Check log_actions table for HttpRequest records

```sql
SELECT event_id, flow_id, action_type, status, message, created_at
FROM log_actions
WHERE action_type = 'HTTP_REQUEST'
ORDER BY created_at DESC
LIMIT 10;
```

**Expected:** At least two records — one for the POST to `/api/log-flows` and one for the GET to `/health`. Both should have `flow_type = 'HTTP_ACTION'` and `action_type = 'HTTP_REQUEST'`.

#### Check correlation IDs are preserved

```sql
SELECT event_id, correlation_id, message
FROM log_actions
WHERE correlation_id IN ('smoke-test-001', 'smoke-test-002')
ORDER BY created_at DESC;
```

**Expected:** Both correlation IDs appear in the `correlation_id` column.

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

### 7. Verify 5xx errors produce Failed status

```bash
# Trigger a deliberate error (non-existent endpoint will return 404, not 500)
# To test 500, temporarily add a throwing endpoint or check logs for a real error
curl -s http://localhost:5000/api/nonexistent-endpoint -o /dev/null -w "%{http_code}"
```

### 8. Verify application logs for publish failures (when Kafka is intentionally down)

1. Stop Kafka: `docker compose stop kafka`
2. Make a request: `curl http://localhost:5000/health`
3. Check application logs — should contain `Warning` entries about failed Kafka publish:

```
warn: Skysim.Logger.Api.Middlewares.LoggerMiddleware[0]
      Kafka publish failed for LogEventMessage. EventId=<guid>, CorrelationId=<id>
```

**Expected:** API returns `200 OK` to the client despite Kafka being down. No HTTP error is returned.

### 9. Tear down

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
