## Why

The current HTTP logging middleware, Kafka log producer, and sensitive data masking logic are embedded within `Skysim.Logger.Api`. This tightly couples logging infrastructure to the API project, making it impossible to reuse these components in other microservices (e.g., Order, Payment, Notification) that need to publish structured log events to Kafka. Extracting these into a dedicated `Skysim.Logger.Client` class library enables any backend service to adopt centralized logging without duplicating code.

## What Changes

- Create a new .NET 8 class library `Skysim.Logger.Client` with `<FrameworkReference Include="Microsoft.AspNetCore.App" />` and project reference to `Skysim.Logger.Contracts`.
- Add `Skysim.Logger.Client` to `backend/Skysim.sln`.
- Move `LoggerMiddleware.cs` from `Skysim.Logger.Api/Middlewares` to `Skysim.Logger.Client/Middlewares`.
- Delete `RequestBodyBufferingMiddleware.cs` — `LoggerMiddleware` will call `EnableBuffering()` internally.
- Move `IKafkaLogProducer.cs` and `KafkaLogProducer.cs` from `Skysim.Logger.Api/Infrastructure/Kafka` to `Skysim.Logger.Client/Producers`. `KafkaLogProducer` uses primitive constructor parameters; minimal retry helper logic is inlined, no shared helper classes.
- Move and merge masking logic into `Skysim.Logger.Client/Masking`, preserving both `MaskJson(string)` and `Mask(LogEventMessage)` behaviors.
- Delete duplicate masking files:
  - `Skysim.Logger.Api/Common/SensitiveDataMasker.cs`
  - `Skysim.Logger.Common/Masking/SensitiveDataMasker.cs`
  - `Skysim.Logger.Common/Masking/SensitiveFields.cs`
- Make `LoggerMiddleware` excluded paths configurable via constructor parameter with sensible defaults.
- Update namespaces and project references throughout affected projects.
- Keep build and tests green (162 tests passing).

## Capabilities

### New Capabilities

- `logger-client`: A reusable .NET 8 class library providing HTTP request/response logging middleware, Kafka log event producer, and sensitive data masking — usable by any backend service without coupling to Logger.Api.

## Impact

**New Projects:**
- `backend/Skysim.Logger.Client/` — new class library project.

**Modified Projects:**
- `backend/Skysim.Logger.Api/` — loses `LoggerMiddleware`, `IKafkaLogProducer`, `KafkaLogProducer`, and masking files; gains reference to `Skysim.Logger.Client`.
- `backend/Skysim.Logger.Api.Tests/` — may reference `Skysim.Logger.Client` for middleware, producer, and masking unit tests.

**Deleted Files:**
- `Skysim.Logger.Api/Common/SensitiveDataMasker.cs`
- `Skysim.Logger.Api/Middlewares/RequestBodyBufferingMiddleware.cs`
- `Skysim.Logger.Api/Infrastructure/Kafka/IKafkaLogProducer.cs`
- `Skysim.Logger.Api/Infrastructure/Kafka/KafkaLogProducer.cs`
- `Skysim.Logger.Api/Infrastructure/Kafka/KafkaLogProducerOptions.cs`
- `Skysim.Logger.Common/Masking/SensitiveDataMasker.cs`
- `Skysim.Logger.Common/Masking/SensitiveFields.cs`

**Files KEPT in Skysim.Logger.Api (NOT deleted):**
- `Skysim.Logger.Api/Infrastructure/Kafka/LoggerOptions.cs` — used by Api configuration and KafkaLogConsumerService
- `Skysim.Logger.Api/Infrastructure/Kafka/KafkaConsumerOptions.cs` — used by KafkaLogConsumerService and DlqPublisher
- `Skysim.Logger.Api/Infrastructure/Kafka/KafkaLogConsumerService.cs` — out of scope
- `Skysim.Logger.Api/Infrastructure/Kafka/DlqPublisher.cs` — out of scope

**Kafka Topics:** No change (continues using `skysim.action.logs`).

**Database:** No change.

**API Contracts:** No change.

**Dependencies:**
- `Skysim.Logger.Client` → `Skysim.Logger.Contracts` (exactly one project reference)
- `Skysim.Logger.Client` uses `<FrameworkReference Include="Microsoft.AspNetCore.App" />` for ASP.NET Core types
- `Skysim.Logger.Client` must NOT reference `Skysim.Logger.Api`, `Skysim.Logger.Infrastructure`, or `Skysim.Logger.Common`
- `Skysim.Logger.Api` → `Skysim.Logger.Client` (temporarily; may be removed in future cleanup)
- `Skysim.Logger.Api.Tests` → `Skysim.Logger.Client`
