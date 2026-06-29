# Logger Contracts Specification

## ADDED Requirements

### Requirement: Logger contracts shall provide shared log event models

The `Skysim.Logger.Contracts` project SHALL contain `LogEventMessage` as the canonical event model for all logger Kafka messages. The event model SHALL include all fields required for the logging flow: eventId, flowId, flowType, serviceName, actionType, status, createdAt, checkoutType, userId, customerEmail, customerPhone, orderId, paymentId, message, requestTime, responseTime, duration, requestData, responseData, errorCode, errorMessage, exception, and correlationId. The event model SHALL use `System.Text.Json` with existing serialization attributes for JSON serialization and deserialization. The event model SHALL NOT depend on any project outside of `Skysim.Logger.Contracts`.

#### Scenario: LogEventMessage contains all required Kafka message fields
- **GIVEN** a `LogEventMessage` instance
- **WHEN** it is serialized to JSON and deserialized back
- **THEN** all fields SHALL be preserved (eventId, flowId, flowType, serviceName, actionType, status, createdAt, checkoutType, userId, customerEmail, customerPhone, orderId, paymentId, message, requestTime, responseTime, duration, requestData, responseData, errorCode, errorMessage, exception, correlationId)

### Requirement: Logger contracts shall provide shared constants

The `Skysim.Logger.Contracts` project SHALL contain static classes for all shared string constants used across the logger system. Each constant class SHALL reside in `Skysim.Logger.Contracts.Constants` and SHALL NOT reference any other project.

#### Scenario: ActionTypes constants are accessible
- **GIVEN** `Skysim.Logger.Contracts` is referenced by a project
- **WHEN** `ActionTypes.OrderCreated` is accessed
- **THEN** it SHALL return the string `"ORDER_CREATED"`
- **AND** `ActionTypes.PaymentSuccess` SHALL return `"PAYMENT_SUCCESS"`
- **AND** `ActionTypes.EsimActivated` SHALL return `"ESIM_ACTIVATED"`
- **AND** `ActionTypes.OrderFailed` SHALL return `"ORDER_FAILED"`
- **AND** `ActionTypes.PaymentFailed` SHALL return `"PAYMENT_FAILED"`
- **AND** `ActionTypes.ProviderFailed` SHALL return `"PROVIDER_FAILED"`
- **AND** `ActionTypes.EsimActivationFailed` SHALL return `"ESIM_ACTIVATION_FAILED"`
- **AND** `ActionTypes.EmailFailed` SHALL return `"EMAIL_FAILED"`
- **AND** `ActionTypes.HttpRequest` SHALL return `"HTTP_REQUEST"`

#### Scenario: StatusTypes constants are accessible
- **GIVEN** `Skysim.Logger.Contracts` is referenced by a project
- **WHEN** `StatusTypes.Success` is accessed
- **THEN** it SHALL return the string `"SUCCESS"`
- **AND** `StatusTypes.Failed` SHALL return `"FAILED"`
- **AND** `StatusTypes.InProgress` SHALL return `"IN_PROGRESS"`

#### Scenario: KafkaTopics constants are accessible
- **GIVEN** `Skysim.Logger.Contracts` is referenced by a project
- **WHEN** `KafkaTopics.ActionLogs` is accessed
- **THEN** it SHALL return the string `"skysim.action.logs"`
- **AND** `KafkaTopics.ActionLogsDlq` SHALL return `"skysim.action.logs.dlq"`

#### Scenario: CheckoutTypes constants are accessible
- **GIVEN** `Skysim.Logger.Contracts` is referenced by a project
- **WHEN** `CheckoutTypes.Guest` is accessed
- **THEN** it SHALL return the string `"GUEST"`
- **AND** `CheckoutTypes.Authenticated` SHALL return `"AUTHENTICATED"`

#### Scenario: FlowTypes constants are accessible
- **GIVEN** `Skysim.Logger.Contracts` is referenced by a project
- **WHEN** `FlowTypes.CheckoutEsim` is accessed
- **THEN** it SHALL return the string `"CHECKOUT_ESIM"`
- **AND** `FlowTypes.HttpAction` SHALL return `"HTTP_ACTION"`

#### Scenario: SensitiveFieldNames constants are accessible
- **GIVEN** `Skysim.Logger.Contracts` is referenced by a project
- **WHEN** `SensitiveFieldNames.Password` is accessed
- **THEN** it SHALL return the string `"password"`
- **AND** `SensitiveFieldNames.AccessToken` SHALL return `"access_token"`
- **AND** `SensitiveFieldNames.RefreshToken` SHALL return `"refresh_token"`
- **AND** `SensitiveFieldNames.Authorization` SHALL return `"authorization"`
- **AND** `SensitiveFieldNames.Otp` SHALL return `"otp"`
- **AND** `SensitiveFieldNames.CardNumber` SHALL return `"cardNumber"`
- **AND** `SensitiveFieldNames.Cvv` SHALL return `"cvv"`
- **AND** `SensitiveFieldNames.PaymentSecret` SHALL return `"paymentSecret"`
- **AND** `SensitiveFieldNames.Secret` SHALL return `"secret"`
- **AND** `SensitiveFieldNames.Token` SHALL return `"token"`

#### Scenario: HeaderNames constants are accessible
- **GIVEN** `Skysim.Logger.Contracts` is referenced by a project
- **WHEN** `HeaderNames.CorrelationId` is accessed
- **THEN** it SHALL return the string `"X-Correlation-Id"`
- **AND** `HeaderNames.RequestId` SHALL return `"X-Request-Id"`

#### Scenario: LogTypes constants are accessible
- **GIVEN** `Skysim.Logger.Contracts` is referenced by a project
- **WHEN** `LogTypes.Technical` is accessed
- **THEN** it SHALL return the string `"TECHNICAL"`
- **AND** `LogTypes.Business` SHALL return `"BUSINESS"`

---

### Requirement: Logger contracts shall provide shared response DTOs when needed

The `Skysim.Logger.Contracts` project SHALL contain `PagedResponse<T>` as a generic paginated response wrapper. It SHALL contain `ApiErrorResponse`, `ApiErrorDetail`, and `ApiFieldError` for standardized error responses. These DTOs SHALL NOT depend on any project outside of `Skysim.Logger.Contracts`.

#### Scenario: PagedResponse contains all required pagination fields
- **GIVEN** a `PagedResponse<LogFlowSummaryDto>` instance
- **WHEN** it is constructed with items, page, pageSize, totalItems, and totalPages
- **THEN** all fields SHALL be accessible and match the constructor arguments

#### Scenario: ApiErrorResponse contains error detail
- **GIVEN** an `ApiErrorResponse` instance wrapping an `ApiErrorDetail`
- **WHEN** the response is serialized to JSON
- **THEN** it SHALL produce a valid JSON object with an `error` property containing `code`, `message`, and optionally `details`

#### Scenario: ApiFieldError provides field-level error information
- **GIVEN** an `ApiFieldError` instance
- **WHEN** it is constructed with field name and message
- **THEN** both properties SHALL be accessible and match the constructor arguments

---

### Requirement: Logger contracts shall remain dependency-free

The `Skysim.Logger.Contracts` project SHALL have zero NuGet package dependencies. The project SHALL NOT reference `Skysim.Logger.Api`, `Skysim.Logger.Infrastructure`, `Skysim.Logger.Common`, or any future Client or SampleService project.

#### Scenario: Contracts project builds without external dependencies
- **GIVEN** the `Skysim.Logger.Contracts.csproj` project file
- **WHEN** `dotnet build` is executed on the Contracts project alone
- **THEN** it SHALL succeed with zero package restore
- **AND** it SHALL NOT contain any `<PackageReference>` elements in the project file

#### Scenario: Contracts has no project references
- **GIVEN** the `Skysim.Logger.Contracts.csproj` project file
- **WHEN** the file is inspected
- **THEN** it SHALL NOT contain any `<ProjectReference>` elements
- **AND** it SHALL NOT contain `<FrameworkReference>` elements beyond those provided by .NET 8 SDK

#### Scenario: Api can reference Contracts without circular dependency
- **GIVEN** `Skysim.Logger.Api` references `Skysim.Logger.Contracts`
- **WHEN** `dotnet build` is executed on the Api project
- **THEN** build SHALL succeed
- **AND** there SHALL be no circular dependency error
