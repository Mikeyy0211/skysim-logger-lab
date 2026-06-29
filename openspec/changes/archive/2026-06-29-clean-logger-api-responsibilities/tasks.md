## 1. Pre-flight Check

- [x] 1.1 Verify `dotnet build` passes on current baseline
- [x] 1.2 Verify `dotnet test` passes (all 162 tests)
- [x] 1.3 Ensure working tree is clean (no pending changes)

## 2. Migrate Server-Side Kafka Helpers from Common to Api

- [x] 2.1 Create `backend/Skysim.Logger.Api/Kafka/` folder
- [x] 2.2 Use `git mv` to move `Common/Kafka/KafkaCommon.cs` → `Api/Kafka/KafkaCommon.cs`
- [x] 2.3 Update namespace in `Api/Kafka/KafkaCommon.cs` to `Skysim.Logger.Api.Kafka`
- [x] 2.4 Use `git mv` to move `Common/Kafka/RetryPolicyFactory.cs` → `Api/Kafka/RetryPolicyFactory.cs`
- [x] 2.5 Update namespace in `Api/Kafka/RetryPolicyFactory.cs` to `Skysim.Logger.Api.Kafka`
- [x] 2.6 Update usings in `DlqPublisher.cs` to reference `Api.Kafka` namespace
- [x] 2.7 Update usings in `KafkaLogConsumerService.cs` to reference `Api.Kafka` namespace
- [x] 2.8 Verify `dotnet build` passes
- [x] 2.9 Remove empty `Common/Kafka/` folder (should be empty after git mv)
- [x] 2.10 Verify `dotnet build` and `dotnet test` still pass

## 3. Reorganize Api Internal Structure

- [x] 3.1 Create `backend/Skysim.Logger.Api/Consumers/` folder
- [x] 3.2 Use `git mv` to move `Infrastructure/Kafka/KafkaLogConsumerService.cs` → `Consumers/`
- [x] 3.3 Update namespace in moved file to `Skysim.Logger.Api.Consumers`
- [x] 3.4 Use `git mv` to move `Infrastructure/Kafka/DlqPublisher.cs` → `Kafka/`
- [x] 3.5 Use `git mv` to move `Infrastructure/Kafka/KafkaConsumerOptions.cs` → `Kafka/`
- [x] 3.6 Use `git mv` to move `Contracts/DTOs/LogEventMessageValidator.cs` → `Validators/`
- [x] 3.7 Update namespace in moved validator file to `Skysim.Logger.Api.Validators`
- [x] 3.8 Create `backend/Skysim.Logger.Api/Contracts/Queries/` folder
- [x] 3.9 Use `git mv` to move `Contracts/DTOs/Queries/LogFlowListQuery.cs` → `Contracts/Queries/`
- [x] 3.10 Use `git mv` to move `Contracts/DTOs/Queries/LogActionListQuery.cs` → `Contracts/Queries/`
- [x] 3.11 Update namespaces in all moved query files
- [x] 3.12 Update all using statements in controllers, services, and Program.cs
- [x] 3.13 Remove empty `Infrastructure/Kafka/` folder
- [x] 3.14 Remove empty `Contracts/DTOs/Queries/` folder

## 4. Update Test Project References

- [x] 4.1 Update namespaces in `Skysim.Logger.Api.Tests` for any moved types
- [x] 4.2 Update using statements in test files referencing moved classes
- [x] 4.3 Verify `dotnet build` passes
- [x] 4.4 Verify `dotnet test` passes (all tests)

## 5. Cleanup Unused Common Project Files

- [x] 5.1 Verify `Common/Middleware/MiddlewareLogEntry.cs` is unused (grep for references)
- [x] 5.2 Use `git rm` to delete `Common/Middleware/MiddlewareLogEntry.cs`
- [x] 5.3 Remove empty `Common/Middleware/` folder
- [x] 5.4 Verify `dotnet build` and `dotnet test` pass

## 6. Verify Common Project Usage

- [x] 6.1 Check all references to `Skysim.Logger.Common` from Api.csproj and Infrastructure.csproj
- [x] 6.2 If no code uses Common after step 5 (Kafka helpers moved, MiddlewareLogEntry deleted):
  - Remove project reference from `Skysim.Logger.Api.csproj`
  - Remove project reference from `Skysim.Logger.Infrastructure.csproj` if present
- [x] 6.3 Verify `dotnet build` and `dotnet test` pass after removing references
- [x] 6.4 Do NOT delete the Common project itself (leave for future use or explicit approval)

## 7. Final Verification

- [x] 7.1 Run `dotnet build` - must pass with no errors
- [x] 7.2 Run `dotnet test` - all 162 tests must pass
- [x] 7.3 Verify target folder structure matches design
- [x] 7.4 Run `openspec validate clean-logger-api-responsibilities --strict`
