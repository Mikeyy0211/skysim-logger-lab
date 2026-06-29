# Proposal: Reorganize Backend Test Structure

## Why

The `Skysim.Logger.Api.Tests` project contains 12 test files that have accumulated over multiple phases of development. The folder organization does not clearly reflect the component architecture of the Logger solution (Api, Client, Infrastructure, Consumers, Validators, Services). This makes tests harder to navigate, review, and extend.

## What Changes

1. Reorganize test files into folders matching the solution architecture
2. Group tests by tested component: Client/Middlewares, Client/Producers, Client/Masking, Consumers, Infrastructure/Repositories, Services/Domain, Services/Query, Validators
3. Move existing tests to appropriate folders based on what they test
4. Rename test files only when it improves clarity
5. Keep all test assertions and behavior unchanged

## Capabilities

### New Capabilities

- `test-structure-reorganization`: Organized test structure aligned with Logger solution architecture
  - Creates clear folder boundaries matching Api, Client, Infrastructure, Consumers, Validators, and Services
  - Improves test discoverability and navigation
  - Enables easier test maintenance as the solution grows

### Modified Capabilities

- None. This is a pure reorganization that preserves all existing test behavior.

## Impact

### Affected Code

- **Skysim.Logger.Api.Tests/**: All test files will be reorganized into subfolders
  - Tests for `LoggerMiddleware` → `Client/Middlewares/`
  - Tests for `KafkaLogProducer` → `Client/Producers/`
  - Tests for `SensitiveDataMasker` → `Client/Masking/`
  - Tests for `KafkaLogConsumerService` → `Consumers/`
  - Tests for `LogActionRepository` → `Infrastructure/Repositories/`
  - Tests for `FlowDomainService.IsTerminalAction` → `Services/Domain/`
  - Tests for `LogFlowQueryService` and `LogActionQueryService` → `Services/Query/`
  - Tests for validators → `Validators/`

### Not Affected

- Production code in `Skysim.Logger.Api`
- Production code in `Skysim.Logger.Client`
- Production code in `Skysim.Logger.Contracts`
- Production code in `Skysim.Logger.Infrastructure`
- Database schema
- API behavior
- Kafka consumer logic
- Build and test outcomes (162 tests should continue passing)
