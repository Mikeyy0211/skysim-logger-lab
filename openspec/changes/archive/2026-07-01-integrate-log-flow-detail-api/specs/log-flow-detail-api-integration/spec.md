# log-flow-detail-api-integration Specification

## Purpose
Integrate the Flow Detail page with real Logger.Api detail endpoints to replace mock data usage with live API data.

## ADDED Requirements

### Requirement: Flow detail page fetches real flow data

The flow detail page SHALL fetch flow detail data from the real `GET /api/log-flows/{flowId}` endpoint when the page loads.

#### Scenario: Fetch flow detail on page load
- **WHEN** user navigates to `/logs/:flowId` and is authenticated
- **THEN** system fetches `GET /api/log-flows/{flowId}`
- **AND** system displays "Loading flow detail..." while fetching

#### Scenario: Display flow detail data on success
- **WHEN** flow detail API returns successfully with data
- **THEN** system renders Flow Summary card with real data: flowId, flowType, checkoutType, status, lastActionType, lastMessage, startedAt, completedAt, updatedAt, orderId, customerEmail, customerPhone, paymentId
- **AND** system renders Metric Cards with real data: totalSteps, successSteps, failedSteps
- **AND** system displays StatusBadge with real status

#### Scenario: Display not-found state when flow does not exist
- **WHEN** flow detail API returns 404 or flow with null/nonexistent data
- **THEN** system displays "Flow not found" message
- **AND** system shows "Back to Logs" button

#### Scenario: Display error state when fetch fails
- **WHEN** flow detail API fails with non-401 error
- **THEN** system displays "Unable to load flow detail." error message
- **AND** system does not crash

### Requirement: Flow actions page fetches real action data

The flow detail page SHALL fetch action timeline data from the real `GET /api/log-flows/{flowId}/actions` endpoint when the page loads.

#### Scenario: Fetch actions on page load
- **WHEN** user navigates to `/logs/:flowId` and is authenticated
- **THEN** system fetches `GET /api/log-flows/{flowId}/actions`
- **AND** system displays loading indicator in Action Timeline section while fetching

#### Scenario: Display actions in timeline on success
- **WHEN** flow actions API returns successfully with data
- **THEN** system renders each action in API return order
- **AND** each action displays: actionId, serviceName, actionType, status, message, durationMs, createdAt
- **AND** each action shows a "View Details" button

#### Scenario: Display empty actions state
- **WHEN** flow actions API returns empty array
- **THEN** system displays "No actions found for this flow." message in timeline

#### Scenario: Display error state when actions fetch fails
- **WHEN** flow actions API fails with non-401 error
- **THEN** system displays "Unable to load actions." error message in timeline section
- **AND** system does not crash

### Requirement: Action detail loading on demand

The system SHALL load action details when user clicks "View Details" button for an action.

#### Scenario: Load details when user clicks View Details
- **WHEN** user clicks "View Details" button on an action
- **THEN** system fetches `GET /api/log-actions/{actionId}/details`
- **AND** system displays loading state in Technical Details section

#### Scenario: Display action details on success
- **WHEN** action details API returns successfully
- **THEN** system renders REQUEST payload if available
- **AND** system renders RESPONSE payload if available
- **AND** system renders ERROR payload if available

#### Scenario: Display no details available state
- **WHEN** action details API returns empty array or null
- **THEN** system displays "No details available for this action." message

#### Scenario: Display error state when details fetch fails
- **WHEN** action details API fails with non-401 error
- **THEN** system displays "Unable to load action details." error in Technical Details section

### Requirement: TypeScript types for log actions and details

The system SHALL define TypeScript interfaces aligned with backend DTOs for log actions and action details.

#### Scenario: LogAction interface includes required fields
- **GIVEN** backend returns action data with fields: actionId, flowId, serviceName, actionType, status, message, durationMs, createdAt, finishedAt
- **THEN** `LogAction` interface includes all matching fields

#### Scenario: LogActionDetail interface includes required fields
- **GIVEN** backend returns action detail data with fields: actionId, detailType, payload, masked, createdAt
- **THEN** `LogActionDetail` interface includes all matching fields

#### Scenario: ActionDetailType enum supports expected values
- **GIVEN** backend returns detailType as "REQUEST", "RESPONSE", or "ERROR"
- **THEN** `ActionDetailType` type supports these exact string values

#### Scenario: Payload field is flexible JSON-safe type
- **GIVEN** backend returns payload as various JSON-safe values (object, array, string, number, boolean, null)
- **THEN** `LogActionDetail` interface uses `unknown` type for payload field
- **AND** payload is safely stringified when rendering

#### Scenario: Service normalizes single or array response
- **WHEN** `getLogActionDetails(actionId)` is called
- **THEN** if API returns `LogActionDetail[]`, service returns the array directly
- **AND** if API returns single `LogActionDetail`, service wraps it in an array and returns
- **AND** component always receives `LogActionDetail[]`

### Requirement: Authorization header attachment for detail APIs

The detail page API calls SHALL attach the stored Bearer token to all requests.

#### Scenario: Detail API requests include Authorization header
- **WHEN** any detail API is called (flow detail, actions, action details)
- **THEN** request includes header `Authorization: Bearer <token>`

### Requirement: 401 error handling on detail page

The detail page SHALL handle 401 responses consistently with other pages.

#### Scenario: Redirect to login on 401
- **WHEN** any API returns 401 status code and user is not on `/login`
- **THEN** existing Axios interceptor removes token from localStorage
- **AND** existing Axios interceptor redirects user to `/login`

### Requirement: User-friendly error messages on detail page

The detail page SHALL display user-friendly error messages without exposing raw technical details.

#### Scenario: Show friendly error on flow fetch failure
- **WHEN** flow detail fetch fails and user is not redirected to login
- **THEN** system displays "Unable to load flow detail." in error message
- **AND** raw API error details are not shown to user

#### Scenario: Show friendly error on action fetch failure
- **WHEN** flow actions fetch fails and user is not redirected to login
- **THEN** system displays "Unable to load action details." in error message
- **AND** raw API error details are not shown to user

#### Scenario: Show friendly error on action detail fetch failure
- **WHEN** action details fetch fails and user is not redirected to login
- **THEN** system displays "Unable to load action details." in error message
- **AND** raw API error details are not shown to user

### Requirement: Field formatting and null handling

The detail page SHALL format data consistently and handle missing values gracefully.

#### Scenario: Duration displays in human-readable format
- **WHEN** an action displays durationMs
- **THEN** it shows as "Xms" for durations under 1000ms
- **OR** it shows as "Xs" for durations 1000ms or longer (e.g., "2.5s")
- **AND** if durationMs is null or undefined, display "—"

#### Scenario: Dates display in local format
- **WHEN** a date field is displayed (startedAt, completedAt, createdAt, updatedAt)
- **THEN** it is formatted using `toLocaleString()`
- **AND** if date is null or undefined, display "—"

#### Scenario: Null or missing values display as em dash
- **WHEN** a field value is null, undefined, or empty string
- **THEN** it is displayed as "—" to the user

#### Scenario: Nullable flow fields handled safely
- **GIVEN** API may return null for: checkoutType, customerEmail, customerPhone, userId, orderId, paymentId, completedAt, lastActionType, lastMessage
- **THEN** the UI renders "—" for each null/undefined field
- **AND** the page does not crash on null values

#### Scenario: Nullable action fields handled safely
- **GIVEN** API may return null for: message, durationMs, finishedAt
- **THEN** the UI renders "—" for each null/undefined field
- **AND** duration helper handles null by returning "—"
