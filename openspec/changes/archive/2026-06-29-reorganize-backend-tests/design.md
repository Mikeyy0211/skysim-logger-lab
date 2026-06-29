## Context

The `Skysim.Logger.Api.Tests` project contains 12 test files organized in a mix of flat structure and partial subfolders. After multiple phases of development (extracting Logger.Client, Logger.Contracts, Logger.Infrastructure), the test organization no longer matches the solution architecture. This creates friction during code review and test maintenance.

**Current test file locations:**
- Flat: `KafkaLogConsumerServiceTests.cs`, `KafkaLogConsumerServicePersistenceTests.cs`, `LogEventMessageValidatorTests.cs`, `SensitiveDataMaskerTests.cs`
- In subfolders: `MiddlewareTests/`, `KafkaProducerTests/`, `RepositoryTests/`, `QueryServiceTests/`

**Current folder-to-tested-component mapping is inconsistent:**
- `MiddlewareTests/` → `Client/Middlewares` ✓
- `KafkaProducerTests/` → `Client/Producers` ✓
- `RepositoryTests/` → partially correct (LogActionRepository → Infrastructure, but LogFlowTerminalAction → Services/Domain)
- `QueryServiceTests/` → `Services/Query` ✓
- Root files → need proper categorization

## Goals / Non-Goals

**Goals:**
- Align test folder structure with Logger solution architecture (Api, Client, Infrastructure, Consumers, Validators, Services)
- Make test discoverability match the code structure developers expect
- Keep all test assertions unchanged (no behavior modification)
- Maintain 100% test pass rate

**Non-Goals:**
- Do not change production code organization
- Do not add new tests (only reorganize existing ones)
- Do not modify Kafka consumer logic, Client behavior, or API behavior
- Do not add Docker-based or Testcontainers integration tests
- Do not rename files unless clarity genuinely improves
- Do not add Keycloak/auth-server tests

## Decisions

### Decision 1: Target folder structure

**Chosen:** Create the following folder hierarchy inside `Skysim.Logger.Api.Tests/`:

```
Client/
  Middlewares/
  Producers/
  Masking/
Consumers/
Infrastructure/
  Repositories/
Services/
  Domain/
  Query/
Validators/
```

**Rationale:** This mirrors the production solution structure. Developers looking for "where are the middleware tests?" will naturally look in `Client/Middlewares/`.

**Alternatives considered:**
- Flat structure with prefix naming (e.g., `Test_Client_Middleware_LoggerMiddlewareTests.cs`) — rejected for poor readability
- Mixed flat/folder structure — rejected for inconsistency

### Decision 2: File naming

**Chosen:** Keep original file names when they clearly indicate purpose. Rename only when ambiguous.

**Rationale:** Minimizes churn. `KafkaLogProducerTests.cs` is self-explanatory; renaming to `ProducerTests.cs` would lose specificity.

**Files to rename (if any):** None identified. All current names are clear.

### Decision 3: Namespace updates

**Chosen:** Update namespace to match new folder location using pattern `Skysim.Logger.Api.Tests.<FolderPath>`.

**Examples:**
- `LoggerMiddlewareTests.cs` → namespace `Skysim.Logger.Api.Tests.Client.Middlewares`
- `LogFlowTerminalActionTests.cs` → namespace `Skysim.Logger.Api.Tests.Services.Domain`

**Rationale:** C# convention. Rider/Visual Studio auto-fixes this when file is moved.

### Decision 4: Keep RepositoryTests/LogFlowTerminalActionTests.cs in new location

**Chosen:** Move `LogFlowTerminalActionTests.cs` from `RepositoryTests/` to `Services/Domain/`.

**Rationale:** The test exercises `FlowDomainService.IsTerminalAction`, which is a domain service, not a repository. Misplaced folder.

## Migration Plan

### Phase 1: Create new folder structure
Create empty folders matching the target structure. No files yet.

### Phase 2: Move files to new locations
For each file:
1. Move file to new folder
2. Update namespace to match new path
3. Update using statements if imports break
4. Verify build still passes
5. Run tests to verify pass rate unchanged

### Phase 3: Remove old empty folders
Delete `MiddlewareTests/`, `KafkaProducerTests/`, `RepositoryTests/`, `QueryServiceTests/` after all files are migrated.

### Phase 4: Verify and commit
1. `dotnet build backend/Skysim.Logger.sln` passes
2. `dotnet test backend/Skysim.Logger.sln` passes (162 tests)
3. `openspec validate reorganize-backend-tests --strict` passes

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Namespace mismatch causes build errors | Update namespaces incrementally; run build after each file move |
| Test pass rate drops | Run tests after each file move; do not proceed if any test fails |
| Merge conflicts if other work happens in parallel | Communicate timing with team; prioritize this change |

**Trade-off:** Short-term churn (moving files) vs. long-term maintainability (clear structure). The reorganization cost is one-time.
