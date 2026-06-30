## Context

The frontend foundation is complete with React + TypeScript + Vite project at `frontend/`. TailwindCSS is configured, React Router v6 is set up with routes for `/login`, `/dashboard`, `/logs`, and `/logs/:flowId`. The Axios instance is created but should not be used in this phase. The current pages are placeholder components that need to be replaced with polished static UI.

Design references are located at `docs/design/stitch_skysim_logger_admin_dashboard/`:
- Login page visual reference: `login_skysim_logger/screen.png`
- Dashboard visual reference: `dashboard_skysim_logger/screen.png`
- Log list HTML/design: `log_list_skysim_logger_revised/code.html`
- Log detail HTML/design: `log_detail_skysim_logger_revised/code.html`

Key stakeholders: Backend/Frontend developers, Operations team, Support team.

## Goals / Non-Goals

**Goals:**
- Implement polished static UI pages matching the Stitch design references
- Create reusable shared components for consistent styling
- Use mock data matching backend field names (no real API calls)
- Keep implementation simple and junior-friendly
- Ensure `npm run build` passes without errors

**Non-Goals:**
- No Keycloak authentication integration
- No protected routes or auth middleware
- No Redux Toolkit state management
- No real API calls with Axios
- No real pagination, filtering, or sorting
- No dark mode support
- No real-time features

## Decisions

### 1. Component Organization

**Decision:** Create shared components under `frontend/src/components/` and page-specific components in `frontend/src/pages/`.

**Rationale:** Following the existing folder structure from `logger-web-foundation`, keeping shared UI components separate from page components makes them reusable and maintainable.

**Shared Components:**
- `Sidebar.tsx` - Navigation sidebar with links to Dashboard and Logs
- `Header.tsx` - Top header with title and logout placeholder
- `PageHeader.tsx` - Page title and subtitle component
- `StatusBadge.tsx` - Reusable status badge with color coding
- `MetricCard.tsx` - Dashboard metric card component
- `EmptyState.tsx` - Empty state placeholder component

### 2. Color Scheme and Styling

**Decision:** Use TailwindCSS utility classes with the design principles:
- Light background: `bg-gray-50`
- White cards: `bg-white`
- Soft borders: `border-gray-200`
- Rounded corners: `rounded-lg`
- Subtle shadows: `shadow-sm`
- Blue primary accent: `text-blue-600`, `bg-blue-600`

**Status Colors:**
- SUCCESS: `bg-green-100 text-green-800`
- FAILED: `bg-red-100 text-red-800`
- PARTIAL_FAILED: `bg-amber-100 text-amber-800`
- RUNNING: `bg-blue-100 text-blue-800`

**Rationale:** Matches the design references and provides clear visual indicators for different flow statuses.

### 3. Mock Data Structure

**Decision:** Create a `frontend/src/data/mockData.ts` file containing all mock data as TypeScript constants.

**Rationale:** Centralizing mock data makes it easy to find and modify, and ensures consistency across all pages using the same data.

**Flow Mock Data Fields:**
```typescript
interface MockFlow {
  flowId: string;
  flowType: string;
  checkoutType: string;
  status: 'RUNNING' | 'SUCCESS' | 'FAILED' | 'PARTIAL_FAILED';
  customerEmail: string;
  customerPhone: string;
  orderId: string;
  paymentId: string;
  totalSteps: number;
  successSteps: number;
  failedSteps: number;
  lastActionType: string;
  lastMessage: string;
  startedAt: string;
  completedAt?: string;
  createdAt: string;
  updatedAt: string;
}
```

**Action Mock Data Fields:**
```typescript
interface MockAction {
  actionId: string;
  flowId: string;
  serviceName: string;
  actionType: string;
  status: 'RUNNING' | 'SUCCESS' | 'FAILED' | 'PARTIAL_FAILED';
  message: string;
  durationMs: number;
  createdAt: string;
  finishedAt?: string;
}
```

### 4. Routing and Navigation

**Decision:** Use React Router v6 `Link` components for navigation within the sidebar.

**Rationale:** Existing Router.tsx already has the routes configured. Using `Link` from React Router provides client-side navigation without page reloads.

### 5. Page Structure

**Login Page:**
- Centered card layout using `flex` and `justify-center`
- Title: "SkySim Logger Admin"
- Subtitle: "Sign in to monitor system logs"
- Username input, Password input, Remember checkbox, Login button
- Helper text at bottom

**Dashboard Page:**
- PageHeader with title and subtitle
- Grid of 6 MetricCards (2x3 layout on desktop)
- Recent Flows section with table
- Uses mock data with backend-aligned field names

**Flow Monitoring Page (Log List):**
- PageHeader with title and subtitle
- Filter bar UI (search input, status select, flow type select, date inputs)
- Table with columns: Flow ID, Flow Type, Status, Last Action, Last Message, Steps, Updated At, Actions
- Steps column shows: `successSteps/totalSteps` with failedSteps if > 0
- View Detail button navigates to `/logs/:flowId`

**Flow Detail Page:**
- Back button and PageHeader
- Status badge next to title
- Flow Summary card with all flow details
- 3 MetricCards (Total Steps, Success Steps, Failed Steps)
- Action Timeline section (vertical list of actions)
- Technical Details section (static JSON preview blocks)

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| TypeScript errors if field names don't match | Use exact backend field names in mock data |
| Inconsistent styling across pages | Use shared components (StatusBadge, MetricCard, PageHeader) |
| Build failures with new components | Keep implementation simple, use standard Tailwind patterns |
| Path alias issues with @/ imports | Use relative imports instead of @/ paths |

## Migration Plan

This is a greenfield implementation for UI pages. Steps:

1. Create shared components under `frontend/src/components/`
2. Create mock data file at `frontend/src/data/mockData.ts`
3. Implement Login page at `frontend/src/pages/LoginPage.tsx`
4. Implement Dashboard page at `frontend/src/pages/DashboardPage.tsx`
5. Implement Flow Monitoring page at `frontend/src/pages/LogListPage.tsx`
6. Implement Flow Detail page at `frontend/src/pages/LogDetailPage.tsx`
7. Update `frontend/src/layouts/AdminLayout.tsx` to use new Sidebar and Header
8. Update `frontend/src/app/Router.tsx` to use new page components
9. Run `npm run build` to verify no errors

**Rollback:** Revert to previous page components using git checkout.

## Open Questions

1. Should the login page have a loading state on the button? (Decided: No, keep simple)
2. Should the table rows be clickable? (Decided: No, use explicit View Detail button)
3. Should we implement local client-side filtering? (Decided: Optional, keep simple)
