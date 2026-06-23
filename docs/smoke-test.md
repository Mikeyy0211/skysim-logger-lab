# Local Smoke Test Guide

This guide documents manual smoke tests for the Kafka Consumer and PostgreSQL persistence layer.

## Prerequisites

- Docker and Docker Compose installed
- .NET 8 SDK installed
- PostgreSQL running on port 5432
- Kafka running on port 9092

## Infrastructure Setup

### Start Infrastructure

```bash
cd infra
docker compose up -d
```

### Verify Services

```bash
# Check PostgreSQL
docker exec skysim-postgres pg_isready -U skysim

# Check Kafka
docker exec skysim-kafka kafka-broker-api-versions --bootstrap-server localhost:9092
```

## Database Setup

### Apply Migrations

```bash
cd backend/Skysim.Logger.Api
dotnet ef database update --connection "Host=localhost;Port=5432;Database=skysim_logger;Username=skysim;Password=skysim_password"
```

Expected output: `Applying migration 'InitialCreate'...`

## Application Startup

### Start the API

```bash
cd backend/Skysim.Logger.Api
dotnet run
```

Expected logs within 10 seconds:
- `Starting KafkaLogConsumerService`
- `Subscribed to topic: skysim.action.logs`

## Smoke Tests

### 1. Test Valid Message Persistence

Produce a valid message to Kafka:

```bash
docker exec skysim-kafka kafka-console-producer \
  --bootstrap-server localhost:9092 \
  --topic skysim.action.logs \
  --property "parse.key=true" \
  --property "key.separator=:"
```

Type the following message (press Enter):

```
test-flow-001:{"eventId":"11111111-1111-1111-1111-111111111111","flowId":"test-flow-001","flowType":"CHECKOUT_ESIM","serviceName":"Order","actionType":"ORDER_CREATED","status":"SUCCESS","createdAt":"2026-06-23T08:00:00.000Z","checkoutType":"GUEST","customerEmail":"test@example.com","orderId":"ORD-001","message":"Order created successfully"}
```

Press Ctrl+C to exit.

### 2. Verify Database Records

Query PostgreSQL:

```bash
docker exec -i skysim-postgres psql -U skysim -d skysim_logger -c "SELECT flow_id, flow_type, status, total_steps FROM log_flows;"
```

Expected: 1 row with `test-flow-001`, `CHECKOUT_ESIM`, `Success`, `1`

```bash
docker exec -i skysim-postgres psql -U skysim -d skysim_logger -c "SELECT event_id, flow_id, action_type, status FROM log_actions;"
```

Expected: 1 row with `11111111-1111-1111-1111-111111111111`, `ORDER_CREATED`, `Success`

### 3. Test Duplicate Message (Idempotency)

Produce the same message again:

```
test-flow-001:{"eventId":"11111111-1111-1111-1111-111111111111","flowId":"test-flow-001","flowType":"CHECKOUT_ESIM","serviceName":"Order","actionType":"ORDER_CREATED","status":"SUCCESS","createdAt":"2026-06-23T08:00:00.000Z","checkoutType":"GUEST","customerEmail":"test@example.com","orderId":"ORD-001","message":"Order created successfully"}
```

Verify no duplicate in `log_actions`:

```bash
docker exec -i skysim-postgres psql -U skysim -d skysim_logger -c "SELECT COUNT(*) FROM log_actions;"
```

Expected: 1 (no new row inserted)

### 4. Test Invalid Message (Validation Failure)

Produce a message with invalid status:

```
test-flow-002:{"eventId":"22222222-2222-2222-2222-222222222222","flowId":"test-flow-002","flowType":"CHECKOUT_ESIM","serviceName":"Order","actionType":"ORDER_CREATED","status":"WEIRD_STATUS","createdAt":"2026-06-23T08:00:00.000Z"}
```

Verify:
- No rows created in `log_flows` or `log_actions` for `test-flow-002`
- Consumer logs show validation error
- Consumer did not crash

### 5. Test Parse Error (Malformed JSON)

```bash
docker exec skysim-kafka kafka-console-producer \
  --bootstrap-server localhost:9092 \
  --topic skysim.action.logs
```

Type invalid JSON:

```
test-flow-003:not valid json at all
```

Press Ctrl+C.

Verify:
- No rows created
- Consumer logs show deserialization error
- Consumer did not crash

## Expected Structured Log Output

For persisted messages, expect structured logs with:

```
{eventId, flowId, actionType, status, durationMs, outcome}
```

Example:
```
Message persisted successfully. EventId=11111111-1111-1111-1111-111111111111, FlowId=test-flow-001, ActionType=ORDER_CREATED, Status=Success
```

For duplicates:
```
Duplicate event detected (idempotent skip). EventId=11111111-1111-1111-1111-111111111111
```

For DLQ:
```
Max retry attempts reached, publishing to DLQ. Reason=VALIDATION_FAILED: ..., MaxAttempts=5
```

## Cleanup

```bash
# Stop infrastructure
cd infra
docker compose down

# Clean up test data (optional)
docker exec -i skysim-postgres psql -U skysim -d skysim_logger -c "TRUNCATE log_actions, log_flows, log_action_details CASCADE;"
```
