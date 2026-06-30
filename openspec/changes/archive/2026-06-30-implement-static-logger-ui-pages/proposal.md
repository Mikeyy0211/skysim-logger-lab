## Why

The frontend foundation is complete with React + TypeScript + Vite, but the UI pages are placeholder components with minimal structure. We need to implement polished, production-ready static UI pages that match the finalized Stitch design references, providing a clean admin dashboard experience for operations and support users to monitor system logs and flow activities.

## What Changes

- **New Login Page**: Polished static login UI with SkySim Logger Admin branding, centered card layout, and form inputs.
- **New Dashboard Page**: Static dashboard with metric cards showing flow statistics and a recent flows table with mock data.
- **New Flow Monitoring Page**: Static log list page with search/filter UI elements, static table, and mock flow data.
- **New Flow Detail Page**: Static flow detail page with summary card, metric cards, action timeline, and technical details sections.
- **New Shared Components**: Reusable Sidebar, Header, PageHeader, StatusBadge, MetricCard, and EmptyState components under `frontend/src/components/`.
- **Updated AdminLayout**: Integrated with new Sidebar and Header components.

## Capabilities

### New Capabilities

- **static-login-page**: Static login UI with polished design, centered card layout, form inputs, and branding. No authentication integration.
- **static-dashboard-page**: Dashboard with mock metric cards (Total Flows, Success Flows, Failed Flows, Running Flows, Partial Failed, Average Duration) and recent flows table.
- **static-flow-monitoring-page**: Flow list page with static filter UI (search input, status filter, flow type filter, date range filter) and mock flow data table.
- **static-flow-detail-page**: Flow detail page with summary card, metric cards, action timeline, and technical details sections using mock action data.
- **shared-ui-components**: Reusable UI components (Sidebar, Header, PageHeader, StatusBadge, MetricCard, EmptyState) for consistent styling across pages.
- **mock-data-layer**: Static mock data matching backend field names for flows and actions, used by all pages without real API calls.

### Modified Capabilities

- `logger-web-foundation`: The placeholder pages (Login, Dashboard, LogList, LogDetail) will be replaced with fully implemented static UI pages.

## Impact

- **Frontend**: New component files under `frontend/src/components/`, updated pages under `frontend/src/pages/`, updated AdminLayout.
- **No Backend Changes**: This is frontend-only implementation with mock data.
- **No API Calls**: Axios instance exists but is not used; no Keycloak integration.
- **No New Dependencies**: Uses existing React + TypeScript + TailwindCSS stack.
