## Context

After Phase 2 (extract-logger-client), `Skysim.Logger.Client` now owns all client-side logging components. However, `Skysim.Logger.Api` has accumulated mixed responsibilities and scattered folder structure from its initial monolithic design.

### Current Api Structure Issues

| Issue | Location | Description |
|-------|----------|-------------|
| Consumer misplaced | `Infrastructure/Kafka/KafkaLogConsumerService.cs` | Kafka consumer belongs in a dedicated `Consumers/` folder |
| DLQ publisher misplaced | `Infrastructure/Kafka/DlqPublisher.cs` | DLQ publisher is a Kafka concern but buried deep |
| Options misplaced | `Infrastructure/Kafka/KafkaConsumerOptions.cs` | Options class is in wrong location |
| Validator misplaced | `Contracts/DTOs/LogEventMessageValidator.cs` | Validator is in DTOs folder instead of Validators/ |
| Query classes misplaced | `Contracts/DTOs/Queries/` | Query classes should be at `Contracts/Queries/` |
| Server-side helpers in Common | `Common/Kafka/` | KafkaCommon.cs and RetryPolicyFactory.cs are only used by Api |

### Usage Analysis of Common Project

| File | Used By | Decision |
|------|---------|----------|
| `Common/Kafka/KafkaCommon.cs` | `Api` (DlqPublisher) | Move to `Api/Kafka/` |
| `Common/Kafka/RetryPolicyFactory.cs` | `Api` (Consumer) | Move to `Api/Kafka/` |
| `Common/Middleware/MiddlewareLogEntry.cs` | None | Delete after build confirms |

### Target Folder Structure

```
backend/Skysim.Logger.Api/
├── Base/
│   └── ApiControllerBase.cs
├── Consumers/
│   └── KafkaLogConsumerService.cs          ← moved from Infrastructure/Kafka/
├── Controllers/
│   ├── LogFlowsController.cs
│   └── LogActionsController.cs
├── Contracts/
│   ├── DTOs/
│   │   ├── LogFlowSummaryDto.cs
│   │   ├── LogFlowDetailDto.cs
│   │   ├── LogActionDto.cs
│   │   └── LogActionDetailsDto.cs
│   └── Queries/
│       ├── LogFlowListQuery.cs              ← moved from Contracts/DTOs/Queries/
│       └── LogActionListQuery.cs            ← moved from Contracts/DTOs/Queries/
├── Domain/
│   └── Services/
│       └── FlowDomainService.cs
├── Infrastructure/
│   ├── Persistence/
│   │   ├── Migrations/
│   │   └── Exceptions/
│   │       └── DuplicateEventException.cs
│   └── Kafka/                              ← may be removed if empty after moves
├── Kafka/
│   ├── KafkaConsumerOptions.cs             ← moved from Infrastructure/Kafka/
│   ├── DlqPublisher.cs                     ← moved from Infrastructure/Kafka/
│   ├── RetryPolicyFactory.cs               ← moved from Common/Kafka/
│   └── KafkaCommon.cs                      ← moved from Common/Kafka/
├── Services/
│   └── Query/
│       ├── ILogFlowQueryService.cs
│       ├── LogFlowQueryService.cs
│       ├── ILogActionQueryService.cs
│       └── LogActionQueryService.cs
├── Validators/
│   ├── LogEventMessageValidator.cs          ← moved from Contracts/DTOs/
│   ├── LogFlowListQueryValidator.cs
│   └── LogActionListQueryValidator.cs
└── Program.cs
```

## Goals / Non-Goals

**Goals:**
- Clarify `Skysim.Logger.Api` as the server-side Logger Service
- Organize files into logical folders matching their responsibilities
- Move server-side Kafka helpers from Common to Api
- Remove unused code (MiddlewareLogEntry.cs)
- Keep `dotnet build` and `dotnet test` green throughout

**Non-Goals:**
- Do NOT change runtime behavior (APIs, consumer, DLQ, persistence)
- Do NOT modify Client library
- Do NOT modify database schema
- Do NOT add new features
- Do NOT add authentication/authorization
- Do NOT delete Common project (it may be used by Infrastructure or future services)
- Do NOT create new Options classes (KafkaConsumerOptions already exists)
- Do NOT create Extension methods

## Decisions

### Decision 1: Where to place Kafka helpers?

**Chosen:** Move `KafkaCommon.cs` and `RetryPolicyFactory.cs` from `Common/Kafka/` to `Api/Kafka/`

**Rationale:** These helpers are only used by server-side Api code (DlqPublisher, KafkaLogConsumerService). Moving them:
- Reduces Common project surface area
- Makes Api more self-contained
- Aligns with "Api owns server-side Kafka logic" principle

**Alternatives Considered:**
1. Keep them in Common → Would leave server-only code in shared project
2. Inline into DlqPublisher/Consumer → Would duplicate logic

### Decision 2: How to handle unused MiddlewareLogEntry?

**Chosen:** Delete `Common/Middleware/MiddlewareLogEntry.cs` after verifying build

**Rationale:** This class was created for the old LoggerMiddleware design. Now that Client owns middleware and uses its own LogEventMessage, this class is unused. Deletion reduces dead code.

**Verification:** Build must pass after deletion.

### Decision 3: Keep vs. Delete Common project?

**Chosen:** Keep `Skysim.Logger.Common` project for now

**Rationale:**
- `Infrastructure` references `Common` (indirect via shared helpers)
- Project may be useful for future shared utilities
- Easy to delete later if it becomes completely empty
- User explicitly said "Do not delete Common unless it is completely unused"

### Decision 4: Keep query DTOs in Contracts folder?

**Chosen:** Move `LogFlowListQuery` and `LogActionListQuery` to `Contracts/Queries/` (not out of Contracts)

**Rationale:** These are API contract types (request models), not response DTOs. Keeping them in `Contracts/` maintains clean separation:
- `Contracts/DTOs/` → Response DTOs
- `Contracts/Queries/` → Request/query models

### Decision 5: No new service abstractions

**Chosen:** Do not create `ILogPersistenceService` or `LogPersistenceService`

**Rationale:** This phase is structural cleanup only. Existing persistence orchestration in `KafkaLogConsumerService` should remain unchanged. Adding new abstractions would be scope creep.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Namespace changes break tests | Update namespaces in test files; run `dotnet test` after each file move |
| Build fails after moving KafkaCommon | Move KafkaCommon.cs together with all files that use it in same commit |
| Common project becomes orphaned | Monitor Common usage; delete project only when truly empty |
| File moves cause merge conflicts | Use git mv to preserve history; coordinate with team |

**Trade-off:** Moving files creates short-term churn but long-term clarity.

### File Move Strategy

Use `git mv` instead of copy/delete to preserve git history:
```bash
# Example: git mv source.cs DestinationFolder/
git mv Infrastructure/Kafka/KafkaLogConsumerService.cs Consumers/
git mv Contracts/DTOs/LogEventMessageValidator.cs Validators/
```

Benefits:
- Git tracks renames automatically
- History preserved in git blame
- Easier rollback if needed

## Migration Plan

### Phase 1: Prepare
1. Ensure `dotnet build` and `dotnet test` pass on baseline
2. Commit any pending changes

### Phase 2: Move Kafka helpers from Common to Api
1. Create `Api/Kafka/` folder if not exists
2. Use `git mv` to move `Common/Kafka/KafkaCommon.cs` → `Api/Kafka/KafkaCommon.cs`
3. Use `git mv` to move `Common/Kafka/RetryPolicyFactory.cs` → `Api/Kafka/RetryPolicyFactory.cs`
4. Update namespaces in moved files to `Skysim.Logger.Api.Kafka`
5. Update usings in `DlqPublisher.cs` and `KafkaLogConsumerService.cs` to reference `Api.Kafka`
6. Verify `dotnet build` passes
7. Verify `dotnet test` passes

### Phase 3: Reorganize Api internal structure
1. Move `KafkaLogConsumerService.cs` → `Consumers/`
2. Move `DlqPublisher.cs` → `Kafka/` (if not already)
3. Move `KafkaConsumerOptions.cs` → `Kafka/`
4. Move `LogEventMessageValidator.cs` → `Validators/`
5. Move query classes → `Contracts/Queries/`
6. Update all namespaces and usings
7. Remove empty folders after moves

### Phase 4: Cleanup Common
1. Verify `MiddlewareLogEntry.cs` is unused (grep for usages)
2. Delete `Common/Middleware/` folder
3. Verify build passes

### Phase 5: Verify Common Project Usage
1. Check all references to `Skysim.Logger.Common` from Api and Infrastructure
2. If no code uses Common after moving Kafka helpers and deleting MiddlewareLogEntry:
   - Remove unnecessary project references from Api.csproj and Infrastructure.csproj
   - Verify build and tests still pass
3. Do NOT delete the Common project itself unless explicitly approved

### Phase 6: Final Verification

## Open Questions

There are no open questions. Common project usage will be verified during implementation. If no code uses `Skysim.Logger.Common` after moving server-side helpers, unnecessary project references may be removed, but the Common project itself will not be deleted in this phase.
