## ADDED Requirements

### Requirement: GET /api/log-actions/{actionId}/details returns action summary plus masked payload details
The endpoint SHALL return HTTP 200 with `LogActionDetailsDto` containing the `LogActionDto` fields plus `LogActionDetailsDto.requestPayload`, `LogActionDetailsDto.responsePayload`, and `LogActionDetailsDto.errorPayload`. Payloads SHALL be masked using the existing `SensitiveDataMasker` before being returned.

#### Scenario: Returns action with masked payloads
- **WHEN** `GET /api/log-actions/{actionId}/details` is called with a valid existing actionId
- **THEN** the response contains the `LogActionDto` fields plus masked `requestPayload`, `responsePayload`, `errorPayload`, and `metadata`

#### Scenario: Returns 404 for unknown actionId
- **WHEN** `GET /api/log-actions/00000000-0000-0000-0000-000000000000/details` is called
- **THEN** the API returns HTTP 404 with `{ error: { code: "action_not_found", message: "..." } }`

#### Scenario: Sensitive fields in payloads are masked
- **WHEN** an action has `requestPayload` containing `{"password": "secret123"}`
- **THEN** the returned `requestPayload` contains `{"password": "***"}`

#### Scenario: Null payloads return null in response
- **WHEN** an action has no `requestPayload`
- **THEN** the returned `requestPayload` is `null` (not omitted)

#### Scenario: Non-sensitive payloads are returned unchanged
- **WHEN** an action has `requestPayload` containing `{"orderId": "ORD-123", "amount": 9.99}`
- **THEN** the returned `requestPayload` contains the original values unchanged

### Requirement: LogActionDetailsDto includes serviceName from the parent action
The `serviceName` field SHALL be present in the `LogActionDto` portion of the response.

#### Scenario: Details response includes serviceName
- **WHEN** `GET /api/log-actions/{actionId}/details` returns an action
- **THEN** the action portion of the response contains `serviceName`

### Requirement: Metadata is also masked if it contains sensitive fields
The `metadata` JSON field SHALL be processed by `SensitiveDataMasker` before being returned.

#### Scenario: Sensitive fields in metadata are masked
- **WHEN** an action has `metadata` containing `{"authorization": "Bearer token123"}`
- **THEN** the returned `metadata` contains `{"authorization": "***"}`
