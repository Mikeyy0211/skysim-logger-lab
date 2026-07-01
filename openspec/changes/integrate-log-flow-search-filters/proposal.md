## Why

The Flow Monitoring page (`/logs`) currently displays data from the real Logger.Api endpoint but uses non-functional, visual-only filters. The backend now supports `search`, `status`, `flowType`, and `checkoutType` query parameters. This change integrates the frontend with these backend search and filter capabilities, enabling operations users to actively search and filter log flows by email, phone, order ID, payment ID, flow ID, status, flow type, and checkout type.

## What Changes

- **Update `logFlowService.ts`**: Add filter parameters (`search`, `status`, `flowType`, `checkoutType`, `page`, `pageSize`) to `getLogFlows()` function and pass them to the API call.
- **Update `LogFlowSummary` type**: Add `lastServiceName` field and ensure all backend fields are typed.
- **Add `LogFlowListResponse` type**: Support paginated response shape with `items`, `page`, `pageSize`, `totalItems`, `totalPages`.
- **Update `/logs` page**:
  - Wire up search input to call API with `search` param (debounced or on Enter/button click).
  - Wire up Status, Flow Type, and Checkout Type dropdowns to call API with respective params.
  - Add Reset/Clear Filters button.
  - Reset page to 1 when any filter changes.
  - Render table columns: Flow ID, Flow Type, Status, Customer (email + phone), Order ID, Payment ID, Checkout Type, Last Service, Last Action, Updated At, Action.
  - Display status badges with color coding (SUCCESS=green, FAILED=red, RUNNING=blue, PARTIAL_FAILED=amber).
  - Show customer email and phone together in Customer column; render "—" when null.
  - Show "—" for null/missing values; avoid "undefined" or "Invalid Date".
  - Implement Previous/Next pagination with disabled states and "Page X of Y" info.
  - Add loading state while fetching.
  - Add error state with user-friendly message.
  - Add empty state when no flows match.
  - Keep existing View Detail navigation to `/logs/{flowId}`.
- **Keep existing auth behavior**: Token interceptor and 401 redirect remain unchanged.
- **UI reference**: Follow Stitch Flow Monitoring design for layout, colors, typography, and component styling.

## Capabilities

### New Capabilities

- `log-flow-search-frontend`: Frontend integration of real search and filter functionality on the Flow Monitoring page. Enables users to search by email, phone, order ID, payment ID, or flow ID; filter by status, flow type, and checkout type; and paginate through results using backend-provided metadata.

### Modified Capabilities

- `log-flow-list-api-integration`: Upgrade from "visual-only filters" (non-functional) to "functional filters" that call the real backend API. The `logFlowService` now accepts filter parameters and the `/logs` page actively queries the backend. All scenarios in the existing spec remain valid; filter interactions now trigger real API calls instead of being non-functional.

## Impact

- **Files modified**:
  - `frontend/src/services/logFlowService.ts`
  - `frontend/src/types/logFlow.ts` (if exists) or `frontend/src/types/index.ts`
  - `frontend/src/pages/LogsPage.tsx` (or wherever `/logs` is implemented)
- **No backend files modified**
- **No new libraries added**
- **No Redux, React Query, or state management libraries introduced**
- **No `@/` path aliases introduced**
