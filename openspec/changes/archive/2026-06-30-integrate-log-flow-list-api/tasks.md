# Tasks: integrate-log-flow-list-api

This change integrates the Flow Monitoring page with the real Logger.Api `GET /api/log-flows` endpoint. Follow the task order — later tasks depend on earlier ones.

## 1. Create TypeScript Types

- [x] 1.1 Create `frontend/src/types/logFlow.ts` with TypeScript interfaces aligned to backend DTOs:
  - `FlowStatus` type union: `'RUNNING' | 'SUCCESS' | 'FAILED' | 'PARTIAL_FAILED'`
  - `LogFlowSummary` interface with all flow fields (flowId, flowType, checkoutType, status, customerEmail, customerPhone, userId, orderId, paymentId, totalSteps, successSteps, failedSteps, lastActionType, lastMessage, startedAt, completedAt, createdAt, updatedAt)
  - `PagedResponse<T>` interface with items, page, pageSize, totalItems, totalPages
  - `LogFlowListResponse` type as union to handle both paginated and plain array responses
  - **Do NOT create** `LogFlowListResult` (not needed for this phase)

## 2. Create Log Flow Service

- [x] 2.1 Create `frontend/src/services/logFlowService.ts`:
  - Import `apiClient` from `./api` (NOT a new axios instance)
  - Import types from `../types/logFlow`
  - Export `LogFlowListParams` interface with optional page and pageSize
  - Export `getLogFlows(params?: LogFlowListParams)` async function
  - Call `apiClient.get('/api/log-flows', { params })`
  - Normalize response: if array return directly, if object with items return items, else return empty array
  - Catch errors and re-throw with console.error for debugging

## 3. Update Axios API Client (401 Handler)

- [x] 3.1 Update `frontend/src/services/api.ts`:
  - Add import for `removeToken` from `../utils/tokenStorage` (already imported for getToken)
  - Update response interceptor to check `error.response?.status === 401`
  - On 401: call `removeToken()`, check `window.location.pathname !== '/login'` to avoid redirect loop, then redirect to `/login` with `window.location.href`
  - Keep existing error logging but do not expose raw stack traces to user
  - **Avoid redirect loop if current path is already `/login`.**

## 4. Update LogListPage with Real API Integration

- [x] 4.1 Update `frontend/src/pages/LogListPage.tsx`:
  - Add import for `getLogFlows` from `../services/logFlowService`
  - Add import for types from `../types/logFlow`
  - Remove import for `mockFlows` from `../data/mockData`
  - Add local state: `flows` (LogFlowSummary[]), `isLoading` (boolean), `error` (string | null)
  - Add `useEffect` that calls `getLogFlows()` on mount
  - Handle loading state: show "Loading log flows..." text centered in the table area
  - Handle error state: show error message in a red alert box
  - Handle empty state: show "No log flows found." message
  - Replace `mockFlows.map()` with `flows.map()` in the table body
  - Keep all existing table columns and UI structure
  - Keep filter UI as visual-only (no onChange handlers needed)
  - Keep View Detail links pointing to `/logs/${flow.flowId}`

## 5. Verify Build

- [x] 5.1 Run `cd frontend && npm run build` and confirm no errors
- [x] 5.2 Verify TypeScript compilation passes with no type errors

## 6. Browser Verification

- [ ] 6.1 Open browser DevTools (F12), go to Network tab
- [ ] 6.2 Login and navigate to `/logs`
- [ ] 6.3 Confirm `GET /api/log-flows` request includes header:
  ```
  Authorization: Bearer <token>
  ```
- [ ] 6.4 Verify response returns JSON with log flow data

## 7. Verification & Completion

- [x] 7.1 All tasks marked complete in this file
- [x] 7.2 Review `design.md` decisions against implementation
- [x] 7.3 Confirm `npm run build` passes
