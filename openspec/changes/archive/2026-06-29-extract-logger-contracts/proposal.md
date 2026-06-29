## Why

Shared logger models and constants are currently scattered across `Skysim.Logger.Api` and partially in `Skysim.Logger.Common`. Before extracting `Logger.Client` and `SampleService`, we need a dependency-free contracts layer that can be referenced by Api, Client, tests, and SampleService without circular dependencies or coupling to infrastructure.

## What Changes

- Create new `Skysim.Logger.Contracts` project as a .NET 8 class library with no external dependencies
- Move `LogEventMessage.cs` to `Skysim.Logger.Contracts/Events/`
- Move/create shared constants to `Skysim.Logger.Contracts/Constants/`
- Move `PagedResponse.cs` and `ApiErrorResponse.cs` to `Skysim.Logger.Contracts/DTOs/`
- Update namespaces and project references across Api and Tests
- Keep all build and test targets green

## Capabilities

### New Capabilities

- `logger-contracts`: Define the shared contracts layer for the SkySim Logger module, including event models, constants, and shared DTOs. This capability establishes the dependency-free foundation for all logger-related projects.

### Modified Capabilities

- None. Existing capabilities are not changing their requirements; this is a structural refactoring.

## Impact

- **New Project**: `Skysim.Logger.Contracts` added to solution
- **Modified Projects**: `Skysim.Logger.Api`, `Skysim.Logger.Api.Tests` will reference Contracts
- **Constants Moved**: `ActionTypes`, `CheckoutTypes`, `FlowTypes`, `KafkaTopics`, `SensitiveFieldNames`, `StatusTypes` will be consolidated into Contracts
- **Event Model**: `LogEventMessage.cs` will be refactored to use Contracts-based enums/constants instead of Api enums
- **DTOs Moved**: `PagedResponse.cs`, `ApiErrorResponse.cs` moved to Contracts
- **No Breaking API Changes**: Internal implementation only; REST APIs remain unchanged
- **No Kafka Runtime Change**: Message format unchanged
- **No Frontend Impact**: Frontend unchanged
