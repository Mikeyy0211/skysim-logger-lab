# log-flow-list-api-integration Specification

## Purpose
This spec has been modified to upgrade filter functionality from visual-only (non-functional) to functional API-driven search and filter.

## MODIFIED Requirements

### Requirement: Pass search and filter params to Logger API

The `logFlowListParams` interface SHALL accept `search`, `status`, `flowType`, and `checkoutType` parameters in addition to `page` and `pageSize`. These parameters are passed to `GET /api/log-flows`.

#### Scenario: Service accepts search parameter

- **WHEN** `getLogFlows({ search: 'jane@example.com' })` is called
- **THEN** service sends `GET /api/log-flows?search=jane%40example.com`

#### Scenario: Service accepts status parameter

- **WHEN** `getLogFlows({ status: 'SUCCESS' })` is called
- **THEN** service sends `GET /api/log-flows?status=SUCCESS`

#### Scenario: Service accepts flowType parameter

- **WHEN** `getLogFlows({ flowType: 'CHECKOUT_ESIM' })` is called
- **THEN** service sends `GET /api/log-flows?flowType=CHECKOUT_ESIM`

#### Scenario: Service accepts checkoutType parameter

- **WHEN** `getLogFlows({ checkoutType: 'GUEST' })` is called
- **THEN** service sends `GET /api/log-flows?checkoutType=GUEST`

#### Scenario: Service accepts combined parameters

- **WHEN** `getLogFlows({ search: 'test', status: 'SUCCESS', flowType: 'CHECKOUT_ESIM', checkoutType: 'GUEST', page: 1, pageSize: 10 })` is called
- **THEN** service sends `GET /api/log-flows?search=test&status=SUCCESS&flowType=CHECKOUT_ESIM&checkoutType=GUEST&page=1&pageSize=10`

#### Scenario: Undefined parameters are omitted

- **WHEN** `getLogFlows({ search: 'test', page: 1 })` is called with no other params
- **THEN** service sends only `search` and `page` in the query string
- **AND** `status`, `flowType`, `checkoutType`, `pageSize` are not sent

#### Scenario: lastServiceName is returned in LogFlowSummary

- **WHEN** `getLogFlows()` is called and API returns items with `lastServiceName`
- **THEN** `LogFlowSummary` objects include `lastServiceName` field
