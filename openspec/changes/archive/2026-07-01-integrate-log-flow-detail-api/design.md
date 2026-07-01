## Context

The Flow Monitoring page (`/logs`) is integrated with the real Logger.Api `GET /api/log-flows` endpoint. The Flow Detail page (`/logs/:flowId`) currently uses mock data from `mockData.ts` and does not make any API calls. The backend exposes additional endpoints for flow details and action details that are not yet consumed by the frontend.

**Current State:**
- `LogDetailPage.tsx` reads `flowId` from route params and finds matching mock flow from `mockFlows`
- No API calls are made on the detail page
- Technical Details section shows hardcoded JSON preview

**Target State:**
- On page load, fetch flow detail and flow actions from real APIs
- Show loading state while fetching
- Show error or not-found state if API fails or flow does not exist
- Technical Details loads on-demand when user clicks to view action details

## Goals / Non-Goals

**Goals:**
- Replace mock data usage in `LogDetailPage.tsx` with real API calls
- Create TypeScript types for log actions and action details
- Extend `logFlowService.ts` with new service functions
- Add loading, error, and not-found states for API calls
- Implement on-demand action detail loading
- Keep existing UI structure and styling intact

**Non-Goals:**
- Dashboard API integration
- Server-side filtering and sorting
- Redux Toolkit or React Query for state management
- Retry, re-run, export, delete, or edit features
- Realtime sync
- Backend or Docker changes

## Decisions

### Decision 1: TypeScript Types Location

**Choice:** Create a new file `frontend/src/types/logAction.ts` for log action and action detail types, while reusing existing types from `frontend/src/types/logFlow.ts`.

**Rationale:** Separating log action types from log flow types creates cleaner boundaries. The log flow types are already defined and cover the list/summary scenarios. New types for actions and action details are specific to the detail page and action timeline.

**Nullable Fields:** Backend may return null for checkoutType, customerEmail, customerPhone, userId, orderId, paymentId, completedAt, lastActionType, lastMessage, message, durationMs, finishedAt. TypeScript interfaces should allow these as nullable (`| null`), and UI helpers must display "—" for null values.

**Alternative Considered:** Add all types to `logFlow.ts`. Rejected because mixing flow summary, flow detail, action, and action detail types in one file reduces clarity.

### Decision 2: Service Location

**Choice:** Extend `frontend/src/services/logFlowService.ts` with new functions for flow detail, flow actions, and action details.

**Rationale:** The service file already exists for log flow operations. Adding detail-related functions maintains consistency and keeps service logic in one place.

**Alternative Considered:** Create a new `logFlowDetailService.ts`. Rejected because it would fragment related logic across multiple files.

### Decision 3: Action Details Loading Strategy

**Choice:** Load action details on-demand when user clicks "View Details" button.

**Rationale:** Loading details for every action on page load would be inefficient and could result in many unnecessary API calls. On-demand loading is more performant and aligns with the "keep API usage simple" requirement.

**Alternative Considered:** Auto-load details for the first action. Rejected because on-demand provides better UX and is explicitly suggested in the requirements.

**Payload Type:** The payload field is flexible. Use `unknown` type in TypeScript interface and stringify safely when rendering. Backend may return any JSON-safe value (object, array, string, number, boolean, null).

**Response Normalization:** The service must handle both `LogActionDetail[]` and single `LogActionDetail` responses and normalize to `LogActionDetail[]` before returning to the component.

### Decision 4: State Management Approach

**Choice:** Use React `useState` and `useEffect` hooks for managing API state.

**Rationale:** The project explicitly excludes Redux Toolkit and React Query. Using local component state is straightforward and sufficient for this use case.

**Alternative Considered:** Redux Toolkit or React Query. Rejected due to project constraints.

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| API returns unexpected data shape | Define strict TypeScript interfaces; handle edge cases gracefully |
| Long loading times for large action lists | Show loading indicator immediately; consider pagination if needed |
| Missing or null values from API | Use `formatFieldValue()` helper to display "—" for missing data |
| 401 errors during API calls | Existing Axios interceptor handles token removal and redirect |
| Action details not available for some actions | Show "No details available" state gracefully |

## Implementation Notes

### API Endpoints

| Function | Endpoint | Response |
|----------|----------|----------|
| `getLogFlowById(flowId)` | `GET /api/log-flows/{flowId}` | `LogFlowDetail` |
| `getLogFlowActions(flowId)` | `GET /api/log-flows/{flowId}/actions` | `LogAction[]` |
| `getLogActionDetails(actionId)` | `GET /api/log-actions/{actionId}/details` | `LogActionDetail[]` |

### Component State

```typescript
interface LogDetailState {
  flow: LogFlowDetail | null;
  actions: LogAction[];
  selectedActionDetails: LogActionDetail[] | null;
  isLoadingFlow: boolean;
  isLoadingActions: boolean;
  isLoadingDetails: boolean;
  error: string | null;
  flowNotFound: boolean;
}
```

### Error Handling Strategy

1. If flow fetch fails with 404 → Show "Flow not found" state
2. If flow fetch fails with other error → Show "Unable to load flow detail" error
3. If actions fetch fails → Show "Unable to load actions" inline error
4. If details fetch fails → Show "Unable to load action details" inline error
5. If 401 occurs → Existing Axios interceptor handles redirect

### File Changes Summary

| File | Change |
|------|--------|
| `frontend/src/types/logAction.ts` | **Create** - LogAction, LogActionDetail, ActionDetailType interfaces |
| `frontend/src/services/logFlowService.ts` | **Extend** - Add getLogFlowById, getLogFlowActions, getLogActionDetails |
| `frontend/src/pages/LogDetailPage.tsx` | **Modify** - Replace mock data with API calls, add loading/error states |
