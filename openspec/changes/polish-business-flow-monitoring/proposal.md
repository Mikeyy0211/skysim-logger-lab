## Why

The current business flow monitoring UI (Log List and Log Detail pages) has functional API integration but lacks polish for demo readiness. Status badges, checkout type badges, loading/error/empty states, spacing, and action timeline presentation need improvement to deliver a professional and usable demo experience.

## What Changes

### Log List Page Polish

- Improve status badges with consistent styling for SUCCESS, FAILED, RUNNING, PARTIAL_FAILED
- Add styled checkout type badges for GUEST and AUTHENTICATED
- Truncate long Flow IDs while keeping them readable with tooltip
- Add copy-to-clipboard button for Flow ID
- Improve filter bar spacing and alignment
- Add loading spinner during filter/search operations
- Show clear empty state when no results match filters
- Improve table column alignment and row hover states

### Log Detail Page Polish

- Streamline flow summary header with key fields: Flow ID, Status, Customer, Checkout Type, Created At, Updated At
- Add styled summary cards for: Total Actions, Success Actions, Failed Actions, Last Service, Last Action
- Improve action timeline/table with columns: Time, Service, Action, Status, Duration, Message, View Detail
- Enhance action payload/details panel with collapsible accordion or inline expansion
- Add retry button on error states
- Ensure no blank screens during loading states

### General UX Improvements

- Consistent loading spinner across pages
- Error states with clear messages and retry buttons
- Empty states with helpful guidance
- No unhandled console errors
- Responsive layout adjustments

## Capabilities

### New Capabilities

- `polished-flow-monitoring-ui`: Frontend polish layer for business flow monitoring pages. This is a pure UI/UX enhancement that does not change API contracts or backend behavior.

### Modified Capabilities

- None — this is a frontend-only polish that does not modify existing spec requirements.

## Impact

**Files affected:**
- `frontend/src/pages/LogListPage.tsx`
- `frontend/src/pages/LogDetailPage.tsx`
- `frontend/src/components/StatusBadge.tsx`
- `frontend/src/types/logFlow.ts` (if adding checkout type badge component)

**No backend changes.**
**No API contract changes.**
**No new dependencies.**
