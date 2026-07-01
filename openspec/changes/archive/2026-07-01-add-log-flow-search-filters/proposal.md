## Why

The current `GET /api/log-flows` endpoint supports individual filter fields (`customerEmail`, `customerPhone`, `orderId`, `paymentId`, etc.) but lacks a unified `search` parameter that can match across multiple fields simultaneously. Frontend Flow Monitoring needs to let operators search by any identifier (email, phone, order ID, payment ID, or flow ID) from a single search box. Additionally, the current default sort is `createdAt` descending, but the requirement is to default to `updatedAt` descending so that recently active flows appear first. Adding proper enum-based validation for `status`, `flowType`, and `checkoutType` will give cleaner 400 responses instead of silently returning zero results for typos.

## What Changes

- Add `search` query parameter to `GET /api/log-flows` that performs case-insensitive partial matching across: `flowId`, `customerEmail`, `customerPhone`, `orderId`, `paymentId`, `userId`, `lastMessage`.
- Use `EF.Functions.ILike()` for PostgreSQL/Npgsql to ensure truly case-insensitive search across all searchable fields.
- Update `LogFlowListQuery` to include `search` field.
- Update `LogFlowListQueryValidator` to validate `search` (max length) and add enum-aware validation for `status`, `flowType`, and `checkoutType` values against the project's standard constants.
- Add `RUNNING` and `PARTIAL_FAILED` to `StatusTypes.cs`.
- Change default sort from `createdAt` to `updatedAt` descending in `LogFlowQueryService.ApplySorting`.
- Optionally add `lastServiceName` to `LogFlowSummaryDto` via a single correlated subquery — if too expensive, document as follow-up.
- Add unit tests for the search + filter query behavior.

## Capabilities

### New Capabilities

- `log-flow-search`: Adds a unified `search` query parameter to the log flow list API that matches across multiple business fields simultaneously, enabling quick operator lookups by email, phone, order ID, payment ID, flow ID, user ID, or last message excerpt.

### Modified Capabilities

*(None — this phase is backend-only. Frontend integration will be handled by a separate change.)*

## Impact

- **Modified files**: `StatusTypes.cs`, `LogFlowListQuery.cs`, `LogFlowListQueryValidator.cs`, `LogFlowQueryService.cs`, `LogFlowSummaryDto.cs`
- **New files**: Search/filter unit tests in `LogFlowQueryServiceTests.cs`
- **No frontend changes** in this phase
- **No Kafka consumer changes** in this phase
- **Breaking change**: Default sort order changes from `createdAt desc` to `updatedAt desc` — this is intentional per requirement; existing callers using explicit `sortBy=createdAt` are unaffected.
