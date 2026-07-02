## ADDED Requirements

### Requirement: Checkout Type Badge Component

The system SHALL provide a reusable CheckoutTypeBadge component for displaying checkout types.

#### Scenario: Renders GUEST checkout type
- **WHEN** CheckoutTypeBadge receives `checkoutType="GUEST"`
- **THEN** it displays a badge with purple/indigo styling
- **AND** label shows "GUEST"

#### Scenario: Renders AUTHENTICATED checkout type
- **WHEN** CheckoutTypeBadge receives `checkoutType="AUTHENTICATED"`
- **THEN** it displays a badge with blue styling
- **AND** label shows "AUTHENTICATED"

#### Scenario: Renders null or undefined checkout type
- **WHEN** CheckoutTypeBadge receives null or undefined
- **THEN** it displays "—" placeholder

### Requirement: Flow ID Truncation and Copy

The Log List page SHALL display Flow IDs in a readable truncated format with copy functionality.

#### Scenario: Flow ID is truncated in table cell
- **WHEN** a Flow ID renders in the table
- **THEN** it shows first 8 and last 4 characters separated by "..."
- **AND** the full Flow ID is shown in a tooltip on hover

#### Scenario: Copy button copies Flow ID to clipboard
- **WHEN** user clicks the copy button next to a Flow ID
- **THEN** the full Flow ID is copied to clipboard
- **AND** a brief success indicator is shown (checkmark or tooltip)

### Requirement: Status Badge Styling Consistency

The StatusBadge component SHALL display consistent professional styling.

#### Scenario: SUCCESS status displays green badge
- **WHEN** StatusBadge receives `status="SUCCESS"`
- **THEN** it displays green background with green text
- **AND** label shows "SUCCESS"

#### Scenario: FAILED status displays red badge
- **WHEN** StatusBadge receives `status="FAILED"`
- **THEN** it displays red background with red text
- **AND** label shows "FAILED"

#### Scenario: RUNNING status displays blue badge
- **WHEN** StatusBadge receives `status="RUNNING"`
- **THEN** it displays blue background with blue text
- **AND** label shows "RUNNING"

#### Scenario: PARTIAL_FAILED status displays amber badge
- **WHEN** StatusBadge receives `status="PARTIAL_FAILED"`
- **THEN** it displays amber background with amber text
- **AND** label shows "PARTIAL FAILED"

#### Scenario: Unknown status displays gray badge
- **WHEN** StatusBadge receives an unknown status
- **THEN** it displays gray background with gray text
- **AND** label shows the status in title case

### Requirement: Filter Bar Improvements

The Log List page filter bar SHALL provide improved UX.

#### Scenario: Reset Filters button clears all filters
- **WHEN** user clicks the Reset Filters button
- **THEN** all filter inputs are cleared to default values
- **AND** the API is called without filter parameters

#### Scenario: Loading state shown during filter/search
- **WHEN** user submits a filter or search
- **THEN** a loading indicator is displayed while API call is in progress
- **AND** the table content is replaced with loading state

#### Scenario: Empty state shown when no results
- **WHEN** API returns empty array
- **THEN** a clear empty state message is displayed
- **AND** helpful guidance is shown (e.g., "No flows match your filters. Try adjusting your search criteria.")

### Requirement: Log List Table Columns

The Log List table SHALL display focused, readable columns.

#### Scenario: Table columns are correctly defined
- **WHEN** the Log List table renders
- **THEN** columns are: Flow ID, Status, Customer, Checkout, Last Service, Last Action, Updated At, Actions
- **AND** Flow ID column uses truncation and copy functionality

#### Scenario: Table row hover state
- **WHEN** user hovers over a table row
- **THEN** the row has a subtle background color change

### Requirement: Log Detail Summary Cards

The Log Detail page SHALL display enhanced summary cards.

#### Scenario: Summary cards show key metrics
- **WHEN** the Log Detail page renders
- **THEN** five summary cards are displayed: Total Actions, Success Actions, Failed Actions, Last Service, Last Action
- **AND** each card shows the corresponding value from the flow data

### Requirement: Log Detail Action Timeline

The Log Detail page SHALL display an improved action timeline.

#### Scenario: Action timeline shows structured information
- **WHEN** the action timeline renders
- **THEN** each action displays: Time, Service, Action Type, Status Badge, Duration, Message, View Details button

#### Scenario: Action details expand inline
- **WHEN** user clicks "View Details" on an action
- **THEN** the action details expand inline below the action row
- **AND** clicking again collapses the details

#### Scenario: Action details show payload sections
- **WHEN** action details are expanded
- **THEN** it shows sections for: Request Payload, Response Payload, Error (if applicable)
- **AND** JSON is formatted and syntax highlighted
- **AND** sensitive fields are masked (shown as "***")

### Requirement: Loading States

Both Log List and Log Detail pages SHALL display consistent loading states.

#### Scenario: Initial page load shows loading state
- **WHEN** user navigates to Log List or Log Detail page
- **THEN** a loading indicator is displayed until data loads

#### Scenario: Loading state replaces content
- **WHEN** loading is in progress
- **THEN** the previous content or table is hidden
- **AND** a loading spinner or skeleton is shown in its place

### Requirement: Error States with Retry

Both pages SHALL display error states with retry functionality.

#### Scenario: Error state shows message and retry
- **WHEN** an API call fails
- **THEN** an error message is displayed
- **AND** a retry button is shown

#### Scenario: Retry reloads data
- **WHEN** user clicks retry button
- **THEN** the API call is made again
- **AND** on success, normal content is displayed

### Requirement: Empty States

Both pages SHALL display helpful empty states.

#### Scenario: Empty log list shows guidance
- **WHEN** Log List page loads with no data
- **THEN** a message "No log flows found" is displayed
- **AND** helpful text suggests checking filters or waiting for data

#### Scenario: Empty action timeline shows message
- **WHEN** Log Detail page loads with no actions
- **THEN** a message "No actions found for this flow" is displayed
