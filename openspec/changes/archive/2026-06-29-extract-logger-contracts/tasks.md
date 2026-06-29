## 1. Setup Contracts Project

- [x] 1.1 Create `backend/Skysim.Logger.Contracts/` folder structure (`Events/`, `Constants/`, `DTOs/`)
- [x] 1.2 Create `Skysim.Logger.Contracts.csproj` as a .NET 8 class library with no NuGet dependencies
- [x] 1.3 Add `Skysim.Logger.Contracts` to the solution file
- [x] 1.4 Ensure `Skysim.Logger.Contracts` has no project references

## 2. Move Event Contract

- [x] 2.1 Move the existing `LogEventMessage.cs` to `Skysim.Logger.Contracts/Events/LogEventMessage.cs`
- [x] 2.2 Update namespace to `Skysim.Logger.Contracts.Events`
- [x] 2.3 Keep only the existing event fields and serialization attributes required by the current pipeline
- [x] 2.4 Do not add new helper methods such as `Deserialize(byte[])` unless they already exist and are currently used

## 3. Move/Create Shared Constants

- [x] 3.1 Move existing `KafkaTopics.cs` to `Skysim.Logger.Contracts/Constants`
- [x] 3.2 Move existing `StatusTypes.cs` to `Skysim.Logger.Contracts/Constants`
- [x] 3.3 Move existing `CheckoutTypes.cs` to `Skysim.Logger.Contracts/Constants` if it already exists or is currently used
- [x] 3.4 Move existing `FlowTypes.cs` to `Skysim.Logger.Contracts/Constants` only if it already exists or is currently used
- [x] 3.5 Move existing `SensitiveFieldNames.cs` to `Skysim.Logger.Contracts/Constants`
- [x] 3.6 Move existing `ActionTypes.cs` to `Skysim.Logger.Contracts/Constants` (currently in `Skysim.Logger.Common/Constants`)
- [x] 3.7 Create `HeaderNames.cs` for `X-Flow-Id`, `X-Correlation-Id`, and `X-Request-Id`
- [x] 3.8 Create `LogTypes.cs` for `HTTP` and `ACTION`
- [x] 3.9 Update namespaces to `Skysim.Logger.Contracts.Constants`
- [x] 3.10 Do not duplicate existing constants

## 4. Move Shared DTOs Only If Needed

- [x] 4.1 Move existing `PagedResponse.cs` to `Skysim.Logger.Contracts/DTOs` if it is shared by controllers/query services/tests
- [x] 4.2 Move existing `ApiErrorResponse.cs` to `Skysim.Logger.Contracts/DTOs` if it already exists and is reused
- [x] 4.3 Update namespaces to `Skysim.Logger.Contracts.DTOs`
- [x] 4.4 Do not create unused DTOs

## 5. Update Project References

- [x] 5.1 Add `Skysim.Logger.Contracts` project reference to `Skysim.Logger.Api.csproj`
- [x] 5.2 Add `Skysim.Logger.Contracts` project reference to `Skysim.Logger.Api.Tests.csproj`
- [x] 5.3 Add `Skysim.Logger.Contracts` project reference to `Skysim.Logger.Infrastructure.csproj` only if Infrastructure directly uses moved constants or models

## 6. Update Using Statements

- [x] 6.1 Update all Api usages of `LogEventMessage` to use `Skysim.Logger.Contracts.Events`
- [x] 6.2 Update all Api usages of moved constants to use `Skysim.Logger.Contracts.Constants`
- [x] 6.3 Update all Api usages of moved DTOs to use `Skysim.Logger.Contracts.DTOs`
- [x] 6.4 Update all test usages of moved types
- [x] 6.5 Remove old duplicate files only after all usages are updated

## 7. Verify Build and Tests

- [x] 7.1 Run `dotnet build` and verify success
- [x] 7.2 Run `dotnet test` and verify all tests pass
- [x] 7.3 Confirm the number of passing tests remains 163 unless tests are only renamed or moved
