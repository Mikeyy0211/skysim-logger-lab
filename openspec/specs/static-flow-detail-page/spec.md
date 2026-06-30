# static-flow-detail-page Specification

## Purpose
TBD - created by archiving change implement-static-logger-ui-pages. Update Purpose after archive.
## Requirements
### Requirement: Flow Detail Page UI

The system SHALL provide a static flow detail page for tracing actions and inspecting technical details.

#### Scenario: Flow detail page renders with header
- **WHEN** the user navigates to `/logs/:flowId`
- **THEN** the page title "Flow Detail" is displayed
- **AND** the subtitle "Trace actions and inspect technical details for this flow" is shown

#### Scenario: Flow detail page selects mock flow by route param
- **WHEN** the user navigates to `/logs/:flowId`
- **THEN** the page reads the flowId from route params
- **AND** it selects the matching mock flow from mockFlows
- **AND** if no matching flow exists, it displays EmptyState or a simple not-found message
- **AND** it safely falls back without crashing

#### Scenario: Flow detail page shows back button
- **WHEN** the flow detail page renders
- **THEN** a "Back to Logs" button is displayed
- **AND** clicking it navigates back to `/logs`

#### Scenario: Flow detail page displays status badge
- **WHEN** the flow detail page renders
- **THEN** the flow status badge is displayed next to the page title
- **AND** the badge uses correct color based on status

#### Scenario: Flow summary card displays all fields
- **WHEN** the flow summary card renders
- **THEN** it displays: Flow ID, Flow Type, Status, Last Action, Last Message, Started At, Completed At, Updated At, Order ID, Customer Email, Customer Phone, Payment ID
- **AND** missing values are displayed as "—"

#### Scenario: Metric cards display step counts
- **WHEN** the metric cards render
- **THEN** three cards are displayed: Total Steps, Success Steps, Failed Steps
- **AND** each shows the corresponding count from mock data

### Requirement: Action Timeline Section

The flow detail page SHALL display an action timeline section.

#### Scenario: Action timeline shows actions in order
- **WHEN** the action timeline section renders
- **THEN** mock actions are displayed in chronological order
- **AND** each action shows: actionId, serviceName, actionType, status, message, durationMs, createdAt, finishedAt

#### Scenario: Action status badges use correct colors
- **WHEN** action status badges render
- **THEN** SUCCESS uses green styling
- **AND** FAILED uses red styling
- **AND** RUNNING uses blue styling
- **AND** PARTIAL_FAILED uses amber styling

#### Scenario: Duration is displayed in human-readable format
- **WHEN** an action displays durationMs
- **THEN** it shows as "Xms" or "Xs" for longer durations (e.g., "150ms" or "2.5s")

### Requirement: Technical Details Section

The flow detail page SHALL display a technical details section.

#### Scenario: Technical details section exists
- **WHEN** the flow detail page renders
- **THEN** a "Technical Details" section is displayed below the timeline
- **AND** it has static sections or tabs for: Request, Response, Error

#### Scenario: Technical details show JSON preview
- **WHEN** the technical details section renders
- **THEN** formatted JSON preview blocks are displayed
- **AND** sensitive fields are masked (shown as "***")

#### Scenario: Helper text explains masking
- **WHEN** the technical details section renders
- **THEN** helper text "Sensitive fields are masked." is displayed

#### Scenario: No real API calls for technical details
- **WHEN** the page renders
- **THEN** no API calls are made to `/api/log-flows/{flowId}` or `/api/log-actions/{actionId}/details`
- **AND** all data comes from mock data

