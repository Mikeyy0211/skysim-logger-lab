# Proposal: integrate-log-flow-list-api

## Why

The Skysim Logger frontend has a Flow Monitoring page (`LogListPage.tsx`) that currently displays mock data from `mockData.ts`. The backend has a working `GET /api/log-flows` endpoint implemented in the `implement-logger-query-api` change, returning paginated flow summaries from PostgreSQL. This change bridges the gap by connecting the frontend log list page to the real backend API.

## What Changes

- New `frontend/src/services/logFlowService.ts`: service layer for calling `GET /api/log-flows`
- New `frontend/src/types/logFlow.ts`: TypeScript interfaces aligned with backend DTOs
- Updated `frontend/src/pages/LogListPage.tsx`: replace mock data with real API integration
- 401 error handling in `frontend/src/services/api.ts`: clear token and redirect to `/login`
- Loading and error states in the LogListPage component

## Capabilities

### New Capabilities

- `log-flow-list-api-integration`: Connect the frontend Flow Monitoring page to `GET /api/log-flows`. Produces `specs/log-flow-list-api-integration/spec.md`.

### Modified Capabilities

- `log-list-page`: The existing static/log mock log list page is enhanced with real API data.

## Impact

- **Frontend**: New files `logFlowService.ts` and `types/logFlow.ts`. Modified `LogListPage.tsx` and `api.ts`.
- **Backend**: No changes.
- **Database**: No changes.
- **Kafka**: No changes.
- **Docker**: No changes.
- **Keycloak**: No changes.

## Out of Scope

- `GET /api/log-flows/{flowId}` integration
- `GET /api/log-flows/{flowId}/actions` integration
- `GET /api/log-actions/{actionId}/details` integration
- Dashboard API integration
- Detail page API integration
- Server-side filtering with query params
- Sorting implementation
- Pagination UI controls
- Redux Toolkit
- React Query
- Realtime sync
- Backend changes
- Docker changes
- Keycloak changes
