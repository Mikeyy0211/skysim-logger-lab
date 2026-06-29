## Why

After Phase 2 (extract-logger-client), `Skysim.Logger.Client` now owns all client-side logging components (LoggerMiddleware, KafkaLogProducer, SensitiveDataMasker). However, `Skysim.Logger.Api` still contains leftover files and mixed responsibilities from its initial monolithic design. This cleanup will clarify the Logger API's role as the **server-side Logger Service** and establish a clean project structure that separates concerns clearly.

## What Changes

- **File relocations** within `Skysim.Logger.Api`:
  - Move `KafkaLogConsumerService.cs` from `Infrastructure/Kafka/` to `Consumers/`
  - Move `DlqPublisher.cs` from `Infrastructure/Kafka/` to `Kafka/`
  - Move `KafkaConsumerOptions.cs` from `Infrastructure/Kafka/` to `Kafka/`
  - Move `RetryPolicyFactory.cs` into `Kafka/` (if server-side only)
  - Move `LogEventMessageValidator.cs` from `Contracts/DTOs/` to `Validators/`
  - Move query parameter classes from `Contracts/DTOs/Queries/` to `Contracts/Queries/`

- **Namespace and using updates** after file moves

- **Empty folder cleanup** after file moves

- **Optional cleanup** (if confirmed unused via build/tests):
  - Evaluate `Skysim.Logger.Common/Kafka/KafkaCommon.cs` usage
  - Evaluate `Skysim.Logger.Common/Kafka/RetryPolicyFactory.cs` usage
  - Evaluate `Skysim.Logger.Common/Middleware/MiddlewareLogEntry.cs` for deletion

- **Preserve runtime behavior**:
  - Same API routes
  - Same Kafka consumer group and topic configuration
  - Same DLQ and retry behavior
  - Same persistence and idempotency logic
  - Same query API response shapes

## Capabilities

### New Capabilities

- `logger-api-responsibilities`: Define the explicit responsibilities and boundaries of `Skysim.Logger.Api` as the server-side Logger Service. This spec will replace any informal documentation about what belongs in the API project.

### Modified Capabilities

- None. This is a structural cleanup that preserves existing behavior. The `logger-client` spec remains unchanged.

## Impact

### Affected Code

| Component | Impact |
|-----------|--------|
| `Skysim.Logger.Api` | File relocations, namespace updates |
| `Skysim.Logger.Common` | Potential removal of server-side-only helpers (conditional) |
| `Skysim.Logger.Api.Tests` | Namespace/using updates if tests reference moved types |

### Dependencies

- `Skysim.Logger.Api` → `Skysim.Logger.Contracts` (unchanged)
- `Skysim.Logger.Api` → `Skysim.Logger.Infrastructure` (unchanged)
- `Skysim.Logger.Api` → `Skysim.Logger.Common` (may reduce or remove)
- `Skysim.Logger.Api` → `Skysim.Logger.Client` (no change, Client is independent)

### Build and Test

- `dotnet build` must pass after changes
- `dotnet test` must pass with all 162 tests
- No API contract changes

### Out of Scope

- Client library changes
- Frontend changes
- Database schema changes
- Kafka topic or message contract changes
- New features or behavior changes
