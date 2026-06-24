## ADDED Requirements

### Requirement: GET /api/log-flows/{flowId} returns a single flow with its action timeline
The endpoint SHALL return HTTP 200 with a `LogFlowDetailDto` containing the flow summary fields plus an ordered `timeline` array of `LogActionDto` objects for that flow. The timeline SHALL be ordered by `stepOrder` ascending.

#### Scenario: Returns flow with timeline
- **WHEN** `GET /api/log-flows/{flowId}` is called with a valid existing flowId
- **THEN** the response contains the `LogFlowDetailDto` with `timeline` array ordered by `stepOrder`

#### Scenario: Returns 404 for unknown flowId
- **WHEN** `GET /api/log-flows/unknown-flow-id` is called and no such flow exists
- **THEN** the API returns HTTP 404 with `{ error: { code: "flow_not_found", message: "..." } }`

### Requirement: LogFlowDetailDto includes the timeline without heavy payloads
The `timeline` array SHALL contain `LogActionDto` objects. `requestPayload`, `responsePayload`, and `errorPayload` SHALL NOT be present in timeline items.

#### Scenario: Timeline items have no payload fields
- **WHEN** `GET /api/log-flows/{flowId}` returns a timeline
- **THEN** no timeline item contains `requestPayload`, `responsePayload`, or `errorPayload`

### Requirement: The response includes serviceName for each timeline action
Each `LogActionDto` in the timeline SHALL include the `serviceName` field.

#### Scenario: Timeline includes serviceName
- **WHEN** `GET /api/log-flows/{flowId}` returns a timeline
- **THEN** every timeline item has a `serviceName` field with a non-empty value
