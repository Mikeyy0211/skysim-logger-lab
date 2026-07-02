# logger-registration-extensions Specification

## ADDED Requirements

### Requirement: Logger.Client shall provide AddSkysimLogger extension method

The `Skysim.Logger.Client` SHALL provide an `AddSkysimLogger()` extension method on `IServiceCollection` that registers all required services for HTTP logging:
- `ISensitiveDataMasker` as singleton
- `IKafkaLogProducer` as singleton
- `LoggerMiddlewareOptions` from configuration

#### Scenario: Consuming service uses AddSkysimLogger to register services

- **GIVEN** a backend service calls `builder.Services.AddSkysimLogger(builder.Configuration)`
- **WHEN** the service starts
- **THEN** `IKafkaLogProducer` SHALL be registered in the DI container
- **AND** `ISensitiveDataMasker` SHALL be registered in the DI container

#### Scenario: AddSkysimLogger reads serviceName from Logger:ServiceName config

- **GIVEN** `appsettings.json` contains `"Logger": { "ServiceName": "OrderService" }`
- **AND** a backend service calls `AddSkysimLogger(builder.Configuration)`
- **WHEN** the service publishes a log event
- **THEN** the `serviceName` field SHALL be `"OrderService"`

### Requirement: Logger.Client shall provide UseSkysimLogger extension method

The `Skysim.Logger.Client` SHALL provide a `UseSkysimLogger()` extension method on `IApplicationBuilder` that registers `LoggerMiddleware` in the pipeline.

#### Scenario: Consuming service uses UseSkysimLogger to add middleware

- **GIVEN** a backend service calls `app.UseSkysimLogger()`
- **WHEN** the service starts
- **THEN** `LoggerMiddleware` SHALL be added to the middleware pipeline
- **AND** HTTP requests SHALL be intercepted and logged

### Requirement: Extension methods shall work without FlowIdSeedingMiddleware dependency

The `UseSkysimLogger()` extension method SHALL NOT require a separate `FlowIdSeedingMiddleware` to be registered. The `LoggerMiddleware` SHALL handle flow ID generation if no correlation header is present.

#### Scenario: Logger works without FlowIdSeedingMiddleware registered

- **GIVEN** a backend service registers only `UseSkysimLogger()` (no `UseFlowIdSeeding()`)
- **AND** a client sends an HTTP request without `X-Flow-Id` header
- **WHEN** the request completes
- **THEN** `LoggerMiddleware` SHALL generate a new GUID for `flowId`
- **AND** the response SHALL include `X-Correlation-ID` header with the generated GUID
