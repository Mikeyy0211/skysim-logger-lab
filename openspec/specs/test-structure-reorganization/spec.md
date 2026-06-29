# test-structure-reorganization Specification

## Purpose
TBD - created by archiving change reorganize-backend-tests. Update Purpose after archive.
## Requirements
### Requirement: Test files shall be organized by tested component

All test files in `Skysim.Logger.Api.Tests` SHALL be organized into subfolders that reflect the architecture of the solution being tested.

#### Scenario: Middleware tests are in Client/Middlewares folder

- **GIVEN** `LoggerMiddlewareTests.cs` exists in the test project
- **WHEN** the test project structure is examined
- **THEN** the file SHALL be located at `Client/Middlewares/LoggerMiddlewareTests.cs`
- **AND** the namespace SHALL be `Skysim.Logger.Api.Tests.Client.Middlewares`

#### Scenario: Producer tests are in Client/Producers folder

- **GIVEN** `KafkaLogProducerTests.cs` exists in the test project
- **WHEN** the test project structure is examined
- **THEN** the file SHALL be located at `Client/Producers/KafkaLogProducerTests.cs`
- **AND** the namespace SHALL be `Skysim.Logger.Api.Tests.Client.Producers`

#### Scenario: Sensitive data masking tests are in Client/Masking folder

- **GIVEN** `SensitiveDataMaskerTests.cs` exists in the test project
- **WHEN** the test project structure is examined
- **THEN** the file SHALL be located at `Client/Masking/SensitiveDataMaskerTests.cs`
- **AND** the namespace SHALL be `Skysim.Logger.Api.Tests.Client.Masking`

#### Scenario: Consumer tests are in Consumers folder

- **GIVEN** `KafkaLogConsumerServiceTests.cs` exists in the test project
- **WHEN** the test project structure is examined
- **THEN** the file SHALL be located at `Consumers/KafkaLogConsumerServiceTests.cs`
- **AND** the namespace SHALL be `Skysim.Logger.Api.Tests.Consumers`

#### Scenario: Consumer persistence tests are in Consumers folder

- **GIVEN** `KafkaLogConsumerServicePersistenceTests.cs` exists in the test project
- **WHEN** the test project structure is examined
- **THEN** the file SHALL be located at `Consumers/KafkaLogConsumerServicePersistenceTests.cs`
- **AND** the namespace SHALL be `Skysim.Logger.Api.Tests.Consumers`

#### Scenario: Repository tests are in Infrastructure/Repositories folder

- **GIVEN** repository test files exist in the test project
- **WHEN** the test project structure is examined
- **THEN** `LogActionRepositoryTests.cs` SHALL be located at `Infrastructure/Repositories/LogActionRepositoryTests.cs`
- **AND** the namespace SHALL be `Skysim.Logger.Api.Tests.Infrastructure.Repositories`

#### Scenario: Domain service tests are in Services/Domain folder

- **GIVEN** `LogFlowTerminalActionTests.cs` tests `FlowDomainService.IsTerminalAction`
- **WHEN** the test project structure is examined
- **THEN** the file SHALL be located at `Services/Domain/LogFlowTerminalActionTests.cs`
- **AND** the namespace SHALL be `Skysim.Logger.Api.Tests.Services.Domain`

#### Scenario: Query service tests are in Services/Query folder

- **GIVEN** query service test files exist in the test project
- **WHEN** the test project structure is examined
- **THEN** the files SHALL be located in `Services/Query/` folder
- **AND** the namespace SHALL be `Skysim.Logger.Api.Tests.Services.Query`

#### Scenario: Query validator tests are in Validators folder

- **GIVEN** query validator test files exist in the test project
- **WHEN** the test project structure is examined
- **THEN** `LogFlowListQueryValidatorTests.cs` SHALL be located at `Validators/LogFlowListQueryValidatorTests.cs`
- **AND** `LogActionListQueryValidatorTests.cs` SHALL be located at `Validators/LogActionListQueryValidatorTests.cs`
- **AND** the namespace SHALL be `Skysim.Logger.Api.Tests.Validators`

#### Scenario: LogEventMessage validator tests are in Validators folder

- **GIVEN** `LogEventMessageValidatorTests.cs` exists in the test project
- **WHEN** the test project structure is examined
- **THEN** the file SHALL be located at `Validators/LogEventMessageValidatorTests.cs`
- **AND** the namespace SHALL be `Skysim.Logger.Api.Tests.Validators`

---

### Requirement: Test reorganization shall preserve test behavior

All test assertions, test data, and test logic SHALL remain unchanged after reorganization.

#### Scenario: Test count remains stable

- **GIVEN** the test project has 162 tests before reorganization
- **WHEN** the reorganization is complete
- **THEN** `dotnet test` SHALL report the same number of tests (162)
- **AND** all tests SHALL pass

#### Scenario: Test assertions remain unchanged

- **GIVEN** `LoggerMiddlewareTests.cs` contains 29 tests
- **WHEN** the file is moved to `Client/Middlewares/LoggerMiddlewareTests.cs`
- **THEN** each test SHALL produce identical pass/fail results
- **AND** no test logic SHALL be modified

#### Scenario: Test namespaces are updated to reflect new location

- **GIVEN** `LoggerMiddlewareTests.cs` is moved to `Client/Middlewares/`
- **WHEN** the namespace is examined
- **THEN** it SHALL be `Skysim.Logger.Api.Tests.Client.Middlewares`
- **AND** all using statements SHALL reference correct namespaces

---

### Requirement: Test files shall not be renamed unless clarity improves

File renaming SHALL only occur when the current name does not clearly indicate what is being tested.

#### Scenario: File is renamed when name is unclear

- **GIVEN** a test file with generic or misleading name exists
- **WHEN** the reorganization is planned
- **THEN** the file MAY be renamed to better reflect its purpose
- **AND** the rename SHALL be documented in tasks

#### Scenario: File keeps original name when clear

- **GIVEN** `KafkaLogProducerTests.cs` clearly indicates it tests KafkaLogProducer
- **WHEN** the reorganization is planned
- **THEN** the file SHALL keep its original name
- **AND** only the folder location SHALL change

---

### Requirement: Build and tests shall continue passing after reorganization

The test project SHALL build successfully and all tests SHALL pass after the reorganization.

#### Scenario: Solution builds successfully

- **GIVEN** `dotnet build backend/Skysim.Logger.sln` is executed
- **WHEN** the build completes
- **THEN** there SHALL be no build errors

#### Scenario: All tests pass

- **GIVEN** `dotnet test backend/Skysim.Logger.sln` is executed
- **WHEN** the test run completes
- **THEN** all 162 tests SHALL pass
- **AND** there SHALL be no skipped or failed tests

