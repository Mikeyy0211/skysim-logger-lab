## Context

The Logger API already provides a `GET /api/log-flows` endpoint with individual filter fields (`customerEmail`, `customerPhone`, `orderId`, `paymentId`, `flowType`, `checkoutType`, `status`, `serviceName`, `fromDate`, `toDate`) and pagination. The `LogFlowSummaryDto` already includes all business fields needed by the Flow Monitoring page. The `LogFlowQueryService.GetListAsync` already implements all individual field filters. The `LogFlowListQueryValidator` already validates field lengths.

The missing pieces are:

1. A unified `search` parameter for cross-field partial matching (the operator's "one search box" use case).
2. Case-insensitive search implementation that works correctly with PostgreSQL (Npgsql) — `EF.Functions.Like()` is case-sensitive in PostgreSQL by default.
3. Enum-aware validation for `status`, `flowType`, and `checkoutType` so invalid values return 400 instead of silently returning zero results.
4. `RUNNING` and `PARTIAL_FAILED` status constants that are referenced by the frontend but missing from `StatusTypes.cs`.
5. Default sort must change from `createdAt desc` to `updatedAt desc` so the most recently active flows appear first.
6. `lastServiceName` in the list response — the next FE phase needs it for Flow Monitoring and Dashboard.

## Goals / Non-Goals

**Goals:**

- Add `search` query parameter to `GET /api/log-flows` that performs case-insensitive partial matching across `flowId`, `customerEmail`, `customerPhone`, `orderId`, `paymentId`, `userId`, `lastMessage`.
- Ensure search is truly case-insensitive for PostgreSQL using `EF.Functions.ILike()`.
- Validate `search` (max length, non-empty).
- Validate `status`, `flowType`, `checkoutType` against known enum constants and return 400 for unknown values.
- Add `RUNNING` and `PARTIAL_FAILED` to `StatusTypes.cs`.
- Change default sort from `createdAt` to `updatedAt` descending.
- Combine `search` with all existing filters (AND logic).
- Add `lastServiceName` to the log flow list response. The `lastServiceName` represents the `serviceName` of the latest action for each flow, determined by `StepOrder` descending.
- Add unit tests covering search + filter + sort + pagination combinations.
- `dotnet build` and `dotnet test` pass.
- Backend only — do not modify frontend, Kafka consumer, or infrastructure.

**Non-Goals:**

- Adding a new dashboard endpoint (out of scope per requirements).
- Implementing full-text search engine (SQL `ILIKE` is sufficient for the stated use cases).
- Modifying Docker Compose, Kafka, or Keycloak configuration.
- Modifying the Kafka consumer.
- Frontend integration (handled by separate change).

## Decisions

### Decision 1: Search uses `EF.Functions.ILike()` for truly case-insensitive matching

**Chosen approach:** Use `EF.Functions.ILike()` (PostgreSQL `ILIKE`) for all searchable fields. This is the PostgreSQL/Npgsql-native case-insensitive pattern match operator. Wrap each field check with a null guard (`field != null && EF.Functions.ILike(field, $"%{search}%")`).

**Alternatives considered:**

- `EF.Functions.Like()` with `ToLower()` on both sides: Works in-memory and with InMemory provider, but `ToLower()` on the DB column prevents index use even for prefix matches. Rejected.
- `.ToLower() == search.ToLower()` pattern: Already used for `customerEmail` filter in the existing code. This works but `ILike` is the idiomatic PostgreSQL approach.
- `EF.Functions.StartsWith()` only: Rejected because operators search for partial order/payment IDs mid-string.
- Full-text search (PostgreSQL `tsvector`): Over-engineered for the current scale and adds infrastructure complexity.
- ElasticSearch / Lucene: Same over-engineering concern.

**Implementation note:** `ILike` is Npgsql-specific and not supported by the EF Core InMemory provider used in unit tests. In production code, use `EF.Functions.ILike()`. In unit tests, use `ToLower().Contains()` as the fallback expression so tests run without a real PostgreSQL instance. A conditional helper or test-specific expression builder can select the right approach per provider.

### Decision 2: Enum-aware validation in `LogFlowListQueryValidator`

**Chosen approach:** Use `FlowTypes`, `CheckoutTypes`, and `StatusTypes` constants to build `HashSet<string>` validators. Invalid values return 400 with a clear error message listing valid options.

**Valid status values:**
- `SUCCESS`
- `FAILED`
- `RUNNING`
- `PARTIAL_FAILED`

**Note:** `IN_PROGRESS` exists in `StatusTypes.cs` but is not in the current project standard for flow-level status. It should not be added to new acceptance criteria or validator rules for this change. The existing `LogEventMessageValidator` uses `IN_PROGRESS` for action-level events — that is a separate concern and out of scope here.

### Decision 3: `lastServiceName` implementation strategy

**Chosen approach:** Use a correlated subquery in the `Select` projection ordered by `StepOrder` descending (latest action):

```csharp
// In the Select projection of GetListAsync:
LastServiceName = db.LogActions
    .Where(a => a.FlowId == f.FlowId)
    .OrderByDescending(a => a.StepOrder)
    .Select(a => (string?)a.ServiceName)
    .FirstOrDefault()
```

This translates to a single SQL query with a correlated subquery — no N+1. It works with PostgreSQL.

**If the correlated subquery is problematic with the InMemory provider in unit tests**, use an alternative approach for tests:
1. Use a grouped join approach with `GroupBy` and `OrderByDescending` in the test seed setup
2. Or mock the service at a higher level and test the query behavior with SQLite in-memory instead of the EF Core InMemory provider
3. Or test the `lastServiceName` projection in a separate integration-style test that uses a real-like query pattern

The correlated subquery is the correct production approach. Tests should adapt to the production code, not the other way around.

**Expected values:**
- For CHECKOUT_ESIM flows where the latest action is EMAIL_SENT, `lastServiceName` should be `NotificationService`.
- For HTTP_ACTION-only flows where the latest action is HTTP_REQUEST, `lastServiceName` should be `sample-checkout-service`.
- Flows with no actions should return `null` for `lastServiceName`.

### Decision 4: Default sort changes to `updatedAt desc`

**Chosen:** `updatedAt` is the new default sort column.

**Rationale:** "Most recently active flows appear first" is the desired behavior for operators monitoring live traffic. `updatedAt` is updated every time a new action arrives for the flow.

**Breaking change consideration:** The existing test `GetListAsync_SortByCreatedAtDesc_IsDefault` asserts `createdAt desc` as default. This test will need to be updated to reflect the new default. No external callers are affected since the only caller using explicit sortBy is the sort test itself.

## Risks / Trade-offs

- [Risk] `ILike` is Npgsql-specific and not supported by InMemory EF Core provider in unit tests.
  - **Mitigation:** Use `ToLower().Contains()` for the InMemory provider in tests. Production code uses `ILike` for correct PostgreSQL behavior.
- [Risk] `ILIKE '%value%'` without a full-text index may be slow on very large tables.
  - **Mitigation:** For a lab/training project this is acceptable. PostgreSQL GIN indexes can be added later if needed.
- [Risk] Correlated subquery for `lastServiceName` may not translate with InMemory provider.
  - **Mitigation:** Use a test approach that works around InMemory limitations (see Decision 3). Production code is correct.
- [Risk] Changing default sort may surprise any existing consumers relying on `createdAt` order.
  - **Mitigation:** The change is intentional and documented. Callers using explicit `sortBy=createdAt` are unaffected.
- [Risk] `StatusTypes` needs `RUNNING` and `PARTIAL_FAILED` added before enum validation can be complete.
  - **Mitigation:** Add the two constants to `StatusTypes.cs`. No other changes needed.

## Migration Plan

1. **Update `StatusTypes.cs`** — add `RUNNING` and `PARTIAL_FAILED` constants.
2. **Update `LogFlowListQuery.cs`** — add `Search` property.
3. **Update `LogFlowListQueryValidator.cs`** — add `Search` length validation + enum validation for `Status`, `FlowType`, `CheckoutType`.
4. **Update `LogFlowSummaryDto.cs`** — add `LastServiceName` property (nullable string).
5. **Update `LogFlowQueryService.cs`** — add search predicate using `ILike` (with `ToLower().Contains()` fallback for InMemory tests), add `LastServiceName` correlated subquery projection, change default sort to `updatedAt`.
6. **Update existing test** `GetListAsync_SortByCreatedAtDesc_IsDefault` to reflect new default.
7. **Add new tests** for search, combined filters, enum validation, and `lastServiceName`.
8. **Verify** `dotnet build` and `dotnet test` pass.
9. **Deploy** — standard backend deployment, no DB migration needed (no schema changes).

**Rollback:** Revert the modified source files and the test file. No data migration required.

## Open Questions

1. Should `IN_PROGRESS` be kept in `StatusTypes.cs` alongside `RUNNING`, or removed as legacy?
   - **Decision:** Keep it for now. The existing `LogEventMessageValidator` uses `IN_PROGRESS` for action-level status. Flow-level status uses `RUNNING` / `PARTIAL_FAILED`. They are separate concerns but both can coexist in `StatusTypes.cs`.
2. Should we add a database index on `lastMessage` for search performance?
   - **Decision:** No, not in this phase. Partial-match `ILIKE` on a text column does not benefit from B-tree indexes anyway. GIN index is out of scope.
