#!/bin/bash
# Smoke Test Script for Skysim Logger API
# Run this script after starting docker-compose and the API

set -e

API_BASE="http://localhost:5000"
KAFKA_UI="http://localhost:8090"

echo "=========================================="
echo "Skysim Logger - Smoke Test"
echo "=========================================="

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

pass() { echo -e "${GREEN}✓ $1${NC}"; }
fail() { echo -e "${RED}✗ $1${NC}"; exit 1; }
warn() { echo -e "${YELLOW}⚠ $1${NC}"; }

# Check prerequisites
echo ""
echo "1. Checking prerequisites..."

if ! curl -s "$API_BASE/health" > /dev/null 2>&1; then
    fail "API not running at $API_BASE"
fi
pass "API is running"

# Step 2: Trigger request with JWT simulation
echo ""
echo "2. Testing HTTP logging middleware with JWT..."

RESPONSE=$(curl -s -X POST "$API_BASE/api/log-flows" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: smoke-test-jwt-001" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJ1c2VyLTEyMyJ9.test" \
  -d '{"email":"test@example.com","password":"secret123","orderId":"ORD-SMOKE"}')

if echo "$RESPONSE" | grep -q "items\|error"; then
    pass "Request accepted by API"
else
    warn "API returned unexpected response"
fi

# Step 3: Trigger health check
echo ""
echo "3. Testing health endpoint..."

HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$API_BASE/health")
if [ "$HTTP_CODE" = "200" ]; then
    pass "Health check returns 200"
else
    fail "Health check failed with $HTTP_CODE"
fi

# Step 4: Verify X-Correlation-ID header
echo ""
echo "4. Testing Correlation ID generation..."

CORR_ID=$(curl -s -D - "$API_BASE/health" -o /dev/null | grep -i "x-correlation-id:" | cut -d' ' -f2 | tr -d '\r')
if [ -n "$CORR_ID" ]; then
    pass "Correlation ID generated: $CORR_ID"
else
    warn "No Correlation ID in response"
fi

# Step 5: Test log-flows query (should be excluded from logging)
echo ""
echo "5. Testing excluded paths..."

LOGS_RESPONSE=$(curl -s "$API_BASE/api/log-flows?page=1&pageSize=10")
if echo "$LOGS_RESPONSE" | grep -q "items\|totalItems"; then
    pass "Log flows API works"
else
    warn "Log flows API returned unexpected response"
fi

# Step 6: Query database for logged events
echo ""
echo "6. Checking database for logged events..."

PG_RESULT=$(docker exec skysim-postgres psql -U skysim -d skysim_logger -t -c \
  "SELECT COUNT(*) FROM log_actions WHERE correlation_id IN ('smoke-test-jwt-001', '$CORR_ID');" 2>/dev/null || echo "0")

if [ -n "$PG_RESULT" ] && [ "$PG_RESULT" -gt 0 ]; then
    pass "Found $PG_RESULT logged actions in database"
else
    warn "No logged actions found yet (might be delay)"
fi

# Step 7: Check sensitive field masking
echo ""
echo "7. Checking sensitive field masking..."

# Note: This requires the consumer to be running and processing messages
# The actual verification is done via SQL query in the smoke-test.md

# Summary
echo ""
echo "=========================================="
echo "Smoke Test Complete"
echo "=========================================="
echo ""
echo "To verify in database:"
echo "  docker exec -it skysim-postgres psql -U skysim -d skysim_logger"
echo ""
echo "SQL to verify logged HTTP actions:"
echo "  SELECT flow_id, action_type, status, user_id FROM log_actions WHERE action_type = 'HTTP_REQUEST' ORDER BY created_at DESC LIMIT 5;"
echo ""
echo "SQL to verify sensitive data masking:"
echo "  SELECT lad.request_payload FROM log_actions la JOIN log_action_details lad ON lad.action_id = la.id WHERE la.action_type = 'HTTP_REQUEST' ORDER BY la.created_at DESC LIMIT 1;"
echo ""
echo "SQL to verify userId extraction from JWT:"
echo "  SELECT user_id FROM log_actions WHERE user_id IS NOT NULL ORDER BY created_at DESC LIMIT 5;"
echo ""
