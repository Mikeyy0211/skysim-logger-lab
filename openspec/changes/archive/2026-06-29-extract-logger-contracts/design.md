## Context

The SkySim Logger module currently has shared constants (`ActionTypes`, `KafkaTopics`, etc.) in `Skysim.Logger.Common` and event models (`LogEventMessage`) in `Skysim.Logger.Api/Contracts/DTOs`. The `LogEventMessage` class references enums (`ActionType`, `FlowType`, `Status`, `CheckoutType`) defined inside `Skysim.Logger.Api/Domain/Enums`, creating a tight coupling.

Before we can create `Skysim.Logger.Client` (for services that want to emit logs) and `Skysim.Logger.SampleService` (reference implementation), we need a contracts layer that:
- Has no dependencies on Api, Infrastructure, or Client projects
- Can be referenced by all parties (Api, Tests, Client, SampleService)
- Contains only pure data models, constants, and shared DTOs

## Goals / Non-Goals

**Goals:**
- Establish `Skysim.Logger.Contracts` as the dependency-free foundation for all logger-related projects
- Consolidate event models (`LogEventMessage`), constants (`ActionTypes`, `KafkaTopics`, etc.), and shared DTOs (`PagedResponse`, `ApiErrorResponse`) into Contracts
- Ensure Contracts can be used by Api, Tests, Client, and SampleService without circular references
- Keep build and tests green throughout the transition

**Non-Goals:**
- Do NOT move `LoggerMiddleware` from Api
- Do NOT move `KafkaLogProducer` or `IKafkaLogProducer` from Api
- Do NOT move `KafkaLogConsumerService` from Infrastructure
- Do NOT move `SensitiveDataMasker` from Common
- Do NOT create `Skysim.Logger.Client` (future change)
- Do NOT create `Skysim.Logger.SampleService` (future change)
- Do NOT add Options or Extensions
- Do NOT add `BusinessActionFilter` or `BusinessLogPublisher`
- Do NOT add `CheckoutBusinessEvent`
- Do NOT change database schema or Kafka runtime behavior

## Decisions

### Decision 1: Use .NET 8 class library with zero external dependencies

**Chosen approach:** `Skysim.Logger.Contracts` has no NuGet dependencies.

**Rationale:** Contracts must be dependency-free to avoid introducing transitive dependencies on Kafka clients, EF Core, Polly, etc. into Client and SampleService. If we need JSON serialization support later, we can add `System.Text.Json` which ships with .NET 8 baseline.

**Alternatives considered:**
- Reuse `Skysim.Logger.Common` as contracts layer: Rejected because Common already depends on `Confluent.Kafka`, `Polly`, and `Npgsql`, which we do not want to force onto Client consumers.
- Add contracts to Infrastructure: Rejected because Infrastructure depends on EF Core and should not be referenced by Client.

### Decision 2: Move string-based constants from Common into Contracts, keep enums in Api

**Chosen approach:** `Skysim.Logger.Contracts/Constants/` will contain static string constants (`ActionTypes`, `CheckoutTypes`, `FlowTypes`, `KafkaTopics`, `SensitiveFieldNames`, `StatusTypes`). A new `HeaderNames.cs` and `LogTypes.cs` will also be added.

**Rationale:** String constants are inherently dependency-free and can be shared everywhere. Enums (`ActionType`, `FlowType`, `Status`, `CheckoutType`) can remain in `Skysim.Logger.Api/Domain/Enums` for now, with mapping utilities in Api as needed. This avoids breaking the existing `LogEventMessage` enum properties (which will be addressed by using string-based approach or separate simple enums in Contracts if needed).

**Alternative considered:**
- Move all enums to Contracts: Would require significant refactoring of existing Api code that uses those enums. Deferred to a future change.

### Decision 3: Move `PagedResponse` and `ApiErrorResponse` to Contracts

**Chosen approach:** Both DTOs have no dependencies on Api, Infrastructure, or external packages. They can be safely moved to `Skysim.Logger.Contracts/DTOs/`.

**Rationale:** These are generic response wrappers used across Api endpoints. Moving them to Contracts allows Client and SampleService to share the same response contract definitions.

### Decision 4: Move `LogEventMessage.cs` to Contracts

**Chosen approach:** `LogEventMessage` already has a static `Deserialize(byte[])` method and `JsonOptions` for `System.Text.Json` deserialization. The model will be moved as-is to `Skysim.Logger.Contracts/Events/` with its existing serialization attributes.

**Rationale:** `LogEventMessage` is the primary Kafka message contract. It already has self-contained deserialization. Moving it to Contracts allows Consumer (in Infrastructure) and Client producers to use the same contract without referencing Api.

### Decision 5: Add new constants `HeaderNames` and `LogTypes`

**Chosen approach:** Create `Skysim.Logger.Contracts/Constants/HeaderNames.cs` and `Skysim.Logger.Contracts/Constants/LogTypes.cs`.

**Rationale:**
- `HeaderNames`: Standardized HTTP header name constants (e.g., `CorrelationId`, `X-Request-Id`) used by middleware and client code.
- `LogTypes`: Classification of log types (e.g., `Technical`, `Business`) for the logging system.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Introducing Contracts creates another project to maintain | Contracts is intentionally minimal and stable; changes are rare |
| Enum-to-string mismatch between Contracts constants and Api enums | Use string constants in Contracts; Api can map enums to strings at boundaries |
| Breaking existing code that references `Skysim.Logger.Api.Contracts` namespace | After moving files, update all `using` statements across solution |
| New project not included in solution file | Ensure `dotnet sln add` is part of implementation tasks |

## Migration Plan

1. Create `Skysim.Logger.Contracts` project structure and add to solution
2. Create all Contracts files in new project (Events, Constants, DTOs)
3. Add `Skysim.Logger.Contracts` project reference to `Skysim.Logger.Api`
4. Add `Skysim.Logger.Contracts` project reference to `Skysim.Logger.Api.Tests`
5. Update all `using` statements in Api and Tests that reference moved types
6. Verify `dotnet build` passes
7. Verify `dotnet test` passes (all 163 tests green)
8. (Optional) Update `Skysim.Logger.Common` to reference Contracts for shared constants

**Rollback:** Revert all file moves and namespace changes via git. Contracts project can be deleted if not needed.

## Open Questions

1. **Should we add `System.Text.Json` package explicitly to Contracts?** Currently using implicit .NET 8 references. Consider adding explicit package reference for clarity and to control versioning.
2. **Should we create a separate `Skysim.Logger.Contracts.Enums` folder with simple enums, or keep everything string-based?** Decision deferred; start with string constants, add enums if real need emerges.
3. **Should `Skysim.Logger.Common` be refactored to reference `Skysim.Logger.Contracts` for constants?** This would reduce duplication. Deferred to this or a follow-up change.
