# mock-data-layer Specification

## Purpose
TBD - created by archiving change implement-static-logger-ui-pages. Update Purpose after archive.
## Requirements
### Requirement: Mock Data File Structure

The system SHALL provide a centralized mock data file with TypeScript types and mock data constants.

#### Scenario: Mock data file exists
- **WHEN** the frontend is built
- **THEN** a file `frontend/src/data/mockData.ts` exists
- **AND** it exports TypeScript interfaces and mock data constants

### Requirement: Flow Mock Data Types

The mock data SHALL include TypeScript interfaces matching backend field names.

#### Scenario: MockFlow interface is defined
- **WHEN** the mock data file is imported
- **THEN** a MockFlow interface is exported with these fields:
  - flowId: string
  - flowType: string
  - checkoutType: string
  - status: 'RUNNING' | 'SUCCESS' | 'FAILED' | 'PARTIAL_FAILED'
  - customerEmail: string
  - customerPhone: string
  - orderId: string
  - paymentId: string
  - totalSteps: number
  - successSteps: number
  - failedSteps: number
  - lastActionType: string
  - lastMessage: string
  - startedAt: string
  - completedAt?: string
  - createdAt: string
  - updatedAt: string

#### Scenario: MockAction interface is defined
- **WHEN** the mock data file is imported
- **THEN** a MockAction interface is exported with these fields:
  - actionId: string
  - flowId: string
  - serviceName: string
  - actionType: string
  - status: 'RUNNING' | 'SUCCESS' | 'FAILED' | 'PARTIAL_FAILED'
  - message: string
  - durationMs: number
  - createdAt: string
  - finishedAt?: string

### Requirement: Mock Data Constants

The mock data SHALL include sample data constants for flows and actions.

#### Scenario: mockFlows constant is exported
- **WHEN** the mock data file is imported
- **THEN** a `mockFlows` array constant is exported
- **AND** it contains at least 5 sample flow objects
- **AND** each flow has realistic data for a checkout flow scenario

#### Scenario: mockActions constant is exported
- **WHEN** the mock data file is imported
- **THEN** a `mockActions` array constant is exported
- **AND** it contains at least 6 sample action objects for the primary demo flow
- **AND** actions include ORDER_CREATED, PAYMENT_REQUESTED, PAYMENT_SUCCESS, PROVIDER_REQUESTED, ESIM_ACTIVATED, EMAIL_SENT

#### Scenario: Dashboard metrics constant is exported
- **WHEN** the mock data file is imported
- **THEN** a `mockDashboardMetrics` object is exported
- **AND** it contains: totalFlows, successFlows, failedFlows, runningFlows, partialFailed, averageDurationMs

### Requirement: Mock Data Consistency

The mock data SHALL use consistent data that makes sense for a checkout flow scenario.

#### Scenario: Flow types are valid
- **WHEN** mock flows are defined
- **THEN** flowType values are: "CHECKOUT_ESIM"
- **AND** checkoutType values are: "GUEST" or "AUTHENTICATED"

#### Scenario: Action types are valid
- **WHEN** mock actions are defined
- **THEN** actionType values match the defined action types: ORDER_CREATED, PAYMENT_REQUESTED, PAYMENT_SUCCESS, PROVIDER_REQUESTED, ESIM_ACTIVATED, EMAIL_SENT
- **AND** failures are represented by status = FAILED with an error message, not by separate failed action type names

#### Scenario: Timestamps are consistent
- **WHEN** mock data is defined
- **THEN** startedAt, completedAt, createdAt, updatedAt, createdAt, finishedAt timestamps are in ISO 8601 format
- **AND** completedAt is after startedAt for completed flows

