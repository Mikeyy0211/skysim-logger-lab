## 1. Project Setup and Data Layer

- [x] 1.1 Create `frontend/src/data/mockData.ts` file
- [x] 1.2 Define `MockFlow` TypeScript interface with all backend-aligned fields
- [x] 1.3 Define `MockAction` TypeScript interface with all backend-aligned fields
- [x] 1.4 Create `mockDashboardMetrics` constant with total, success, failed, running, partialFailed, averageDurationMs
- [x] 1.5 Create `mockFlows` array with at least 5 sample flow objects
- [x] 1.6 Create `mockActions` array with sample action objects per flow

## 2. Shared UI Components

- [x] 2.1 Create `frontend/src/components/Sidebar.tsx` with navigation links to Dashboard and Logs
- [x] 2.2 Create `frontend/src/components/Header.tsx` with title and logout placeholder
- [x] 2.3 Create `frontend/src/components/PageHeader.tsx` with title, subtitle, and optional children
- [x] 2.4 Create `frontend/src/components/StatusBadge.tsx` with color coding for SUCCESS, FAILED, RUNNING, PARTIAL_FAILED
- [x] 2.5 Create `frontend/src/components/MetricCard.tsx` with title and value display
- [x] 2.6 Create `frontend/src/components/EmptyState.tsx` for empty data states

## 3. Login Page

- [x] 3.1 Create `frontend/src/pages/LoginPage.tsx` with centered card layout
- [x] 3.2 Add "SkySim Logger Admin" title and "Sign in to monitor system logs" subtitle
- [x] 3.3 Add Username or Email input field
- [x] 3.4 Add Password input field
- [x] 3.5 Add "Remember this session" checkbox
- [x] 3.6 Add primary Login button
- [x] 3.7 Add helper text "Use your internal account to access logger monitoring"
- [x] 3.8 Apply consistent styling: white card, rounded corners, blue primary button
- [x] 3.9 Ensure no Keycloak integration, no forgot password, no social login

## 4. Dashboard Page

- [x] 4.1 Create `frontend/src/pages/DashboardPage.tsx` using PageHeader component
- [x] 4.2 Create grid of 6 MetricCards: Total Flows, Success Flows, Failed Flows, Running Flows, Partial Failed, Average Duration
- [x] 4.3 Create "Recent Flows" section with table
- [x] 4.4 Add table columns: Flow ID, Flow Type, Status, Last Action, Last Message, Updated At
- [x] 4.5 Use StatusBadge component for status column
- [x] 4.6 Import and use mockFlows data (use first 5 flows for recent)
- [x] 4.7 Format averageDurationMs as a human-readable duration (e.g., "1.8s")
- [x] 4.8 Apply consistent styling with white cards and rounded corners

## 5. Flow Monitoring Page (Log List)

- [x] 5.1 Create `frontend/src/pages/LogListPage.tsx` using PageHeader component
- [x] 5.2 Create filter bar with search input (placeholder: "Search by Flow ID, Order ID, Email, or Phone")
- [x] 5.3 Add Status filter dropdown
- [x] 5.4 Add Flow Type filter dropdown
- [x] 5.5 Add Date Range filter with from/to date inputs
- [x] 5.6 Create table with columns: Flow ID, Flow Type, Status, Last Action, Last Message, Steps, Updated At, Actions
- [x] 5.7 Display Steps as "successSteps/totalSteps" with failed count if > 0
- [x] 5.8 Add "View Detail" button linking to `/logs/:flowId`
- [x] 5.9 Import and use mockFlows data
- [x] 5.10 Apply consistent styling

## 6. Flow Detail Page

- [x] 6.1 Create `frontend/src/pages/LogDetailPage.tsx` using PageHeader component
- [x] 6.2 Add "Back to Logs" button linking to `/logs`
- [x] 6.3 Display status badge next to page title using StatusBadge
- [x] 6.4 Select mock flow by flowId route param and handle missing flow with simple fallback or EmptyState
- [x] 6.5 Create Flow Summary card with all flow fields (Flow ID, Flow Type, Status, Last Action, Last Message, Started At, Completed At, Updated At, Order ID, Customer Email, Customer Phone, Payment ID)
- [x] 6.6 Display "—" for missing values
- [x] 6.7 Create 3 MetricCards: Total Steps, Success Steps, Failed Steps
- [x] 6.8 Create Action Timeline section with mock actions
- [x] 6.9 Display action fields: serviceName, actionType, status, message, durationMs, createdAt
- [x] 6.10 Format duration as "Xms" or "Xs"
- [x] 6.11 Create Technical Details section with static JSON preview blocks
- [x] 6.12 Add helper text "Sensitive fields are masked."
- [x] 6.13 Use mockFlows and mockActions data
- [x] 6.14 Apply consistent styling

## 7. Layout Updates

- [x] 7.1 Update `frontend/src/layouts/AdminLayout.tsx` to import Sidebar and Header
- [x] 7.2 Replace inline sidebar with Sidebar component
- [x] 7.3 Replace inline header with Header component
- [x] 7.4 Update `frontend/src/app/Router.tsx` to import new page components

## 8. Verification and Build

- [x] 8.1 Run `npm run build` in frontend directory
- [x] 8.2 Verify no TypeScript errors
- [x] 8.3 Verify `/login` renders polished Login UI
- [x] 8.4 Verify `/dashboard` renders dashboard cards and recent flows
- [x] 8.5 Verify `/logs` renders Flow Monitoring page with mock table
- [x] 8.6 Verify `/logs/:flowId` renders Flow Detail page with summary, metrics, timeline, technical details
- [x] 8.7 Verify sidebar navigation works
- [x] 8.8 Verify no `@/` imports are introduced
- [x] 8.9 Verify no real API calls are made
