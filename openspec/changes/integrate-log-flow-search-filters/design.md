## Context

The Flow Monitoring page (`/logs`) currently fetches data from `GET /api/log-flows` but has non-functional, visual-only filters. The backend already supports `search`, `status`, `flowType`, and `checkoutType` query parameters with pagination metadata. The frontend service and types are mostly in place but need updating. The current table is missing several columns (Customer, Order ID, Payment ID, Checkout Type, Last Service) and has no pagination controls.

**Current state:**
- `logFlowService.ts`: `LogFlowListParams` has `page` and `pageSize` only. Missing `search`, `status`, `flowType`, `checkoutType`.
- `types/logFlow.ts`: `LogFlowSummary` is complete but missing `lastServiceName`. `PagedResponse` and `LogFlowListResponse` exist.
- `LogListPage.tsx`: Filters have no `onChange` handlers. Table missing Customer, Order ID, Payment ID, Checkout Type, Last Service columns. No pagination UI. Date Range filter present but unsupported by backend.

## Goals / Non-Goals

**Goals:**
- Wire up search and filter inputs to call the real backend API with appropriate query parameters.
- Display all backend fields including `lastServiceName`, `customerEmail`, `customerPhone`, `orderId`, `paymentId`, `checkoutType`.
- Add pagination controls using backend `page`, `pageSize`, `totalItems`, `totalPages`.
- Reset page to 1 when any filter changes.
- Add a Reset/Clear Filters button.
- Follow Stitch Flow Monitoring design for layout, status badges, and table styling.
- Handle loading, error, and empty states gracefully.
- Null/missing values render as "—", no undefined/Invalid Date.

**Non-Goals:**
- Dashboard API integration or summary widgets.
- Deep Flow Detail page polish.
- Date Range filter (backend does not support `fromDate`/`toDate` yet).
- Real-time updates or WebSocket.
- Export, retry, or delete buttons.
- Redux, React Query, or new state management.
- `@/` path aliases.
- Backend modifications.

## Decisions

### 1. Search input: Enter/button vs. debounce

**Decision**: Trigger search on Enter key or a dedicated Search button. Dropdown filters fetch immediately on change.

**Rationale**: Text search needs a deliberate trigger to avoid excessive API calls on every keystroke. A Search button + Enter key is simple and clear. Dropdown filters with a limited set of options are safe to fetch immediately — the user is making a deliberate selection.

**Alternatives considered**:
- Debounce on every keystroke with 300ms delay — rejected as it adds complexity and may cause race conditions with pagination.
- Immediate search on text input — rejected because it may cause excessive API calls.
- Button-only (no Enter) — rejected because Enter is a standard UX pattern users expect.

### 2. Filter state location and fetch behavior

**Decision**: Use local `useState` within `LogListPage.tsx`. Filter state is managed locally and used in a `fetchFlows(currentPage)` function. Dropdown filters trigger immediate fetch on change; text search triggers fetch on Enter or Search button.

**Rationale**: No cross-component filter state needed. The `fetchFlows` function reads current filter state and a `currentPage` parameter, so pagination preserves filters naturally. No Redux or context required for this scope.

**Alternatives considered**:
- Redux Toolkit — rejected as out of scope. No other page needs filter state.
- URL query params — considered but adds routing complexity not needed for this phase.

### 3. Service layer changes

**Decision**: Extend `LogFlowListParams` in `logFlowService.ts` to include `search`, `status`, `flowType`, `checkoutType`. The service already handles both array and paginated responses.

**Rationale**: Minimal change. The service already returns `items` from paginated responses. No new types needed in the service.

### 4. Type updates

**Decision**: Add `lastServiceName` to `LogFlowSummary`. Keep existing `PagedResponse<T>` and `LogFlowListResponse` as-is.

**Rationale**: `lastServiceName` is returned by the backend but missing from the type. No structural changes to response types needed.

### 5. Checkout Type filter

**Decision**: Include `checkoutType` filter dropdown with values `GUEST` and `AUTHENTICATED`.

**Rationale**: Backend supports it and the Stitch design includes it.

### 6. Date Range filter

**Decision**: Remove the Date Range filter from the UI.

**Rationale**: Backend does not yet support `fromDate`/`toDate` query parameters. Removing it avoids confusion. It can be re-added when backend adds support.

### 7. Pagination style

**Decision**: Simple Previous/Next buttons with "Page X of Y" info. No page number buttons.

**Rationale**: Simpler than the Stitch full pagination (with page number buttons and "Showing X-Y of Z" footer). Matches current scope. Full pagination can be added later.

## Risks / Trade-offs

- **Risk**: User types in search and forgets to press Enter/search button → no API call triggered.
  - **Mitigation**: Add a Search button. Users are accustomed to clicking Search. Can add debounce in a future iteration if needed.

- **Risk**: Backend returns 400 validation error for invalid filter values.
  - **Mitigation**: Filter dropdowns only show valid enum values. No free-text filter inputs.

- **Risk**: API returns 401 and user gets redirected, losing filter state.
  - **Mitigation**: Acceptable behavior. Auth redirect is existing behavior. Users can re-navigate and re-search.

- **Risk**: Long `lastMessage` causes table overflow.
  - **Mitigation**: Use `max-w-xs truncate` on the Last Message column, consistent with current implementation.

- **Risk**: `lastServiceName` is null for some flows (e.g., new flows with no actions).
  - **Mitigation**: Display "—" when `lastServiceName` is null/undefined.

## Open Questions

1. Should the "Showing X-Y of Z flows" footer be added now or in a future iteration?
   - Decision: Omit for now. Simple Previous/Next + "Page X of Y" is sufficient.

2. Should `pageSize` be configurable by the user or fixed at 10?
   - Decision: Fixed at 10. Keep it simple. No page size selector.

3. Should `checkoutType` dropdown include an "All Types" option with empty value?
   - Decision: Yes. Matches existing Status and Flow Type dropdowns pattern.
