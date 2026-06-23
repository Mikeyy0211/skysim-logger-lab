# Capability: Logger Query API

## ADDED Requirements

### Requirement: The Logger exposes three read-only endpoints
The Logger Query API SHALL expose `GET /api/log-flows` (list), `GET /api/log-flows/{flowId}` (flow detail), and `GET /api/log-actions/{actionId}` (action detail). All endpoints SHALL return JSON.

#### Scenario: List endpoint returns paginated flows
- **WHEN** the client calls `GET /api/log-flows?page=1&pageSize=20`
- **THEN** the response body matches the pagination envelope `{ items, page, pageSize, totalItems, totalPages }` and `items` is an array of `LogFlowSummaryDto` objects

#### Scenario: Flow detail returns summary plus timeline
- **WHEN** the client calls `GET /api/log-flows/{flowId}`
- **THEN** the response returns the `LogFlowSummaryDto` plus an ordered `timeline` array of `LogActionDto` objects for that flow

#### Scenario: Action detail returns the heavy payloads
- **WHEN** the client calls `GET /api/log-actions/{actionId}`
- **THEN** the response includes `LogActionDto` plus `LogActionDetailsDto` containing masked `requestPayload`, `responsePayload`, and `errorPayload`

### Requirement: List endpoint supports filtering by the documented query parameters
`GET /api/log-flows` SHALL accept the query parameters `customerEmail`, `customerPhone`, `userId`, `orderId`, `paymentId`, `flowType`, `checkoutType`, `status`, `fromDate`, `toDate`, `page`, `pageSize`, `sortBy`, `sortDirection`. Unrecognized parameters SHALL be ignored without error.

#### Scenario: Filtering by email works
- **WHEN** `GET /api/log-flows?customerEmail=alice@example.com` is called
- **THEN** the response includes only flows whose `customer_email` matches

#### Scenario: Filtering by date range works
- **WHEN** `GET /api/log-flows?fromDate=2026-06-01&toDate=2026-06-22` is called
- **THEN** the response includes only flows whose `created_at` is in `[fromDate, toDate]` (inclusive)

#### Scenario: Combining filters ANDs them
- **WHEN** `GET /api/log-flows?customerEmail=alice@example.com&status=FAILED` is called
- **THEN** the response includes only flows matching BOTH filters

### Requirement: Pagination uses 1-based page numbers with sensible defaults and limits
`page` SHALL default to `1`. `pageSize` SHALL default to `20`, minimum `1`, maximum `100`. Invalid values SHALL be rejected with HTTP 400.

#### Scenario: Defaults apply when omitted
- **WHEN** the client calls `GET /api/log-flows` without `page` or `pageSize`
- **THEN** the response uses `page=1`, `pageSize=20`

#### Scenario: Out-of-range pageSize is rejected
- **WHEN** the client calls `GET /api/log-flows?pageSize=10000`
- **THEN** the API returns HTTP 400 with a validation error explaining the limit

### Requirement: Sorting supports a closed set of fields and asc/desc direction
`sortBy` SHALL be one of `createdAt`, `updatedAt`, `completedAt`, `status`. `sortDirection` SHALL be `asc` or `desc`. Default is `sortBy=createdAt`, `sortDirection=desc`.

#### Scenario: Sort by createdAt desc is the default
- **WHEN** the client calls `GET /api/log-flows` without `sortBy` and `sortDirection`
- **THEN** the items are sorted by `created_at` descending

#### Scenario: Unknown sortBy is rejected
- **WHEN** the client calls `GET /api/log-flows?sortBy=foo`
- **THEN** the API returns HTTP 400

### Requirement: List responses never include heavy payloads
The list endpoint SHALL NOT include `requestPayload`, `responsePayload`, or `errorPayload` from `log_action_details`. Only summary fields are exposed.

#### Scenario: List payload is light
- **WHEN** a list response is returned
- **THEN** no item contains a key matching `requestPayload`, `responsePayload`, or `errorPayload`

### Requirement: DTOs are stable and well-typed
The API SHALL expose the DTOs `LogFlowSummaryDto`, `LogActionDto`, `LogActionDetailsDto`, `PagedResponse<T>`, and `ValidationErrorResponse`. Field names SHALL be camelCase in JSON; C# models SHALL use PascalCase property names with `JsonNamingPolicy.CamelCase`.

#### Scenario: DTO field names are camelCase
- **WHEN** a `LogFlowSummaryDto` is serialized
- **THEN** JSON keys are `flowId`, `flowType`, `checkoutType`, `customerEmail`, `customerPhone`, `userId`, `orderId`, `paymentId`, `status`, `totalSteps`, `successSteps`, `failedSteps`, `lastActionType`, `lastMessage`, `startedAt`, `completedAt`, `createdAt`, `updatedAt`

#### Scenario: Validation errors follow a stable shape
- **WHEN** the API rejects a request
- **THEN** the response body matches `{ error: { code, message, details: [{ field, message }] } }` and HTTP status is 4xx

### Requirement: Authorization is a stub for phase 1
The Logger API SHALL be configured with an `[Authorize]` placeholder attribute and a development bypass flag (`Logger:Auth:DevBypass = true`). When the flag is true, requests are accepted without a token. When false, requests require a JWT validated against Keycloak's issuer.

#### Scenario: DevBypass=true allows unauthenticated requests in local dev
- **WHEN** `appsettings.Development.json` has `Logger:Auth:DevBypass = true`
- **THEN** `GET /api/log-flows` returns 200 without an `Authorization` header

#### Scenario: DevBypass=false requires a token
- **WHEN** `Logger:Auth:DevBypass = false`
- **THEN** an unauthenticated request to `GET /api/log-flows` returns 401

### Requirement: Errors are returned with proper HTTP status codes
The API SHALL return 200 for success, 400 for validation errors, 401 for missing/invalid auth, 404 when the resource does not exist, and 500 for unexpected errors. The error body SHALL always contain `{ error: { code, message } }` for non-validation cases.

#### Scenario: 404 for missing flow
- **WHEN** `GET /api/log-flows/unknown-id` is called and no such flow exists
- **THEN** the API returns HTTP 404 with `{ error: { code: "flow_not_found", message: "..." } }`

#### Scenario: 500 for unexpected error
- **WHEN** an unhandled exception occurs in a handler
- **THEN** the global exception handler returns HTTP 500 with `{ error: { code: "internal_error", message: "..." } }` and writes a server-side log entry with the exception
