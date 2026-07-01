# Tasks - Integrate Log Flow Detail API

## 1. Create TypeScript Types

- [x] 1.1 Create `frontend/src/types/logAction.ts` with `LogAction` interface (actionId, flowId, serviceName, actionType, status, message, durationMs, createdAt, finishedAt)
- [x] 1.2 Add `LogActionDetail` interface (actionId, detailType, payload as unknown, masked, createdAt)
- [x] 1.3 Add `ActionDetailType` type for 'REQUEST' | 'RESPONSE' | 'ERROR'
- [x] 1.4 Make nullable fields optional or union with null (message, durationMs, finishedAt can be null)
- [x] 1.5 Export types from the new file

## 2. Extend Service Layer

- [x] 2.1 Add `getLogFlowById(flowId: string)` function to `logFlowService.ts`
- [x] 2.2 Add `getLogFlowActions(flowId: string)` function to `logFlowService.ts`
- [x] 2.3 Add `getLogActionDetails(actionId: string)` function to `logFlowService.ts`
- [x] 2.4 In `getLogActionDetails`, normalize response: if single object, wrap in array; if array, return directly
- [x] 2.5 Import new types in `logFlowService.ts`
- [x] 2.6 Verify service functions use existing `apiClient`

## 3. Update LogDetailPage Component

- [x] 3.1 Import service functions and types
- [x] 3.2 Replace mock flow selection with `useState` for flow, actions, and loading states
- [x] 3.3 Add `useEffect` to fetch flow detail on page load
- [x] 3.4 Add `useEffect` to fetch flow actions on page load
- [x] 3.5 Add loading state UI while fetching flow detail
- [x] 3.6 Add error state UI when flow fetch fails
- [x] 3.7 Add not-found state UI when flow does not exist (404)
- [x] 3.8 Update Flow Summary card to use real data from state
- [x] 3.9 Update Metric Cards to use real data from state
- [x] 3.10 Update Action Timeline to render actions in API return order (no extra sorting)
- [x] 3.11 Handle nullable action fields (message, durationMs, finishedAt) with "—" fallback
- [x] 3.12 Add loading state in Action Timeline section
- [x] 3.13 Add error state in Action Timeline section

## 4. Implement Action Details On-Demand

- [x] 4.1 Add state for selected action and its details
- [x] 4.2 Add "View Details" button to each action in timeline
- [x] 4.3 Add handler for View Details click to fetch action details
- [x] 4.4 Update Technical Details section to show real API data
- [x] 4.5 Display REQUEST/RESPONSE/ERROR payloads when available
- [x] 4.6 Add loading state for action details fetch
- [x] 4.7 Add empty/error state for action details section

## 5. Finalize and Test

- [x] 5.1 Remove unused mock data imports from LogDetailPage.tsx
- [x] 5.2 Verify `npm run build` passes
- [x] 5.3 Test navigation from /logs to /logs/:flowId
- [x] 5.4 Test loading state appears on page load
- [x] 5.5 Test flow data displays correctly when loaded
- [x] 5.6 Test View Details button loads action details
- [x] 5.7 Test 401 handling redirects to /login
