## Why

The Flow Monitoring page (`/logs`) is integrated with real `GET /api/log-flows` API, but the Flow Detail page (`/logs/:flowId`) still uses mock data. This creates an incomplete user experience where users can see a list of flows but cannot inspect individual flow details or action timelines with real data. This change will complete the integration by connecting the detail page to the Logger.Api detail endpoints.

## What Changes

- Replace mock data in `LogDetailPage.tsx` with real API calls
- Create TypeScript types for log flow detail, log actions, and action details
- Create service functions to fetch flow detail, flow actions, and action details
- Add loading, error, and not-found states for API calls
- Implement on-demand action detail loading when user clicks to view details
- Keep existing UI structure, styling, and behavior intact

## Capabilities

### New Capabilities

- `log-flow-detail-api-integration`: Integrate LogDetailPage with GET /api/log-flows/{flowId} and GET /api/log-flows/{flowId}/actions endpoints. This capability covers fetching flow summary data, action timeline data, and rendering them with proper loading/error states.
- `log-action-details-api-integration`: Integrate action detail viewing with GET /api/log-actions/{actionId}/details endpoint. This capability covers on-demand loading of REQUEST/RESPONSE/ERROR payloads when user selects an action.

### Modified Capabilities

- `static-flow-detail-page`: This capability currently requires mock data. After this change, it will use real API data instead of mock data. The spec should be updated to reflect that API calls replace mock data usage.

## Impact

**Files to create:**
- `frontend/src/types/logAction.ts` - TypeScript interfaces for log actions and action details

**Files to modify:**
- `frontend/src/pages/LogDetailPage.tsx` - Replace mock data with API calls
- Potentially extend `frontend/src/services/logFlowService.ts` or create new service file

**APIs consumed:**
- `GET /api/log-flows/{flowId}` - Flow detail
- `GET /api/log-flows/{flowId}/actions` - Flow actions list
- `GET /api/log-actions/{actionId}/details` - Action details

**Dependencies:**
- Existing `apiClient` from `frontend/src/services/api.ts`
- Existing tokenStorage/authService and apiClient interceptor for token handling
