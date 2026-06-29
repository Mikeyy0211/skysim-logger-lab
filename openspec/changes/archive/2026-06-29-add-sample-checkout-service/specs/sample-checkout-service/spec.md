# sample-checkout-service Specification

## Purpose

This specification defines a minimal demo service that demonstrates external service integration with `Skysim.Logger.Client`. The service exposes a single checkout eSIM endpoint that captures HTTP request/response logs via `LoggerMiddleware` and publishes them to Kafka.

**Scope of this phase:**
- HTTP request/response logging via `LoggerMiddleware`
- Single demo endpoint
- Checkout type determination from Authorization header presence

**Explicitly out of scope:**
- Multi-step business action logging (ORDER_CREATED, PAYMENT_REQUESTED, etc.)
- BusinessLogPublisher, BusinessActionFilter, or manual Kafka publishing
- JWT validation or userId extraction
- Database persistence
- Real checkout, payment, provider, or notification logic

## ADDED Requirements

### Requirement: SampleCheckoutService shall be added as a separate backend project

The `Skysim.Logger.SampleService` project SHALL be created as a new ASP.NET Core Web API project under `backend/Skysim.Logger.SampleService/`. The project SHALL reference only `Skysim.Logger.Client` and `Skysim.Logger.Contracts`.

#### Scenario: SampleService project references only allowed projects

- **GIVEN** the `Skysim.Logger.SampleService.csproj` file is examined
- **WHEN** all `<ProjectReference>` elements are listed
- **THEN** there SHALL be a reference to `Skysim.Logger.Client`
- **AND** there SHALL be a reference to `Skysim.Logger.Contracts`

#### Scenario: SampleService project has no forbidden references

- **GIVEN** the `Skysim.Logger.SampleService.csproj` file is examined
- **WHEN** all `<ProjectReference>` elements are listed
- **THEN** there SHALL NOT be any reference to `Skysim.Logger.Api`
- **AND** there SHALL NOT be any reference to `Skysim.Logger.Infrastructure`
- **AND** there SHALL NOT be any reference to `Skysim.Logger.Common`

#### Scenario: SampleService project structure is correct

- **GIVEN** the folder structure of `Skysim.Logger.SampleService` is examined
- **WHEN** the directory is searched for `.cs` files
- **THEN** there SHALL be a `Program.cs` file at the project root
- **AND** there SHALL be a `Controllers/` folder containing `CheckoutController.cs`
- **AND** there SHALL be a `DTOs/` folder containing `CheckoutEsimRequest.cs` and `CheckoutEsimResponse.cs`

#### Scenario: SampleService is added to the solution file

- **GIVEN** the `Skysim.Logger.sln` file is examined
- **WHEN** all project entries are listed
- **THEN** there SHALL be an entry for `Skysim.Logger.SampleService`

---

### Requirement: SampleCheckoutService shall expose POST /api/checkout/esim endpoint

The `CheckoutController` SHALL expose a `POST /api/checkout/esim` endpoint that accepts a checkout request and returns a mock checkout response. This endpoint is for demo purposes only and does not execute real checkout logic.

#### Scenario: Endpoint accepts POST requests with JSON body

- **GIVEN** a client sends an HTTP POST request to `/api/checkout/esim`
- **AND** the request has `Content-Type: application/json`
- **AND** the request body contains valid JSON matching `CheckoutEsimRequest`
- **WHEN** the request is processed
- **THEN** the endpoint SHALL return HTTP 200 with JSON body matching `CheckoutEsimResponse`

#### Scenario: Endpoint returns flowId in response

- **GIVEN** a client sends an HTTP POST request to `/api/checkout/esim`
- **WHEN** the request is processed successfully
- **THEN** the response SHALL include a `flowId` field
- **AND** the `flowId` SHALL be a non-empty GUID string

#### Scenario: Endpoint returns orderId in response

- **GIVEN** a client sends an HTTP POST request to `/api/checkout/esim`
- **WHEN** the request is processed successfully
- **THEN** the response SHALL include an `orderId` field
- **AND** the `orderId` SHALL be a non-empty string

#### Scenario: Endpoint returns checkoutType as GUEST when Authorization header is absent

- **GIVEN** a client sends an HTTP POST request to `/api/checkout/esim`
- **AND** the request does NOT include an `Authorization` header
- **WHEN** the request is processed
- **THEN** the response `checkoutType` SHALL be `"GUEST"`

#### Scenario: Endpoint returns checkoutType as AUTHENTICATED when Authorization header is present

- **GIVEN** a client sends an HTTP POST request to `/api/checkout/esim`
- **AND** the request includes an `Authorization` header with any value
- **WHEN** the request is processed
- **THEN** the response `checkoutType` SHALL be `"AUTHENTICATED"`

#### Scenario: Endpoint returns success status and message

- **GIVEN** a client sends an HTTP POST request to `/api/checkout/esim`
- **WHEN** the request is processed
- **THEN** the response SHALL include `status` field with value `"Success"`
- **AND** the response SHALL include `message` field with a non-empty string

---

### Requirement: SampleCheckoutService shall not validate JWT tokens

The `CheckoutController` SHALL NOT validate JWT tokens in this phase. The presence of an `Authorization` header is used only to determine checkout type, not to authenticate or authorize the request.

#### Scenario: Controller does not validate Authorization header value

- **GIVEN** a client sends an HTTP POST request to `/api/checkout/esim`
- **AND** the request includes an `Authorization` header with any arbitrary value (e.g., "Bearer invalid-token")
- **WHEN** the request is processed
- **THEN** the controller SHALL accept the request
- **AND** the controller SHALL NOT throw an authentication exception
- **AND** the response SHALL have `checkoutType = "AUTHENTICATED"`

#### Scenario: Controller does not extract userId from JWT

- **GIVEN** a client sends an HTTP POST request to `/api/checkout/esim`
- **AND** the request includes an `Authorization` header with a valid JWT containing `sub` claim
- **WHEN** the request is processed
- **THEN** the controller SHALL NOT extract or use the `sub` claim value
- **AND** the response SHALL have `checkoutType = "AUTHENTICATED"` but `userId` SHALL NOT be included

#### Scenario: JWT validation is deferred

- **GIVEN** the specification for this phase is examined
- **WHEN** JWT validation behavior is reviewed
- **THEN** JWT validation SHALL NOT be implemented in this phase
- **AND** JWT validation is deferred to `add-logger-api-auth-integration`

---

### Requirement: SampleCheckoutService shall use LoggerMiddleware for HTTP request/response logging

The `Program.cs` SHALL register `LoggerMiddleware` from `Skysim.Logger.Client` in the ASP.NET Core request pipeline to capture HTTP request/response logs. This phase captures HTTP-level logging only; business action logging is not implemented.

#### Scenario: LoggerMiddleware is registered in the pipeline

- **GIVEN** the `Program.cs` is examined
- **WHEN** the middleware registration code is located
- **THEN** `app.UseMiddleware<LoggerMiddleware>()` SHALL be called
- **AND** the middleware SHALL be registered before `app.MapControllers()`

#### Scenario: LoggerMiddleware uses KafkaLogProducer

- **GIVEN** `KafkaLogProducer` is registered in DI
- **AND** `LoggerMiddleware` is registered in the pipeline
- **WHEN** an HTTP request is processed
- **THEN** `LoggerMiddleware` SHALL use the injected `IKafkaLogProducer` to publish log events

#### Scenario: LoggerMiddleware is configured with sample-checkout-service service name

- **GIVEN** `KafkaLogProducer` is instantiated
- **WHEN** the `serviceName` parameter is set
- **THEN** the service name SHALL be `"sample-checkout-service"`

#### Scenario: LoggerMiddleware captures request body containing checkout metadata

- **GIVEN** a client sends an HTTP POST request to `/api/checkout/esim`
- **AND** the request body contains JSON with `customerEmail`, `customerPhone`, `packageCode`, and `quantity`
- **WHEN** the request is processed
- **THEN** `LoggerMiddleware` SHALL publish a `LogEventMessage` with `requestData.body` containing the checkout metadata

#### Scenario: LoggerMiddleware captures response body containing checkout result

- **GIVEN** a client sends an HTTP POST request to `/api/checkout/esim`
- **WHEN** the request is processed successfully
- **THEN** `LoggerMiddleware` SHALL publish a `LogEventMessage` with `responseData.body` containing `flowId`, `orderId`, `checkoutType`, `status`, and `message`

#### Scenario: LoggerMiddleware excludes only paths defined in default excluded paths

- **GIVEN** `LoggerMiddleware` is configured with default excluded paths
- **AND** a client sends an HTTP POST request to `/api/checkout/esim`
- **WHEN** the request is processed
- **THEN** the request SHALL be logged (not excluded)
- **AND** a `LogEventMessage` SHALL be published to Kafka

---

### Requirement: SampleCheckoutService shall configure KafkaLogProducer correctly

The `Program.cs` SHALL configure and register `KafkaLogProducer` as `IKafkaLogProducer` in the DI container using settings from `appsettings.json`.

#### Scenario: KafkaLogProducer is registered as singleton

- **GIVEN** the `Program.cs` is examined
- **WHEN** the DI registration code is located
- **THEN** `services.AddSingleton<IKafkaLogProducer, KafkaLogProducer>()` SHALL be called

#### Scenario: KafkaLogProducer reads bootstrap servers from configuration

- **GIVEN** the `appsettings.json` is examined
- **AND** it contains `Kafka:BootstrapServers` setting
- **WHEN** `KafkaLogProducer` is instantiated
- **THEN** the `bootstrapServers` parameter SHALL be read from the configuration

#### Scenario: KafkaLogProducer publishes to skysim.action.logs topic

- **GIVEN** `KafkaLogProducer` is configured
- **AND** a `LogEventMessage` is created by `LoggerMiddleware`
- **WHEN** the message is published
- **THEN** the message SHALL be sent to the `skysim.action.logs` Kafka topic

#### Scenario: KafkaLogProducer uses flowId as message key

- **GIVEN** a `LogEventMessage` is created with `flowId` = `"my-flow-123"`
- **WHEN** `KafkaLogProducer.PublishAsync` is called
- **THEN** the Kafka message key SHALL be `"my-flow-123"`

---

### Requirement: SampleCheckoutService shall determine checkout type from Authorization header presence

The `CheckoutController` SHALL determine the checkout type based on the presence of an `Authorization` header in the incoming request. This is a simple header presence check, not JWT validation.

#### Scenario: CheckoutType is GUEST when Authorization header is missing

- **GIVEN** an HTTP POST request to `/api/checkout/esim`
- **AND** the request does NOT contain an `Authorization` header
- **WHEN** the controller processes the request
- **THEN** the response `checkoutType` SHALL be set to the value of `CheckoutTypes.Guest`

#### Scenario: CheckoutType is AUTHENTICATED when Authorization header is present

- **GIVEN** an HTTP POST request to `/api/checkout/esim`
- **AND** the request contains an `Authorization` header with any value
- **WHEN** the controller processes the request
- **THEN** the response `checkoutType` SHALL be set to the value of `CheckoutTypes.Authenticated`

---

### Requirement: SampleCheckoutService shall not implement real checkout, payment, provider, or database logic

The `CheckoutController` SHALL return a simulated successful response without executing any real business logic.

#### Scenario: Controller returns immediately without external calls

- **GIVEN** a client sends an HTTP POST request to `/api/checkout/esim`
- **WHEN** the request is processed
- **THEN** the controller SHALL NOT make any external HTTP calls
- **AND** the controller SHALL NOT connect to any database
- **AND** the controller SHALL NOT call payment, provider, or notification services
- **AND** the controller SHALL return the response immediately

#### Scenario: Controller returns mock orderId

- **GIVEN** a client sends an HTTP POST request to `/api/checkout/esim`
- **WHEN** the request is processed
- **THEN** the controller SHALL generate an `orderId` matching pattern `"ORD-{GUID}"`

#### Scenario: Controller includes request fields in response for logging

- **GIVEN** a client sends an HTTP POST request to `/api/checkout/esim`
- **AND** the request body contains `packageCode` and `quantity`
- **WHEN** the request is processed
- **THEN** the response SHALL include these fields in the response body
- **AND** `LoggerMiddleware` SHALL capture them in the request/response payloads for logging

---

### Requirement: SampleCheckoutService shall log request with X-Flow-Id header support

The `LoggerMiddleware` SHALL use the `X-Flow-Id` header value as the `flowId` for logging when present. SampleService ensures flowId consistency by seeding `X-Flow-Id` before LoggerMiddleware reads it.

#### Scenario: SampleService seeds X-Flow-Id when no flow header exists

- **GIVEN** a client sends an HTTP POST request to `/api/checkout/esim`
- **AND** the request does NOT include `X-Flow-Id` header
- **WHEN** SampleService processes the request
- **THEN** SampleService SHALL generate a new flowId GUID
- **AND** SampleService SHALL set `Request.Headers["X-Flow-Id"]` to the generated flowId
- **AND** `LoggerMiddleware` SHALL read the seeded `X-Flow-Id` and use it for logging
- **AND** `CheckoutController` SHALL read the same `X-Flow-Id` from headers
- **AND** the response SHALL contain the same flowId that was logged

#### Scenario: SampleService preserves existing X-Flow-Id header

- **GIVEN** a client sends an HTTP POST request to `/api/checkout/esim`
- **AND** the request includes header `X-Flow-Id: customer-flow-abc`
- **WHEN** SampleService processes the request
- **THEN** SampleService SHALL keep the existing `X-Flow-Id` value unchanged
- **AND** `LoggerMiddleware` SHALL use `"customer-flow-abc"` as the flowId for logging
- **AND** `CheckoutController` SHALL return `"customer-flow-abc"` in the response

#### Scenario: Middleware publishes log with seeded flowId

- **GIVEN** a client sends an HTTP POST request to `/api/checkout/esim`
- **AND** the request does NOT include `X-Flow-Id` header
- **WHEN** the request is processed
- **THEN** `LoggerMiddleware` SHALL publish a `LogEventMessage` with the seeded `flowId`

#### Scenario: Controller returns same flowId that was seeded

- **GIVEN** a client sends an HTTP POST request to `/api/checkout/esim`
- **AND** the request does NOT include `X-Flow-Id` header
- **WHEN** the request is processed
- **THEN** `CheckoutController` SHALL read the flowId from `Request.Headers["X-Flow-Id"]`
- **AND** the response SHALL contain the same flowId that was seeded by SampleService
- **AND** the logged flowId and response flowId SHALL match

---

### Requirement: SampleCheckoutService shall seed X-Flow-Id before LoggerMiddleware

SampleService SHALL implement a local `FlowIdSeedingMiddleware` that runs before `LoggerMiddleware` to ensure flowId consistency between the response and logged events.

#### Scenario: FlowIdSeedingMiddleware is registered before LoggerMiddleware

- **GIVEN** the `Program.cs` is examined
- **WHEN** the middleware registration order is reviewed
- **THEN** `app.UseMiddleware<FlowIdSeedingMiddleware>()` SHALL be called
- **AND** `FlowIdSeedingMiddleware` SHALL be registered BEFORE `LoggerMiddleware`

#### Scenario: FlowIdSeedingMiddleware generates flowId when absent

- **GIVEN** a client sends an HTTP POST request to `/api/checkout/esim`
- **AND** the request does NOT include `X-Flow-Id` header
- **WHEN** `FlowIdSeedingMiddleware` processes the request
- **THEN** the middleware SHALL generate a new GUID for flowId
- **AND** the middleware SHALL set `Request.Headers["X-Flow-Id"]` to the generated flowId

#### Scenario: FlowIdSeedingMiddleware preserves existing flowId

- **GIVEN** a client sends an HTTP POST request to `/api/checkout/esim`
- **AND** the request includes `X-Flow-Id: my-flow-456`
- **WHEN** `FlowIdSeedingMiddleware` processes the request
- **THEN** the middleware SHALL NOT modify the existing `X-Flow-Id` header
- **AND** `Request.Headers["X-Flow-Id"]` SHALL still be `"my-flow-456"`

#### Scenario: FlowIdSeedingMiddleware is local to SampleService

- **GIVEN** the `Skysim.Logger.Client` source code is examined
- **WHEN** the project is searched for `FlowIdSeedingMiddleware`
- **THEN** `FlowIdSeedingMiddleware` SHALL NOT exist in `Skysim.Logger.Client`
- **AND** `FlowIdSeedingMiddleware` SHALL exist only in `Skysim.Logger.SampleService`

---

### Requirement: SampleCheckoutService shall expose Swagger UI for demo purposes

The `SampleService` SHALL enable Swagger/OpenAPI in the Development environment so the checkout endpoint can be tested easily.

#### Scenario: Swagger is enabled in Development environment

- **GIVEN** `SampleService` is running in Development environment
- **WHEN** a browser navigates to `/swagger`
- **THEN** the Swagger UI page SHALL be displayed
- **AND** the `POST /api/checkout/esim` endpoint SHALL be listed

#### Scenario: Swagger is not enabled in Production environment

- **GIVEN** `SampleService` is running in Production environment
- **WHEN** a browser navigates to `/swagger`
- **THEN** the Swagger UI page SHALL NOT be displayed
- **AND** the endpoint SHALL return 404 or redirect

#### Scenario: Swagger does not require authentication

- **GIVEN** `SampleService` is running in Development environment
- **WHEN** a browser navigates to `/swagger`
- **THEN** no authentication SHALL be required to view or test the Swagger UI
