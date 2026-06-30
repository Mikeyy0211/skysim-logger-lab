# Design: integrate-log-flow-list-api

## Context

The frontend `LogListPage.tsx` currently renders hardcoded mock flows from `mockData.ts`. The backend `implement-logger-query-api` change exposes `GET /api/log-flows` returning `PagedResponse<LogFlowSummaryDto>` with camelCase JSON. The login token flow (`implement-login-token-flow`) provides Keycloak authentication and stores the JWT in localStorage. The Axios instance in `api.ts` already attaches the `Authorization: Bearer <token>` header.

**Current state:** Axios instance exists with auth interceptor. Protected routes redirect to `/login` on unauthenticated access. Mock data drives the LogListPage.

**Constraints:** Do not modify backend. Use existing Axios instance. Do not add Redux Toolkit or React Query. Keep code simple and junior-friendly. `npm run build` must pass.

**Stakeholders:** Frontend developer (consumes the API), Mentor (reviews).

---

## Goals / Non-Goals

**Goals:**

- Create `logFlowService.ts` calling `GET /api/log-flows` using the existing `apiClient` from `api.ts`.
- Create `types/logFlow.ts` with TypeScript interfaces aligned to backend DTOs.
- Add 401 response handling in `api.ts` that clears token and redirects to `/login`.
- Update `LogListPage.tsx` to fetch real data on mount with loading and error states.
- Keep filters as visual-only (no server-side filtering in this phase).
- Show EmptyState component when no flows exist.

**Non-Goals:**

- Integration with flow detail, action list, or action detail endpoints.
- Dashboard API integration.
- Server-side filtering with query params.
- Sorting or pagination UI.
- Redux Toolkit or React Query.
- Backend changes.
- Docker changes.
- Keycloak changes.

---

## Decisions

### Decision 1 — Create a dedicated `logFlowService.ts` instead of calling API directly in the component

- **Why**: Separates API calling logic from UI components. Follows the existing pattern of `authService.ts`. Makes testing easier and keeps the component clean.
- **Consequence**: API calls are isolated in a service file. Component uses `useState` and `useEffect` for data fetching.

### Decision 2 — Use `useState` and `useEffect` for data fetching without Redux or React Query

- **Why**: The task explicitly excludes Redux Toolkit and React Query. A simple `useState` + `useEffect` pattern is sufficient for a single-page fetch-on-mount.
- **Consequence**: Loading state, error state, and data are stored in local component state.

### Decision 3 — Handle both paginated and plain-array API responses

- **Why**: The backend may return `PagedResponse<LogFlowSummaryDto>` (with `items`, `page`, `pageSize`, `totalItems`, `totalPages`) or a plain array depending on implementation.
- **Implementation**: The response type checks for `Array.isArray()` or presence of `items` property and normalizes accordingly.
- **Consequence**: The frontend is resilient to API response format variations.

### Decision 4 — 401 error handling in Axios response interceptor

- **Why**: Centralizing 401 handling in the Axios interceptor ensures all API calls automatically handle token expiration consistently.
- **Implementation**: In `api.ts`, check `error.response?.status === 401`, clear token from localStorage, and redirect to `/login` only if the current path is not already `/login` to avoid a redirect loop.
- **Consequence**: No raw error messages exposed to users on 401. Users on the login page are not redirected again.

---

## TypeScript Types

```typescript
// frontend/src/types/logFlow.ts

export type FlowStatus = 'RUNNING' | 'SUCCESS' | 'FAILED' | 'PARTIAL_FAILED';

export interface LogFlowSummary {
  flowId: string;
  flowType: string;
  checkoutType: string | null;
  status: FlowStatus;
  customerEmail: string | null;
  customerPhone: string | null;
  userId: string | null;
  orderId: string | null;
  paymentId: string | null;
  totalSteps: number;
  successSteps: number;
  failedSteps: number;
  lastActionType: string | null;
  lastMessage: string | null;
  startedAt: string;
  completedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export type LogFlowListResponse = PagedResponse<LogFlowSummary> | LogFlowSummary[];
```

---

## Log Flow Service

```typescript
// frontend/src/services/logFlowService.ts

import { apiClient } from './api';
import type { LogFlowListResponse, LogFlowSummary } from '../types/logFlow';

export interface LogFlowListParams {
  page?: number;
  pageSize?: number;
}

export async function getLogFlows(params?: LogFlowListParams): Promise<LogFlowSummary[]> {
  try {
    const response = await apiClient.get<LogFlowListResponse>('/api/log-flows', { params });
    const data = response.data;

    if (Array.isArray(data)) {
      return data;
    }

    if (data && typeof data === 'object' && 'items' in data) {
      return data.items;
    }

    return [];
  } catch (error) {
    console.error('Failed to fetch log flows:', error);
    throw error;
  }
}
```

---

## 401 Error Handling

The Axios response interceptor in `api.ts` is updated to handle 401 with redirect loop prevention:

```typescript
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401 && window.location.pathname !== '/login') {
      removeToken();
      window.location.href = '/login';
    }
    console.error('API Error:', error);
    return Promise.reject(error);
  }
);
```

**Note:** Redirect only if current path is not already `/login` to avoid an infinite redirect loop.

---

## LogListPage Integration

The component is updated to:

1. Add local state: `flows`, `isLoading`, `error`
2. On mount (`useEffect`), call `getLogFlows()`
3. Handle loading state: show spinner or "Loading..." text
4. Handle error state: show user-friendly message
5. Handle empty state: show EmptyState component
6. Render table with real data when available

---

## Project Structure

```
frontend/src/
  types/
    logFlow.ts              ← new
  services/
    logFlowService.ts       ← new
    api.ts                  ← modified (add 401 handler)
  pages/
    LogListPage.tsx         ← modified (use real API)
```

---

## Loading and Error States

### Loading State

While fetching data, display a simple loading indicator:

```tsx
if (isLoading) {
  return (
    <div className="flex items-center justify-center p-6">
      <div className="text-gray-500">Loading log flows...</div>
    </div>
  );
}
```

### Error State

If fetch fails (and the user is not redirected to login), display a user-friendly error message:

```tsx
if (error) {
  return (
    <div className="p-6">
      <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-red-700">
        Unable to load log flows.
      </div>
    </div>
  );
}
```

**Note:** Raw API errors are not exposed to the user. If the API returns 401, the user is redirected to login instead of showing an error.

### Empty State

If no flows exist, display an empty state message:

```tsx
if (flows.length === 0) {
  return (
    <div className="p-6">
      <div className="text-center text-gray-500">
        No log flows found.
      </div>
    </div>
  );
}
```

---

## Risks / Trade-offs

- **[Risk] API returns plain array instead of paginated response**: Mitigation: `logFlowService.ts` checks for both formats and normalizes to an array.
- **[Risk] Token expires mid-session**: Mitigation: 401 interceptor clears token and redirects to login (only if not already on `/login`).
- **[Risk] Backend not running or unreachable**: Mitigation: Error state shows "Unable to load log flows." Network errors are caught and displayed without raw details.
- **[Risk] Redirect loop on 401**: Mitigation: interceptor checks `window.location.pathname !== '/login'` before redirecting.

---

## Open Questions

1. **Should we add a retry mechanism on failure?** Decision: No — keep it simple for this phase. Users can refresh the page.
2. **Should we cache the API response?** Decision: No — always fetch fresh data on page mount for this phase.
3. **Should we use a custom hook for data fetching?** Decision: No — keep it simple with `useState` + `useEffect` to match junior-friendly requirement.
