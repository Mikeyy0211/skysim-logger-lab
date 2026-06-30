## ADDED Requirements

### Requirement: Log flow list API integration

The system SHALL fetch and display log flows from the real `GET /api/log-flows` backend endpoint on the Flow Monitoring page.

#### Scenario: Load log flows on page mount

- **WHEN** user navigates to `/logs` and is authenticated
- **THEN** system fetches `GET /api/log-flows`
- **AND** system displays "Loading log flows..." while fetching
- **AND** table renders with real flow data after successful fetch

#### Scenario: Display log flows in table

- **WHEN** log flows API returns successfully with data
- **THEN** system renders each flow in the table with columns: Flow ID, Flow Type, Status, Last Action, Last Message, Steps, Updated At, Actions
- **AND** View Detail links navigate to `/logs/{flowId}`

#### Scenario: Display empty state when no flows

- **WHEN** log flows API returns empty array
- **THEN** system displays "No log flows found." message

### Requirement: Log flow API service

The system SHALL provide a dedicated service for fetching log flows using the existing axios instance.

#### Scenario: Service calls correct endpoint

- **WHEN** `getLogFlows()` is called
- **THEN** system sends GET request to `/api/log-flows` using existing `apiClient`
- **AND** system returns array of `LogFlowSummary` objects

#### Scenario: Service handles paginated response

- **WHEN** API returns `PagedResponse` with `items` array
- **THEN** service extracts and returns the `items` array

#### Scenario: Service handles plain array response

- **WHEN** API returns a plain array
- **THEN** service returns the array directly

### Requirement: TypeScript types for log flows

The system SHALL define TypeScript interfaces aligned with backend DTOs.

#### Scenario: Types match backend schema

- **GIVEN** backend returns `LogFlowSummaryDto` with fields: flowId, flowType, checkoutType, status, customerEmail, customerPhone, userId, orderId, paymentId, totalSteps, successSteps, failedSteps, lastActionType, lastMessage, startedAt, completedAt, createdAt, updatedAt
- **THEN** `LogFlowSummary` interface includes all matching fields
- **AND** `FlowStatus` type is `'RUNNING' | 'SUCCESS' | 'FAILED' | 'PARTIAL_FAILED'`
- **AND** `PagedResponse<T>` interface has items, page, pageSize, totalItems, totalPages

### Requirement: Authorization header attachment

The system SHALL attach the stored Bearer token to the log flows API request.

#### Scenario: Request includes Authorization header

- **WHEN** `GET /api/log-flows` is called
- **THEN** request includes header `Authorization: Bearer <token>`

### Requirement: 401 error handling with redirect loop prevention

The system SHALL handle 401 responses by clearing the token and redirecting to login, but only if not already on the login page.

#### Scenario: Redirect to login on 401

- **WHEN** API returns 401 status code and user is not on `/login`
- **THEN** system removes token from localStorage
- **AND** system redirects user to `/login`

#### Scenario: No redirect loop on 401

- **WHEN** API returns 401 status code and user is already on `/login`
- **THEN** system does not redirect
- **AND** user remains on `/login`

### Requirement: User-friendly error messages

The system SHALL display user-friendly error messages without exposing raw technical details.

#### Scenario: Show friendly error on fetch failure

- **WHEN** log flows fetch fails and user is not redirected to login
- **THEN** system displays "Unable to load log flows." in a red alert box
- **AND** raw API error details are not shown to user

### Requirement: Keep filters as visual-only

The system SHALL keep filter inputs visible but non-functional in this phase.

#### Scenario: Filters are non-interactive

- **WHEN** user types in search input on `/logs` page
- **THEN** no API request is triggered
- **AND** no filter state is updated

#### Scenario: Status and flow type selects are non-interactive

- **WHEN** user changes status or flow type dropdown on `/logs` page
- **THEN** no API request is triggered
- **AND** no filter state is updated
