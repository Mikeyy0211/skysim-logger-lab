## ADDED Requirements

### Requirement: Flow Monitoring Page UI

The system SHALL provide a static flow monitoring page for searching and inspecting backend processing flows.

#### Scenario: Flow monitoring page renders with header
- **WHEN** the user navigates to `/logs`
- **THEN** the page title "Flow Monitoring" is displayed
- **AND** the subtitle "Search and inspect backend processing flows" is shown

#### Scenario: Flow monitoring page displays filter bar
- **WHEN** the user views the flow monitoring page
- **THEN** a filter bar is displayed at the top with:
  - Search input with placeholder "Search by Flow ID, Order ID, Email, or Phone"
  - Status filter dropdown
  - Flow Type filter dropdown
  - Date Range filter (from/to date inputs)

#### Scenario: Flow monitoring table renders
- **WHEN** the flow monitoring page loads
- **THEN** a table is displayed with mock flow data

#### Scenario: Table columns are correctly defined
- **WHEN** the flow monitoring table renders
- **THEN** columns are: Flow ID, Flow Type, Status, Last Action, Last Message, Steps, Updated At, Actions
- **AND** no "Service Name" filter or column is included

#### Scenario: Steps column displays step information
- **WHEN** the Steps column renders
- **THEN** it displays "successSteps/totalSteps" (e.g., "5/6")
- **AND** if failedSteps > 0, the failed count is shown as secondary text (e.g., "1 failed")

#### Scenario: Actions column has View Detail button
- **WHEN** the Actions column renders
- **THEN** each row has a "View Detail" button
- **AND** clicking it navigates to `/logs/:flowId`

### Requirement: Flow monitoring uses backend-aligned data

The flow monitoring page SHALL use mock data matching backend field names.

#### Scenario: Flow data uses correct fields
- **WHEN** the page renders flow data
- **THEN** it uses these fields: flowId, flowType, checkoutType, status, customerEmail, customerPhone, orderId, paymentId, totalSteps, successSteps, failedSteps, lastActionType, lastMessage, updatedAt

#### Scenario: No real API calls
- **WHEN** the user interacts with filters
- **THEN** no API calls are made
- **AND** no Axios instance is called
- **AND** filter changes do not trigger data reload
