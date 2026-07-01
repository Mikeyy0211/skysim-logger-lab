# checkout-business-action-logging Specification

## Purpose

Add business action logging to SampleService for CHECKOUT_ESIM flows, and update Logger.Api upsert merge logic to ensure business fields are not overwritten by HTTP middleware logs. This enables operations to search and trace checkout flows by customerEmail, customerPhone, orderId, or paymentId, and view the business timeline showing which service handled each action.

## ADDED Requirements

### Requirement: BusinessActionLogger shall publish checkout flow events to Kafka

The `BusinessActionLogger` SHALL publish a sequence of business action events to Kafka using the existing `IKafkaLogProducer` when a checkout eSIM request is processed. Each event SHALL represent a step in the checkout flow.

#### Scenario: BusinessActionLogger publishes all six checkout events in sequence

- **GIVEN** `BusinessActionLogger.PublishCheckoutFlowAsync` is called with valid checkout parameters
- **WHEN** the method executes
- **THEN** it SHALL publish exactly 6 events to Kafka in this order:
  1. ORDER_CREATED from OrderService
  2. PAYMENT_REQUESTED from PaymentService
  3. PAYMENT_SUCCESS from PaymentService
  4. PROVIDER_REQUESTED from CoreService
  5. ESIM_ACTIVATED from ProviderService
  6. EMAIL_SENT from NotificationService

#### Scenario: BusinessActionLogger uses same flowId for all events

- **GIVEN** `BusinessActionLogger.PublishCheckoutFlowAsync` is called with `flowId = "my-flow-001"`
- **WHEN** each of the 6 events is published
- **THEN** each event SHALL have `flowId` set to `"my-flow-001"`

#### Scenario: BusinessActionLogger sets flowType to CHECKOUT_ESIM

- **GIVEN** `BusinessActionLogger.PublishCheckoutFlowAsync` is called
- **WHEN** each event is published
- **THEN** each event SHALL have `flowType` set to the value of `FlowTypes.CheckoutEsim`

#### Scenario: BusinessActionLogger sets customerEmail and customerPhone from request

- **GIVEN** `BusinessActionLogger.PublishCheckoutFlowAsync` is called with `request.CustomerEmail = "test@example.com"` and `request.CustomerPhone = "0900123456"`
- **WHEN** each event is published
- **THEN** each event SHALL have `customerEmail` set to `"test@example.com"`
- **AND** each event SHALL have `customerPhone` set to `"0900123456"`

#### Scenario: BusinessActionLogger sets orderId from parameter

- **GIVEN** `BusinessActionLogger.PublishCheckoutFlowAsync` is called with `orderId = "ORD-abc123"`
- **WHEN** ORDER_CREATED event is published
- **THEN** the event SHALL have `orderId` set to `"ORD-abc123"`

#### Scenario: BusinessActionLogger sets paymentId for payment-related events

- **GIVEN** `BusinessActionLogger.PublishCheckoutFlowAsync` is called with `paymentId = "PAY-xyz789"`
- **WHEN** PAYMENT_REQUESTED event is published
- **THEN** the event SHALL have `paymentId` set to `"PAY-xyz789"`
- **AND** PAYMENT_SUCCESS event SHALL have `paymentId` set to `"PAY-xyz789"`

#### Scenario: BusinessActionLogger sets checkoutType based on parameter

- **GIVEN** `BusinessActionLogger.PublishCheckoutFlowAsync` is called with `checkoutType = CheckoutTypes.Guest`
- **WHEN** each event is published
- **THEN** each event SHALL have `checkoutType` set to `"GUEST"`

- **GIVEN** `BusinessActionLogger.PublishCheckoutFlowAsync` is called with `checkoutType = CheckoutTypes.Authenticated`
- **WHEN** each event is published
- **THEN** each event SHALL have `checkoutType` set to `"AUTHENTICATED"`

#### Scenario: BusinessActionLogger sets userId to null

- **GIVEN** `BusinessActionLogger.PublishCheckoutFlowAsync` is called
- **WHEN** each event is published
- **THEN** each event SHALL have `userId` set to `null`

#### Scenario: BusinessActionLogger sets status to SUCCESS for all events

- **GIVEN** `BusinessActionLogger.PublishCheckoutFlowAsync` is called
- **WHEN** each event is published
- **THEN** each event SHALL have `status` set to the value of `StatusTypes.Success`

#### Scenario: BusinessActionLogger generates unique eventId for each event

- **GIVEN** `BusinessActionLogger.PublishCheckoutFlowAsync` is called
- **WHEN** events are published
- **THEN** each event SHALL have a unique `eventId` (GUID)
- **AND** no two events SHALL share the same `eventId`

#### Scenario: BusinessActionLogger sets descriptive message for each action type

- **GIVEN** `BusinessActionLogger.PublishCheckoutFlowAsync` is called
- **WHEN** ORDER_CREATED event is published
- **THEN** the message SHALL contain `"Order created"` or similar
- **AND** PAYMENT_REQUESTED event message SHALL contain `"Payment requested"` or similar
- **AND** PAYMENT_SUCCESS event message SHALL contain `"Payment successful"` or similar
- **AND** PROVIDER_REQUESTED event message SHALL contain `"Provider requested"` or similar
- **AND** ESIM_ACTIVATED event message SHALL contain `"eSIM activated"` or similar
- **AND** EMAIL_SENT event message SHALL contain `"Email sent"` or similar

#### Scenario: BusinessActionLogger handles publish errors gracefully

- **GIVEN** `BusinessActionLogger.PublishCheckoutFlowAsync` is called
- **AND** Kafka is temporarily unavailable
- **WHEN** `KafkaLogProducer.PublishAsync` throws an exception
- **THEN** `BusinessActionLogger` SHALL log the error
- **AND** `BusinessActionLogger` SHALL NOT throw the exception to the caller
- **AND** the method SHALL complete without raising an exception

---

### Requirement: CheckoutController shall await BusinessActionLogger publishing

The `CheckoutController` SHALL await `BusinessActionLogger.PublishCheckoutFlowAsync` before returning the HTTP response. The method catches errors internally, so checkout response succeeds even if publishing fails.

#### Scenario: Controller awaits business logging before returning

- **GIVEN** a client sends a POST request to `/api/checkout/esim`
- **WHEN** the controller processes the request successfully
- **THEN** the controller SHALL await `BusinessActionLogger.PublishCheckoutFlowAsync`
- **AND** the HTTP response SHALL be returned only after business logging completes

#### Scenario: Controller passes orderId from response to business logger

- **GIVEN** a client sends a POST request to `/api/checkout/esim`
- **WHEN** the controller processes the request
- **THEN** the controller SHALL pass the generated `orderId` to `BusinessActionLogger.PublishCheckoutFlowAsync`

#### Scenario: Controller passes paymentId to business logger

- **GIVEN** a client sends a POST request to `/api/checkout/esim`
- **WHEN** the controller processes the request
- **THEN** the controller SHALL generate a `paymentId` matching pattern `"PAY-{GUID}"`
- **AND** the controller SHALL pass the `paymentId` to `BusinessActionLogger.PublishCheckoutFlowAsync`

#### Scenario: Controller passes request fields to business logger

- **GIVEN** a client sends a POST request to `/api/checkout/esim` with body containing `customerEmail`, `customerPhone`, `packageCode`, `quantity`
- **WHEN** the controller processes the request
- **THEN** the controller SHALL pass `request.CustomerEmail` to `BusinessActionLogger`
- **AND** the controller SHALL pass `request.CustomerPhone` to `BusinessActionLogger`

#### Scenario: Controller passes flowId from header to business logger

- **GIVEN** a client sends a POST request to `/api/checkout/esim` with header `X-Flow-Id: demo-flow-001`
- **WHEN** the controller processes the request
- **THEN** the controller SHALL pass `"demo-flow-001"` to `BusinessActionLogger.PublishCheckoutFlowAsync`

#### Scenario: Controller includes paymentId in response body

- **GIVEN** a client sends a POST request to `/api/checkout/esim`
- **WHEN** the controller processes the request successfully
- **THEN** the response SHALL include a `paymentId` field
- **AND** the `paymentId` SHALL match pattern `"PAY-{GUID}"`

#### Scenario: Checkout response succeeds even if business logging fails

- **GIVEN** a client sends a POST request to `/api/checkout/esim`
- **AND** `BusinessActionLogger.PublishCheckoutFlowAsync` throws an exception
- **WHEN** the controller processes the request
- **THEN** the HTTP response SHALL still return HTTP 200 with the checkout data
- **AND** the error SHALL be logged by `BusinessActionLogger`

---

### Requirement: CheckoutController response shall include paymentId field

The `CheckoutEsimResponse` SHALL include a `paymentId` field to allow clients to see the generated payment ID.

#### Scenario: Response includes paymentId field

- **GIVEN** a client sends a POST request to `/api/checkout/esim`
- **WHEN** the request is processed successfully
- **THEN** the response SHALL contain a `paymentId` field with a non-empty string value

---

### Requirement: BusinessActionLogger shall be registered as scoped service

The `BusinessActionLogger` SHALL be registered in the DI container as a scoped service to enable proper disposal and logging scope.

#### Scenario: BusinessActionLogger is registered as scoped service

- **GIVEN** the `Program.cs` is examined
- **WHEN** the DI registration code is located
- **THEN** `services.AddScoped<IBusinessActionLogger, BusinessActionLogger>()` SHALL be called

#### Scenario: BusinessActionLogger is injected into CheckoutController

- **GIVEN** the `CheckoutController` constructor is examined
- **WHEN** the constructor parameters are listed
- **THEN** there SHALL be a parameter of type `IBusinessActionLogger`

---

## MODIFIED Requirements

### Requirement: KafkaLogConsumerService shall merge business fields on upsert conflict

The `MapFlowFromMessage` method SHALL merge business fields on upsert conflict, preserving existing non-null values and upgrading flowType from HTTP_ACTION to CHECKOUT_ESIM when a business event arrives.

#### Scenario: HTTP_ACTION creates flow, then CHECKOUT_ESIM updates business fields

- **GIVEN** a LogFlow does not exist for `flowId = "test-flow-001"`
- **WHEN** an HTTP_ACTION event arrives with `customerEmail = null`, `flowType = HTTP_ACTION`
- **THEN** a new LogFlow SHALL be created with `flowType = HTTP_ACTION`
- **AND** `customerEmail` SHALL be `null`
- **WHEN** a CHECKOUT_ESIM event arrives with `customerEmail = "test@example.com"`, `orderId = "ORD-123"`
- **THEN** the existing flow SHALL be updated
- **AND** `flowType` SHALL be upgraded to `CHECKOUT_ESIM`
- **AND** `customerEmail` SHALL be set to `"test@example.com"`
- **AND** `orderId` SHALL be set to `"ORD-123"`

#### Scenario: CHECKOUT_ESIM exists, HTTP_ACTION arrives later and must not clear business fields

- **GIVEN** a LogFlow exists for `flowId = "test-flow-002"` with:
  - `flowType = CHECKOUT_ESIM`
  - `customerEmail = "existing@example.com"`
  - `orderId = "ORD-existing"`
  - `lastActionType = EMAIL_SENT`
- **WHEN** an HTTP_ACTION event arrives with `customerEmail = null`
- **THEN** the existing flow SHALL be updated
- **AND** `flowType` SHALL remain `CHECKOUT_ESIM` (not downgraded)
- **AND** `customerEmail` SHALL remain `"existing@example.com"` (not cleared)
- **AND** `orderId` SHALL remain `"ORD-existing"` (not cleared)
- **AND** `lastActionType` SHALL remain `EMAIL_SENT` (preserved)

#### Scenario: CHECKOUT_ESIM exists, HTTP_ACTION arrives later and must not downgrade flowType

- **GIVEN** a LogFlow exists for `flowId = "test-flow-003"` with `flowType = CHECKOUT_ESIM`
- **WHEN** an HTTP_ACTION event arrives
- **THEN** the existing flow SHALL be updated
- **AND** `flowType` SHALL remain `CHECKOUT_ESIM`

#### Scenario: HTTP_ACTION-only flow still works (existing behavior preserved)

- **GIVEN** a LogFlow does not exist for `flowId = "http-only-flow"`
- **WHEN** an HTTP_ACTION event arrives with `customerEmail = null`, `flowType = HTTP_ACTION`
- **THEN** a new LogFlow SHALL be created with `flowType = HTTP_ACTION`
- **AND** all fields SHALL be set from the incoming event

#### Scenario: Business fields are merged using incoming non-null values

- **GIVEN** a LogFlow exists for `flowId = "test-flow-004"` with:
  - `customerEmail = "existing@example.com"`
  - `customerPhone = null`
  - `orderId = null`
- **WHEN** a CHECKOUT_ESIM event arrives with:
  - `customerEmail = null`
  - `customerPhone = "0900123456"`
  - `orderId = "ORD-new"`
- **THEN** the existing flow SHALL be updated
- **AND** `customerEmail` SHALL remain `"existing@example.com"` (incoming was null)
- **AND** `customerPhone` SHALL be set to `"0900123456"` (incoming was non-null)
- **AND** `orderId` SHALL be set to `"ORD-new"` (incoming was non-null)

#### Scenario: Merge applies to all nullable business fields

- **GIVEN** a LogFlow exists with all business fields set to non-null values
- **WHEN** an HTTP_REQUEST event arrives with all business fields as null
- **THEN** `checkoutType` SHALL remain its existing value
- **AND** `customerEmail` SHALL remain its existing value
- **AND** `customerPhone` SHALL remain its existing value
- **AND** `userId` SHALL remain its existing value
- **AND** `orderId` SHALL remain its existing value
- **AND** `paymentId` SHALL remain its existing value

#### Scenario: HTTP_ACTION-only flow should still set lastActionType = HTTP_REQUEST

- **GIVEN** a LogFlow does not exist for `flowId = "http-only-flow"`
- **WHEN** an HTTP_REQUEST event arrives for a flow that will remain HTTP_ACTION-only
- **THEN** a new LogFlow SHALL be created with `flowType = HTTP_ACTION`
- **AND** `lastActionType` SHALL be set to `HTTP_REQUEST`
- **AND** `lastMessage` SHALL be set from the HTTP middleware message

#### Scenario: CHECKOUT_ESIM flow preserves lastActionType when HTTP_REQUEST arrives later

- **GIVEN** a LogFlow exists for `flowId = "business-flow"` with:
  - `flowType = CHECKOUT_ESIM`
  - `lastActionType = EMAIL_SENT`
  - `lastMessage = "Email sent successfully"`
- **WHEN** an HTTP_REQUEST event arrives later
- **THEN** `lastActionType` SHALL remain `EMAIL_SENT`
- **AND** `lastMessage` SHALL remain `"Email sent successfully"`
- **AND** `lastActionType` SHALL NOT be overwritten with `HTTP_REQUEST`
