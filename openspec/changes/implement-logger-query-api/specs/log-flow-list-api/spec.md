## ADDED Requirements

### Requirement: GET /api/log-flows returns a paginated, filterable list of flow summaries
The endpoint SHALL return HTTP 200 with a JSON body matching the `PagedResponse<LogFlowSummaryDto>` envelope `{ items, page, pageSize, totalItems, totalPages }`. Each `item` in the `items` array SHALL be a `LogFlowSummaryDto` object.

#### Scenario: Returns paginated items with default pagination
- **WHEN** a client calls `GET /api/log-flows`
- **THEN** the response uses `page=1`, `pageSize=20`, sorted by `createdAt` descending

#### Scenario: Pagination parameters are respected
- **WHEN** a client calls `GET /api/log-flows?page=3&pageSize=10`
- **THEN** the response contains `page=3`, `pageSize=10`, and at most 10 items

#### Scenario: pageSize maximum is enforced
- **WHEN** a client calls `GET /api/log-flows?pageSize=200`
- **THEN** the API returns HTTP 400 with a validation error

#### Scenario: page minimum is enforced
- **WHEN** a client calls `GET /api/log-flows?page=0`
- **THEN** the API returns HTTP 400 with a validation error

#### Scenario: Empty result returns empty items array
- **WHEN** no flows match the filter criteria
- **THEN** the response is HTTP 200 with `{ items: [], page, pageSize, totalItems: 0, totalPages: 0 }`

### Requirement: GET /api/log-flows supports filtering by customerEmail, customerPhone, userId, orderId, paymentId, flowType, checkoutType, status, serviceName, fromDate, and toDate
Each filter parameter that is provided SHALL narrow the result set. Filters SHALL be combined with AND logic. Unrecognized query parameters SHALL be ignored without error.

#### Scenario: Filter by customerEmail
- **WHEN** `GET /api/log-flows?customerEmail=alice@example.com` is called
- **THEN** the response includes only flows whose `customer_email` equals the value (case-insensitive)

#### Scenario: Filter by customerPhone
- **WHEN** `GET /api/log-flows?customerPhone=+1234567890` is called
- **THEN** the response includes only flows whose `customer_phone` equals the value

#### Scenario: Filter by userId
- **WHEN** `GET /api/log-flows?userId=user_abc` is called
- **THEN** the response includes only flows whose `user_id` equals the value

#### Scenario: Filter by orderId
- **WHEN** `GET /api/log-flows?orderId=ORD-123` is called
- **THEN** the response includes only flows whose `order_id` equals the value

#### Scenario: Filter by paymentId
- **WHEN** `GET /api/log-flows?paymentId=PAY-456` is called
- **THEN** the response includes only flows whose `payment_id` equals the value

#### Scenario: Filter by status
- **WHEN** `GET /api/log-flows?status=Failed` is called
- **THEN** the response includes only flows whose `status` equals the value

#### Scenario: Filter by flowType
- **WHEN** `GET /api/log-flows?flowType=CheckoutEsim` is called
- **THEN** the response includes only flows whose `flow_type` equals the value

#### Scenario: Filter by checkoutType
- **WHEN** `GET /api/log-flows?checkoutType=Guest` is called
- **THEN** the response includes only flows whose `checkout_type` equals the value

#### Scenario: Filter by date range (fromDate only)
- **WHEN** `GET /api/log-flows?fromDate=2026-06-01` is called
- **THEN** the response includes only flows whose `created_at` >= `fromDate` 00:00:00 UTC

#### Scenario: Filter by date range (toDate only)
- **WHEN** `GET /api/log-flows?toDate=2026-06-22` is called
- **THEN** the response includes only flows whose `created_at` <= `toDate` 23:59:59 UTC

#### Scenario: Filter by date range (both bounds)
- **WHEN** `GET /api/log-flows?fromDate=2026-06-01&toDate=2026-06-22` is called
- **THEN** the response includes only flows whose `created_at` is in the inclusive range

#### Scenario: Filter by serviceName (joined from log_actions)
- **WHEN** `GET /api/log-flows?serviceName=Payment` is called
- **THEN** the response includes only flows that have at least one action with `service_name` = `Payment`

#### Scenario: Multiple filters are combined with AND
- **WHEN** `GET /api/log-flows?customerEmail=alice@example.com&status=Failed` is called
- **THEN** the response includes only flows matching BOTH conditions

### Requirement: GET /api/log-flows supports sorting by createdAt, updatedAt, completedAt, and status
The `sortBy` parameter SHALL accept one of these values. The `sortDirection` parameter SHALL accept `asc` or `desc`. Default is `createdAt` descending.

#### Scenario: Sort by createdAt descending is the default
- **WHEN** a client calls `GET /api/log-flows` without sort parameters
- **THEN** items are sorted by `created_at` descending

#### Scenario: Sort by createdAt ascending
- **WHEN** `GET /api/log-flows?sortBy=createdAt&sortDirection=asc` is called
- **THEN** items are sorted by `created_at` ascending

#### Scenario: Sort by status descending
- **WHEN** `GET /api/log-flows?sortBy=status&sortDirection=desc` is called
- **THEN** items are sorted by `status` descending

#### Scenario: Unknown sortBy is rejected
- **WHEN** `GET /api/log-flows?sortBy=unknownField` is called
- **THEN** the API returns HTTP 400

### Requirement: GET /api/log-flows never includes heavy payloads in list responses
The `items` array SHALL NOT contain `requestPayload`, `responsePayload`, or `errorPayload` keys. Only summary fields are exposed.

#### Scenario: List response has no payload fields
- **WHEN** `GET /api/log-flows` returns a flow
- **THEN** no item contains a key matching `requestPayload`, `responsePayload`, or `errorPayload`

### Requirement: LogFlowSummaryDto JSON field names are camelCase
The JSON representation SHALL use camelCase field names: `flowId`, `flowType`, `checkoutType`, `customerEmail`, `customerPhone`, `userId`, `orderId`, `paymentId`, `status`, `totalSteps`, `successSteps`, `failedSteps`, `lastActionType`, `lastMessage`, `startedAt`, `completedAt`, `createdAt`, `updatedAt`.

#### Scenario: Response uses camelCase field names
- **WHEN** `GET /api/log-flows` returns a flow
- **THEN** JSON keys are camelCase as specified

### Requirement: PagedResponse uses a stable envelope structure
The envelope SHALL contain exactly: `items`, `page`, `pageSize`, `totalItems`, `totalPages`.

#### Scenario: PagedResponse envelope is correct
- **WHEN** `GET /api/log-flows?page=1&pageSize=20` is called with 50 total flows
- **THEN** the response contains `{ items: [...], page: 1, pageSize: 20, totalItems: 50, totalPages: 3 }`
