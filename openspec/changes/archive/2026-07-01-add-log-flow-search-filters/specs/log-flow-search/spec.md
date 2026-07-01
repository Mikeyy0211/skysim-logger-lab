# log-flow-search Specification

## Purpose

Adds a unified `search` query parameter to the log flow list API that enables operators to find flows by any business identifier (email, phone, order ID, payment ID, flow ID, user ID, or last message) from a single search box. Also adds `lastServiceName` to the list response for use by the Flow Monitoring and Dashboard frontend pages. This is a backend-only change — frontend integration will be handled by a separate change.

## ADDED Requirements

### Requirement: Search parameter matches across multiple fields

The system SHALL support a `search` query parameter on `GET /api/log-flows` that performs case-insensitive partial matching across the following fields: `flowId`, `customerEmail`, `customerPhone`, `orderId`, `paymentId`, `userId`, `lastMessage`.

#### Scenario: Search matches flowId

- **WHEN** `GET /api/log-flows?search=demo-business-flow` is called
- **THEN** system returns flows where `flowId` contains "demo-business-flow" (case-insensitive)

#### Scenario: Search matches customerEmail

- **WHEN** `GET /api/log-flows?search=detail.demo@example.com` is called
- **THEN** system returns flows where `customerEmail` contains "detail.demo@example.com" (case-insensitive)

#### Scenario: Search matches customerPhone

- **WHEN** `GET /api/log-flows?search=0900000003` is called
- **THEN** system returns flows where `customerPhone` contains "0900000003" (case-insensitive)

#### Scenario: Search matches orderId

- **WHEN** `GET /api/log-flows?search=ORD` is called
- **THEN** system returns flows where `orderId` contains "ORD" (case-insensitive)

#### Scenario: Search matches paymentId

- **WHEN** `GET /api/log-flows?search=PAY` is called
- **THEN** system returns flows where `paymentId` contains "PAY" (case-insensitive)

#### Scenario: Search matches userId

- **WHEN** `GET /api/log-flows?search=user-42` is called
- **THEN** system returns flows where `userId` contains "user-42" (case-insensitive)

#### Scenario: Search matches lastMessage

- **WHEN** `GET /api/log-flows?search=payment timeout` is called
- **THEN** system returns flows where `lastMessage` contains "payment timeout" (case-insensitive)

#### Scenario: Search is case-insensitive

- **WHEN** `GET /api/log-flows?search=DEMO@EXAMPLE.COM` is called
- **THEN** system returns flows where `customerEmail` contains "demo@example.com"

#### Scenario: Search with empty value returns all results

- **WHEN** `GET /api/log-flows?search=` is called
- **THEN** system returns all flows (search is ignored)

#### Scenario: Search with no search param returns all results

- **WHEN** `GET /api/log-flows` is called without `search` parameter
- **THEN** system returns all flows (behavior unchanged from before)

### Requirement: Search combines with filters

The system SHALL combine the `search` parameter with existing filters using AND logic.

#### Scenario: Search combined with flowType filter

- **WHEN** `GET /api/log-flows?search=detail.demo@example.com&flowType=CHECKOUT_ESIM` is called
- **THEN** system returns flows matching search AND where `flowType` equals "CHECKOUT_ESIM"

#### Scenario: Search combined with status filter

- **WHEN** `GET /api/log-flows?search=detail.demo@example.com&status=SUCCESS` is called
- **THEN** system returns flows matching search AND where `status` equals "SUCCESS"

#### Scenario: Search combined with checkoutType filter

- **WHEN** `GET /api/log-flows?search=detail.demo@example.com&checkoutType=GUEST` is called
- **THEN** system returns flows matching search AND where `checkoutType` equals "GUEST"

#### Scenario: Search combined with multiple filters

- **WHEN** `GET /api/log-flows?search=detail.demo@example.com&flowType=CHECKOUT_ESIM&status=SUCCESS` is called
- **THEN** system returns flows matching search AND `flowType` equals "CHECKOUT_ESIM" AND `status` equals "SUCCESS"

### Requirement: Search validation

The system SHALL validate the `search` query parameter and return 400 Bad Request for invalid values.

#### Scenario: Search exceeding max length returns 400

- **WHEN** `GET /api/log-flows?search=<string with 200 characters>` is called
- **THEN** system returns 400 Bad Request with validation error for `search`

#### Scenario: Search with null value is ignored

- **WHEN** `GET /api/log-flows` is called without `search` parameter
- **THEN** system returns 200 OK with all flows (no validation error)

### Requirement: Enum validation for status, flowType, checkoutType

The system SHALL validate `status`, `flowType`, and `checkoutType` query parameters against known enum values and return 400 Bad Request for unknown values.

Valid status values: `SUCCESS`, `FAILED`, `RUNNING`, `PARTIAL_FAILED`
Valid flowType values: `CHECKOUT_ESIM`, `HTTP_ACTION`
Valid checkoutType values: `GUEST`, `AUTHENTICATED`

#### Scenario: Invalid status returns 400

- **WHEN** `GET /api/log-flows?status=INVALID_STATUS` is called
- **THEN** system returns 400 Bad Request with validation error listing valid status values

#### Scenario: Invalid flowType returns 400

- **WHEN** `GET /api/log-flows?flowType=INVALID_FLOW_TYPE` is called
- **THEN** system returns 400 Bad Request with validation error listing valid flowType values

#### Scenario: Invalid checkoutType returns 400

- **WHEN** `GET /api/log-flows?checkoutType=INVALID_CHECKOUT_TYPE` is called
- **THEN** system returns 400 Bad Request with validation error listing valid checkoutType values

#### Scenario: Valid status values are accepted

- **WHEN** `GET /api/log-flows?status=SUCCESS` is called
- **THEN** system returns 200 OK

#### Scenario: Valid flowType values are accepted

- **WHEN** `GET /api/log-flows?flowType=CHECKOUT_ESIM` is called
- **THEN** system returns 200 OK

#### Scenario: Valid checkoutType values are accepted

- **WHEN** `GET /api/log-flows?checkoutType=GUEST` is called
- **THEN** system returns 200 OK

### Requirement: Default sort by updatedAt descending

The system SHALL sort log flow list results by `updatedAt` descending by default so the most recently active flows appear first.

#### Scenario: Default sort is updatedAt descending

- **WHEN** `GET /api/log-flows` is called without `sortBy` parameter
- **THEN** system returns flows sorted by `updatedAt` descending

#### Scenario: Explicit sortBy overrides default

- **WHEN** `GET /api/log-flows?sortBy=createdAt` is called
- **THEN** system returns flows sorted by `createdAt` descending (respects explicit sortBy)

#### Scenario: Explicit sortDirection is respected

- **WHEN** `GET /api/log-flows?sortBy=createdAt&sortDirection=asc` is called
- **THEN** system returns flows sorted by `createdAt` ascending

### Requirement: Pagination behavior

The system SHALL support pagination with `page` and `pageSize` parameters and return correct page subsets.

#### Scenario: Page defaults to 1

- **WHEN** `GET /api/log-flows` is called without `page` parameter
- **THEN** system returns page 1

#### Scenario: PageSize defaults to 20

- **WHEN** `GET /api/log-flows` is called without `pageSize` parameter
- **THEN** system returns 20 items per page

#### Scenario: PageSize is capped at 100

- **WHEN** `GET /api/log-flows?pageSize=500` is called
- **THEN** system returns 100 items (max enforced)

#### Scenario: Pagination with search works correctly

- **WHEN** `GET /api/log-flows?search=example&page=2&pageSize=10` is called
- **THEN** system returns second page of results matching search, 10 items per page

### Requirement: lastServiceName in list response

The system SHALL include `lastServiceName` in the log flow list response. The `lastServiceName` represents the `serviceName` of the latest action for each flow, determined by the highest `StepOrder`.

#### Scenario: lastServiceName reflects the latest action by StepOrder

- **WHEN** a flow has actions with stepOrder 1 (Order service), 2 (Payment service), 3 (Provider service)
- **THEN** the list response includes `lastServiceName` = "Provider" for that flow

#### Scenario: lastServiceName is null when flow has no actions

- **WHEN** a flow has no actions
- **THEN** `lastServiceName` is null in the list response

#### Scenario: lastServiceName for CHECKOUT_ESIM flow with EMAIL_SENT action

- **WHEN** a CHECKOUT_ESIM flow has a latest action with actionType EMAIL_SENT and serviceName NotificationService
- **THEN** the list response includes `lastServiceName` = "NotificationService" for that flow

#### Scenario: lastServiceName for HTTP_ACTION flow

- **WHEN** an HTTP_ACTION flow has a latest action with actionType HTTP_REQUEST and serviceName sample-checkout-service
- **THEN** the list response includes `lastServiceName` = "sample-checkout-service" for that flow
