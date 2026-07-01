# log-flow-search-frontend Specification

## Purpose
This spec defines the frontend search and filter functionality on the Flow Monitoring page. It covers search input, dropdown filters, API integration, pagination, and UI rendering aligned with the Stitch design.

## ADDED Requirements

### Requirement: Search by text input

The system SHALL allow users to search log flows by entering text and pressing Enter or clicking a Search button. The search value is sent as the `search` query parameter to `GET /api/log-flows`.

#### Scenario: Search triggers API call on Enter

- **WHEN** user types "jane@example.com" in the search input and presses Enter
- **THEN** system calls `GET /api/log-flows?search=jane%40example.com&page=1`
- **AND** system displays results matching the search

#### Scenario: Search triggers API call on button click

- **WHEN** user types "ORD-123" in the search input and clicks Search button
- **THEN** system calls `GET /api/log-flows?search=ORD-123&page=1`
- **AND** system displays results matching the search

#### Scenario: Empty search returns all flows

- **WHEN** user clears search input and submits
- **THEN** system calls `GET /api/log-flows` (no search param)
- **AND** all flows are returned

#### Scenario: Search input has correct placeholder

- **WHEN** the Flow Monitoring page renders
- **THEN** the search input has placeholder "Search by email, phone, order ID, payment ID, or flow ID"

### Requirement: Status filter

The system SHALL allow users to filter log flows by status. Changing the Status dropdown immediately fetches results with the `status` query parameter and resets to page 1.

#### Scenario: Status filter fetches immediately on change

- **WHEN** user selects "SUCCESS" from Status dropdown
- **THEN** system calls `GET /api/log-flows?status=SUCCESS&page=1`
- **AND** page resets to 1

#### Scenario: Status filter defaults to All Statuses

- **WHEN** the Flow Monitoring page loads
- **THEN** Status dropdown shows "All Statuses" selected

#### Scenario: All valid status values are available

- **WHEN** the Status dropdown renders
- **THEN** options include: All Statuses, SUCCESS, FAILED, RUNNING, PARTIAL_FAILED

### Requirement: Flow Type filter

The system SHALL allow users to filter log flows by flow type. Changing the Flow Type dropdown immediately fetches results with the `flowType` query parameter and resets to page 1.

#### Scenario: Flow Type filter fetches immediately on change

- **WHEN** user selects "CHECKOUT_ESIM" from Flow Type dropdown
- **THEN** system calls `GET /api/log-flows?flowType=CHECKOUT_ESIM&page=1`
- **AND** page resets to 1

#### Scenario: Flow Type filter defaults to All Flow Types

- **WHEN** the Flow Monitoring page loads
- **THEN** Flow Type dropdown shows "All Flow Types" selected

#### Scenario: All valid flow type values are available

- **WHEN** the Flow Type dropdown renders
- **THEN** options include: All Flow Types, CHECKOUT_ESIM, HTTP_ACTION

### Requirement: Checkout Type filter

The system SHALL allow users to filter log flows by checkout type. Changing the Checkout Type dropdown immediately fetches results with the `checkoutType` query parameter and resets to page 1.

#### Scenario: Checkout Type filter fetches immediately on change

- **WHEN** user selects "GUEST" from Checkout Type dropdown
- **THEN** system calls `GET /api/log-flows?checkoutType=GUEST&page=1`
- **AND** page resets to 1

#### Scenario: Checkout Type filter defaults to All Types

- **WHEN** the Flow Monitoring page loads
- **THEN** Checkout Type dropdown shows "All Types" selected

#### Scenario: All valid checkout type values are available

- **WHEN** the Checkout Type dropdown renders
- **THEN** options include: All Types, GUEST, AUTHENTICATED

### Requirement: Combined filters

The system SHALL combine search, status, flowType, and checkoutType filters using AND logic. Each filter change is sent together with other active filters to the API.

#### Scenario: Multiple filters combine with AND

- **WHEN** user has "jane@example.com" in search, sets status to "SUCCESS", and sets flowType to "CHECKOUT_ESIM"
- **THEN** system calls `GET /api/log-flows?search=jane%40example.com&status=SUCCESS&flowType=CHECKOUT_ESIM&page=1`

### Requirement: Reset filters

The system SHALL provide a Reset button that clears all filter inputs and fetches page 1 with no filter params.

#### Scenario: Reset button clears all filters and fetches page 1

- **WHEN** user has active filters and clicks Reset
- **THEN** search input is cleared
- **AND** Status dropdown resets to "All Statuses"
- **AND** Flow Type dropdown resets to "All Flow Types"
- **AND** Checkout Type dropdown resets to "All Types"
- **AND** page resets to 1
- **AND** system calls `GET /api/log-flows` (no filter params)

#### Scenario: Search on Enter resets page to 1

- **WHEN** user is on page 3, types "ORD-123" in the search input, and presses Enter
- **THEN** system calls `GET /api/log-flows?search=ORD-123&page=1`

#### Scenario: Search on button click resets page to 1

- **WHEN** user is on page 3, types "ORD-123" in the search input, and clicks Search
- **THEN** system calls `GET /api/log-flows?search=ORD-123&page=1`

### Requirement: Pagination preserves filters

The system SHALL preserve all active filters when navigating between pages.

#### Scenario: Next button preserves search filter

- **WHEN** user is on page 1 with "jane@example.com" in search and clicks Next
- **THEN** system calls `GET /api/log-flows?search=jane%40example.com&page=2`

#### Scenario: Next button preserves dropdown filters

- **WHEN** user is on page 1 with status="SUCCESS" and flowType="CHECKOUT_ESIM" and clicks Next
- **THEN** system calls `GET /api/log-flows?status=SUCCESS&flowType=CHECKOUT_ESIM&page=2`

#### Scenario: Next button preserves all active filters

- **WHEN** user is on page 1 with search="ORD-123", status="FAILED", flowType="CHECKOUT_ESIM", checkoutType="GUEST" and clicks Next
- **THEN** system calls `GET /api/log-flows?search=ORD-123&status=FAILED&flowType=CHECKOUT_ESIM&checkoutType=GUEST&page=2`

### Requirement: Pagination controls

The system SHALL provide Previous and Next pagination controls and display current page information.

#### Scenario: Next button navigates to next page

- **WHEN** user is on page 1 of 3 and clicks Next
- **THEN** system calls `GET /api/log-flows?...&page=2`

#### Scenario: Previous button navigates to previous page

- **WHEN** user is on page 2 and clicks Previous
- **THEN** system calls `GET /api/log-flows?...&page=1`

#### Scenario: Previous button is disabled on page 1

- **WHEN** user is on page 1
- **THEN** Previous button is disabled

#### Scenario: Next button is disabled on last page

- **WHEN** user is on the last page (page 3 of 3)
- **THEN** Next button is disabled

#### Scenario: Page info displays current page and total pages

- **WHEN** user is on page 2 of 5
- **THEN** system displays "Page 2 of 5"

#### Scenario: Page size is fixed at 10

- **WHEN** system calls the API
- **THEN** `pageSize=10` is sent in the request

### Requirement: Table renders all columns

The system SHALL render the table with all columns: Flow ID, Flow Type, Status, Customer, Order ID, Payment ID, Checkout Type, Last Service, Last Action, Updated At, Actions.

#### Scenario: Customer column shows email and phone

- **WHEN** a flow has `customerEmail: "jane@example.com"` and `customerPhone: "+1234567890"`
- **THEN** Customer column displays both on separate lines

#### Scenario: Customer column shows dash when both null

- **WHEN** a flow has `customerEmail: null` and `customerPhone: null`
- **THEN** Customer column displays "—"

#### Scenario: Null values render as dash in all columns

- **WHEN** a flow has `orderId: null`
- **THEN** Order ID column displays "—"

#### Scenario: Last Service column shows lastServiceName

- **WHEN** a flow has `lastServiceName: "NotificationService"`
- **THEN** Last Service column displays "NotificationService"

#### Scenario: Last Service shows dash when null

- **WHEN** a flow has `lastServiceName: null`
- **THEN** Last Service column displays "—"

#### Scenario: Updated At renders valid date

- **WHEN** a flow has `updatedAt: "2024-01-15T10:30:00Z"`
- **THEN** Updated At column displays formatted date string
- **AND** does not display "Invalid Date"

#### Scenario: View Detail navigates to flow detail

- **WHEN** user clicks "View Detail" for a flow with `flowId: "FL-123"`
- **THEN** system navigates to `/logs/FL-123`

### Requirement: Status badges have correct styling

The system SHALL render status badges with color coding matching Stitch design.

#### Scenario: SUCCESS status shows green badge

- **WHEN** a flow has `status: "SUCCESS"`
- **THEN** Status column renders green badge with "SUCCESS" text

#### Scenario: FAILED status shows red badge

- **WHEN** a flow has `status: "FAILED"`
- **THEN** Status column renders red badge with "FAILED" text

#### Scenario: RUNNING status shows blue badge

- **WHEN** a flow has `status: "RUNNING"`
- **THEN** Status column renders blue badge with "RUNNING" text

#### Scenario: PARTIAL_FAILED status shows amber badge

- **WHEN** a flow has `status: "PARTIAL_FAILED"`
- **THEN** Status column renders amber badge with "PARTIAL_FAILED" text

### Requirement: Loading state

The system SHALL display a loading indicator while fetching log flows.

#### Scenario: Loading state shown during fetch

- **WHEN** system is fetching `GET /api/log-flows`
- **THEN** loading indicator is displayed

#### Scenario: Loading state replaces table content

- **WHEN** loading is in progress
- **THEN** table body is replaced with loading indicator

### Requirement: Error state

The system SHALL display a user-friendly error message when API call fails (and user is not redirected to login).

#### Scenario: Error message shown on fetch failure

- **WHEN** `GET /api/log-flows` returns an error and user is not on login page
- **THEN** system displays "Unable to load log flows." in a red alert box
- **AND** raw API error details are not shown to user

### Requirement: Empty state

The system SHALL display an empty state message when no flows match the search/filter criteria.

#### Scenario: Empty state when no flows match

- **WHEN** API returns empty items array
- **THEN** system displays "No log flows found." message

### Requirement: lastServiceName in type

The LogFlowSummary type SHALL include lastServiceName field.

#### Scenario: lastServiceName field exists in type

- **WHEN** LogFlowSummary interface is defined
- **THEN** it includes `lastServiceName: string | null`

### Requirement: Date Range filter removed

The Date Range filter SHALL NOT be rendered on the Flow Monitoring page.

#### Scenario: Date Range filter is not present

- **WHEN** the Flow Monitoring page renders
- **THEN** no Date Range inputs are displayed
