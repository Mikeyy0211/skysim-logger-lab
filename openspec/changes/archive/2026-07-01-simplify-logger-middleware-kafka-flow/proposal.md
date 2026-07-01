## Why

The current `LoggerMiddleware` implementation is more complex than needed. PM feedback indicates that the core requirement is simple: read HTTP context and publish to Kafka. The middleware should not understand business logic, decide checkout steps, or create business action sequences. It should only capture HTTP request/response context and publish it.

## What Changes

1. **Simplify the message contract** - Flatten `LogEventMessage` to include HTTP fields directly instead of nested `RequestData`/`ResponseData` objects
2. **Simplify masking** - Mask sensitive data directly in the JSON without round-trip serialization/deserialization
3. **Reduce helper methods** - Consolidate `BuildRequestData` and `BuildResponseData` into simpler inline logic
4. **Keep core behavior** - FlowId extraction, request/response capture, error handling, Kafka publishing remain unchanged
5. **Remove unnecessary complexity** - Simplify the builder pattern used in `BuildLogEventMessage`

## Capabilities

### New Capabilities
None - this is a refactoring of existing functionality

### Modified Capabilities
- `logger-client`: Simplify the middleware to focus on HTTP context logging only. Remove complex nested structures and simplify the masking approach.

## Impact

**Files affected:**
- `backend/Skysim.Logger.Client/Middlewares/LoggerMiddleware.cs` - Main refactoring target
- `backend/Skysim.Logger.Contracts/Events/LogEventMessage.cs` - May need field flattening
- `backend/Skysim.Logger.Client/Masking/SensitiveDataMasker.cs` - May need simplification

**No changes to:**
- `Skysim.Logger.Api` consumer/persistence/query APIs
- Frontend code
- `BusinessActionLogger` in SampleService
- Database schema
