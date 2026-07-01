## Context

The current `LoggerMiddleware` in `Skysim.Logger.Client` has grown beyond the simple HTTP logging requirement. It includes:
- Complex nested `RequestData`/`ResponseData` structures
- Multiple helper methods for building JSON payloads
- Round-trip serialization for masking sensitive data
- Business-specific fields that belong in `BusinessActionLogger`, not the middleware

PM feedback indicates the middleware should only:
1. Read HTTP context
2. Publish to Kafka
3. Be simple enough for a junior developer to understand

## Goals / Non-Goals

**Goals:**
1. Simplify `LoggerMiddleware` to focus on HTTP context logging only
2. Flatten the message contract to include HTTP fields directly (method, path, queryString, statusCode, etc.)
3. Simplify the masking approach to avoid round-trip JSON serialization
4. Reduce helper methods and complexity
5. Keep the middleware focused and readable

**Non-Goals:**
- Do not modify `BusinessActionLogger` or business action logging logic
- Do not modify `Logger.Api` consumer/persistence/query APIs
- Do not add new UI features or frontend changes
- Do not add service-chain simulation or complex abstractions
- Do not add new major libraries
- Do not modify the database schema

## Decisions

### Decision 1: Flatten the message contract to include HTTP fields directly

**Choice:** Add HTTP-specific fields directly to `LogEventMessage`:
- `method` (string) - HTTP method (GET, POST, etc.)
- `path` (string) - Request path
- `queryString` (string?) - Query string without `?`
- `statusCode` (int) - Response status code
- `durationMs` (int) - Request duration in milliseconds
- `requestBody` (string?) - Masked request body
- `responseBody` (string?) - Masked response body
- `sourceService` (string?) - Source service from X-Source-Service header

**Rationale:** Instead of nested `RequestData` and `ResponseData` JsonElements, direct fields are easier to read, query, and index. The consumer can parse these fields directly without dealing with nested structures.

**Alternatives considered:**
- Keep the nested structure: Not chosen because it adds complexity in both the middleware and consumer
- Create a separate `HttpLogMessage` class: Not chosen because it adds another class to maintain

### Decision 2: Simplify masking to direct JSON manipulation

**Choice:** After building the message object, serialize and mask in one pass using string replacement or a simpler masker approach.

**Rationale:** The current approach serializes to JSON, masks, then deserializes back. This adds complexity and potential for errors. A simpler approach is to mask sensitive fields directly during JSON construction or use a straightforward string-based masker.

**Alternatives considered:**
- Keep the round-trip masking: Works but adds unnecessary complexity
- Use regex-based masking: Faster but harder to maintain for nested JSON
- Mask during JSON construction: Chosen approach - build message properties directly and serialize once

### Decision 3: Keep FlowId extraction logic but simplify the response header

**Choice:** Return `X-Flow-Id` header (not `X-Correlation-ID`) when middleware generates a new flowId.

**Rationale:** The PM requirement specifies `X-Flow-Id` for both reading and writing. Consistency with the header priority order (X-Flow-Id → X-Correlation-Id → X-Request-Id).

### Decision 4: Maximum body size limit

**Choice:** Set a maximum body size limit (e.g., 32KB) and store `"[too large]"` if exceeded.

**Rationale:** Large bodies can cause memory issues and are not useful for debugging. The limit keeps the middleware safe and focused.

## Risks / Trade-offs

[Risk] Consumer API compatibility with flattened fields
→ Mitigation: The consumer already handles `JsonElement?` types; direct string fields are simpler to query

[Risk] Removing `RequestData`/`ResponseData` nesting may break existing consumer parsing
→ Mitigation: This is a refactoring change; if Logger.Api is also updated, the consumer needs to read the new field names

[Trade-off] Simplifying masking vs comprehensive field coverage
→ The simplified masker focuses on top-level sensitive fields; deeply nested sensitive fields may not be masked. Acceptable trade-off for middleware simplicity.

## Migration Plan

1. Create a new simplified `HttpLogMessage` class alongside existing `LogEventMessage` (or flatten `LogEventMessage` directly)
2. Refactor `LoggerMiddleware` to use the simplified message structure
3. Test that Kafka consumer in `Logger.Api` can process the new message format
4. Update consumer parsing if needed (minimal changes only)
5. Deploy and verify logs appear correctly in Kafka UI

## Open Questions

1. Should we keep backward compatibility with nested `RequestData`/`ResponseData` or is a breaking change acceptable for this refactoring?
2. What is the exact maximum body size limit? (Propose 32KB as a reasonable default)
