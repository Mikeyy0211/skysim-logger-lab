# static-dashboard-page Specification

## Purpose
TBD - created by archiving change implement-static-logger-ui-pages. Update Purpose after archive.
## Requirements
### Requirement: Dashboard Page UI

The system SHALL provide a static dashboard page with metric cards and recent flows table.

#### Scenario: Dashboard page renders with header
- **WHEN** the user navigates to `/dashboard`
- **THEN** the page title "Logger Dashboard" is displayed
- **AND** the subtitle "Monitor recent flow activity and system logging health" is shown

#### Scenario: Dashboard displays six metric cards
- **WHEN** the user views the dashboard
- **THEN** six metric cards are displayed in a grid layout
- **AND** each card shows: Total Flows, Success Flows, Failed Flows, Running Flows, Partial Failed, Average Duration
- **AND** each card displays a mock numeric value

#### Scenario: Dashboard shows recent flows section
- **WHEN** the user views the dashboard
- **THEN** a "Recent Flows" section is displayed below the metrics
- **AND** it contains a table with mock flow data

#### Scenario: Recent flows table uses correct columns
- **WHEN** the dashboard renders the recent flows table
- **THEN** the columns are: Flow ID, Flow Type, Status, Last Action, Last Message, Updated At
- **AND** no "Service Name" column is included (serviceName is action-level, not flow-level)

#### Scenario: Recent flows use backend-aligned field names
- **WHEN** the dashboard renders flow data
- **THEN** it uses these fields: flowId, flowType, status, lastActionType, lastMessage, updatedAt
- **AND** the data matches the mock data structure

### Requirement: Dashboard styling follows design principles

The dashboard SHALL use consistent styling with other pages.

#### Scenario: Metric cards have consistent styling
- **WHEN** metric cards render
- **THEN** they have white background, rounded corners, and subtle shadows
- **AND** the primary value uses larger bold text
- **AND** status colors are applied correctly (green for success, red for failed, blue for running)

#### Scenario: Status badges use correct colors
- **WHEN** status badges render on the dashboard
- **THEN** SUCCESS status uses green background and text
- **AND** FAILED status uses red background and text
- **AND** RUNNING status uses blue background and text
- **AND** PARTIAL_FAILED status uses amber background and text

