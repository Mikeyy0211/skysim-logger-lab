## 1. Create New Folder Structure

- [x] 1.1 Create `Client/Middlewares/` folder
- [x] 1.2 Create `Client/Producers/` folder
- [x] 1.3 Create `Client/Masking/` folder
- [x] 1.4 Create `Consumers/` folder
- [x] 1.5 Create `Infrastructure/Repositories/` folder
- [x] 1.6 Create `Services/Domain/` folder
- [x] 1.7 Create `Services/Query/` folder
- [x] 1.8 Create `Validators/` folder

## 2. Move Root-Level Files to New Locations

- [x] 2.1 Move `SensitiveDataMaskerTests.cs` → `Client/Masking/SensitiveDataMaskerTests.cs` and update namespace to `Skysim.Logger.Api.Tests.Client.Masking`
- [x] 2.2 Move `KafkaLogConsumerServiceTests.cs` → `Consumers/KafkaLogConsumerServiceTests.cs` and update namespace to `Skysim.Logger.Api.Tests.Consumers`
- [x] 2.3 Move `KafkaLogConsumerServicePersistenceTests.cs` → `Consumers/KafkaLogConsumerServicePersistenceTests.cs` and update namespace to `Skysim.Logger.Api.Tests.Consumers`
- [x] 2.4 Move `LogEventMessageValidatorTests.cs` → `Validators/LogEventMessageValidatorTests.cs` and update namespace to `Skysim.Logger.Api.Tests.Validators`

## 3. Migrate Existing Subfolder Files

- [x] 3.1 Move `MiddlewareTests/LoggerMiddlewareTests.cs` → `Client/Middlewares/LoggerMiddlewareTests.cs` (namespace already correct as `Skysim.Logger.Api.Tests.Client.Middlewares`)
- [x] 3.2 Move `KafkaProducerTests/KafkaLogProducerTests.cs` → `Client/Producers/KafkaLogProducerTests.cs` (namespace already correct as `Skysim.Logger.Api.Tests.Client.Producers`)
- [x] 3.3 Move `RepositoryTests/LogActionRepositoryTests.cs` → `Infrastructure/Repositories/LogActionRepositoryTests.cs` and update namespace to `Skysim.Logger.Api.Tests.Infrastructure.Repositories`
- [x] 3.4 Move `RepositoryTests/LogFlowTerminalActionTests.cs` → `Services/Domain/LogFlowTerminalActionTests.cs` and update namespace to `Skysim.Logger.Api.Tests.Services.Domain`
- [x] 3.5 Move `QueryServiceTests/LogFlowListQueryValidatorTests.cs` → `Validators/LogFlowListQueryValidatorTests.cs` and update namespace to `Skysim.Logger.Api.Tests.Validators`
- [x] 3.6 Move `QueryServiceTests/LogActionListQueryValidatorTests.cs` → `Validators/LogActionListQueryValidatorTests.cs` and update namespace to `Skysim.Logger.Api.Tests.Validators`
- [x] 3.7 Move `QueryServiceTests/LogFlowQueryServiceTests.cs` → `Services/Query/LogFlowQueryServiceTests.cs` (namespace already correct as `Skysim.Logger.Api.Tests.Services.Query`)
- [x] 3.8 Move `QueryServiceTests/LogActionQueryServiceTests.cs` → `Services/Query/LogActionQueryServiceTests.cs` (namespace already correct as `Skysim.Logger.Api.Tests.Services.Query`)

## 4. Remove Old Empty Folders

- [x] 4.1 Delete `MiddlewareTests/` folder (after step 3.1)
- [x] 4.2 Delete `KafkaProducerTests/` folder (after step 3.2)
- [x] 4.3 Delete `RepositoryTests/` folder (after steps 3.3 and 3.4)
- [x] 4.4 Delete `QueryServiceTests/` folder (after steps 3.5 through 3.8)

## 5. Verify Build and Tests

- [x] 5.1 Run `dotnet build backend/Skysim.Logger.sln` and verify no build errors
- [x] 5.2 Run `dotnet test backend/Skysim.Logger.sln` and verify all 162 tests pass
- [x] 5.3 Run `openspec validate reorganize-backend-tests --strict` and verify OpenSpec passes
