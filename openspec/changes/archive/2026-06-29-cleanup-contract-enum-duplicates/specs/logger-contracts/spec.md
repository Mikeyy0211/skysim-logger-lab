# Logger Contracts Specification (Delta)

## MODIFIED Requirements

### Requirement: Logger contracts shall provide shared constants

The `Skysim.Logger.Contracts` project SHALL contain static classes for all shared string constants used across the logger system. Each constant class SHALL reside in `Skysim.Logger.Contracts.Constants` and SHALL NOT reference any other project. The project SHALL NOT contain both enum definitions and static string constant classes for the same concept.

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

#### Scenario: No duplicate enum definitions exist
- **GIVEN** the `Skysim.Logger.Contracts/Constants` directory
- **WHEN** it is inspected for type definitions
- **THEN** it SHALL NOT contain both enum and static string constant classes for the same concept
- **AND** it SHALL NOT contain files named `ActionType.cs`, `CheckoutType.cs`, `FlowType.cs`, or `Status.cs`
- **AND** `ActionTypes`, `CheckoutTypes`, `FlowTypes`, and `StatusTypes` SHALL be the only canonical definitions for their respective concepts
