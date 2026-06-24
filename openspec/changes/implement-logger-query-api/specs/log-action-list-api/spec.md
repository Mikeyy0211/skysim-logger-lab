## ADDED Requirements

### Requirement: GET /api/log-flows/{flowId}/actions returns a paginated, filterable list of actions for a specific flow
The endpoint SHALL return HTTP 200 with a JSON body matching `PagedResponse<LogActionDto>`. Results SHALL be scoped to the specified `flowId`. The `timeline` array from `GET /api/log-flows/{flowId}` and `GET /api/log-flows/{flowId}/actions` SHALL return the same data when no extra filters are applied.

#### Scenario: Returns paginated actions for a flow
- **WHEN** `GET /api/log-flows/{flowId}/actions` is called
- **THEN** the response is `PagedResponse<LogActionDto>` containing only actions belonging to that flowId

#### Scenario: Returns 404 for unknown flowId
- **WHEN** `GET /api/log-flows/unknown-flow-id/actions` is called
- **THEN** the API returns HTTP 404 with `{ error: { code: "flow_not_found", message: "..." } }`

#### Scenario: Actions are ordered by stepOrder ascending by default
- **WHEN** `GET /api/log-flows/{flowId}/actions` is called
- **THEN** actions are sorted by `stepOrder` ascending

### Requirement: GET /api/log-flows/{flowId}/actions supports filtering by serviceName
The `serviceName` filter SHALL narrow the result to actions whose `service_name` matches.

#### Scenario: Filter by serviceName
- **WHEN** `GET /api/log-flows/{flowId}/actions?serviceName=Payment` is called
- **THEN** the response includes only actions with `service_name` = `Payment`

#### Scenario: Filter by serviceName combined with pagination
- **WHEN** `GET /api/log-flows/{flowId}/actions?serviceName=Payment&page=1&pageSize=5` is called
- **THEN** the response is a paginated subset of Payment actions

### Requirement: GET /api/log-flows/{flowId}/actions never includes heavy payloads
The `items` array SHALL NOT contain `requestPayload`, `responsePayload`, or `errorPayload`.

#### Scenario: Action items have no payload fields
- **WHEN** `GET /api/log-flows/{flowId}/actions` returns actions
- **THEN** no item contains `requestPayload`, `responsePayload`, or `errorPayload`

### Requirement: LogActionDto includes serviceName
Each `LogActionDto` SHALL include `serviceName` as a top-level field with its string value.

#### Scenario: LogActionDto has serviceName field
- **WHEN** `GET /api/log-flows/{flowId}/actions` returns an action
- **THEN** the action contains `serviceName` as a non-null string
