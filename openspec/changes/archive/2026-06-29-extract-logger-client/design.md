## Context

The current HTTP logging middleware, Kafka log producer, and sensitive data masking logic are embedded within `Skysim.Logger.Api`. Specifically:

- `Skysim.Logger.Api/Middlewares/LoggerMiddleware.cs` — generic HTTP logging middleware that reads request/response bodies and publishes `LogEventMessage` to Kafka.
- `Skysim.Logger.Api/Middlewares/RequestBodyBufferingMiddleware.cs` — a thin middleware that calls `EnableBuffering()` on the request body before passing to the pipeline.
- `Skysim.Logger.Api/Infrastructure/Kafka/KafkaLogProducer.cs` — implements `IKafkaLogProducer`, publishes `LogEventMessage` to the `skysim.action.logs` topic with retry logic.
- `Skysim.Logger.Api/Common/SensitiveDataMasker.cs` — implements `ISensitiveDataMasker`, masks sensitive fields in JSON strings and `LogEventMessage`.
- `Skysim.Logger.Common/Masking/SensitiveDataMasker.cs` — duplicate of the above; only has `MaskJson(string)`.
- `Skysim.Logger.Common/Masking/SensitiveFields.cs` — singleton holding the deny-list of sensitive field names.

`Skysim.Logger.Contracts` is the established single source of truth for shared constants, DTOs, and `LogEventMessage`. `SensitiveFieldNames` in `Skysim.Logger.Contracts.Constants` holds the same field names as `SensitiveFields` in `Skysim.Logger.Common.Masking`.

The goal is to extract the reusable client-side logging components into a dedicated class library so any backend microservice (Order, Payment, Notification, etc.) can adopt centralized logging without coupling to `Skysim.Logger.Api`.

## Goals / Non-Goals

**Goals:**

- Extract `LoggerMiddleware`, `KafkaLogProducer`, and masking logic into a new `Skysim.Logger.Client` class library.
- Ensure `Skysim.Logger.Client` references only `Skysim.Logger.Contracts` (no dependency on `Skysim.Logger.Api`, `Skysim.Logger.Infrastructure`, or `Skysim.Logger.Common`).
- Make `LoggerMiddleware` self-contained: call `EnableBuffering()` internally and remove the need for `RequestBodyBufferingMiddleware`.
- Make excluded paths configurable via constructor parameter with sensible defaults.
- Consolidate duplicate masking implementations into a single `SensitiveDataMasker` in `Skysim.Logger.Client` using the sensitive field name values from `SensitiveFieldNames` in `Skysim.Logger.Contracts`.
- Delete duplicate files to prevent future divergence.
- Keep build and all 162 existing tests green throughout.

**Non-Goals:**

- Moving `KafkaLogConsumerService`, `DlqPublisher`, or `KafkaConsumerOptions` — these are server-side and remain in `Skysim.Logger.Api`.
- Reorganizing `Skysim.Logger.Api` folder structure (beyond the files being moved/deleted).
- Creating Options classes, extension methods, `BusinessActionFilter`, `BusinessLogPublisher`, `CheckoutBusinessEvent`, or `Skysim.Logger.SampleService`.
- Creating helper classes in `Skysim.Logger.Client` beyond the three core components.
- Renaming or deleting `Skysim.Logger.Common`.
- Any frontend changes.
- Any database schema changes.
- Any changes to Kafka consumer persistence logic.

## Decisions

### 1. New project: `Skysim.Logger.Client` as a .NET 8 class library with FrameworkReference

**Decision:** Create a new .NET 8 class library project at `backend/Skysim.Logger.Client/` with `<FrameworkReference Include="Microsoft.AspNetCore.App" />` for ASP.NET Core types, and exactly one `<ProjectReference>` to `Skysim.Logger.Contracts`.

**Rationale:** A class library with a framework reference is the simplest way to access ASP.NET Core types (HttpContext, RequestDelegate, ILogger, etc.) without depending on the full `Skysim.Logger.Api` project. The framework reference is implicit in .NET 8 ASP.NET Core apps but must be explicit in a class library targeting `net8.0`.

**Alternatives considered:**
- *Source generator*: Overkill for this extraction; adds complexity and build overhead.
- *Shared project (.shproj)*: Creates fragile shared source with no assembly-level encapsulation.
- *Embedding in `Skysim.Logger.Common`*: Would still couple client-side logging (HTTP middleware + Kafka producer) to a project that is not the single source of truth for contracts.

### 2. `LoggerMiddleware` calls `EnableBuffering()` internally

**Decision:** The middleware calls `context.Request.EnableBuffering()` at the start of `InvokeAsync` and removes the `RequestBodyBufferingMiddleware` entirely.

**Rationale:** The current `LoggerMiddleware` already reads the request body and resets its position to 0, but it relies on `RequestBodyBufferingMiddleware` to have already called `EnableBuffering()`. By calling `EnableBuffering()` inside `LoggerMiddleware` itself, downstream handlers still get a readable stream, and the pipeline is simplified by removing a separate middleware class.

**Behavior preserved:**
- The `ReadRequestBodyAsync` method already checks `CanSeek` before reading; adding `EnableBuffering()` ensures `CanSeek` is always true.
- Downstream handlers can still read the body because position is reset to 0 after `LoggerMiddleware` reads it.

### 3. `KafkaLogProducer` uses primitive constructor parameters, no shared helpers

**Decision:** Replace the current constructor that accepts `IKafkaLogProducerOptions` with individual primitive parameters: `string bootstrapServers`, `string acks`, `int retryMaxAttempts`, `int retryBaseDelayMs`, `string serviceName`. Copy only the minimal private helper logic (exponential backoff delay calculation, `Acks` parsing) directly into `KafkaLogProducer`.

**Rationale:** `Skysim.Logger.Client` must not reference `Skysim.Logger.Common` (which contains `KafkaCommon.cs`). Copying the minimal helper logic inline keeps `KafkaLogProducer` self-contained and testable without introducing a shared helper class.

**No shared helper classes:** `KafkaCommon.cs` in `Skysim.Logger.Common` is not referenced or copied as a whole. Only the specific logic needed by `KafkaLogProducer` (exponential backoff delay and Acks parsing) is duplicated as private static methods within `KafkaLogProducer`.

**Alternatives considered:**
- *Keep Options pattern*: Would require `Skysim.Logger.Client` to define its own Options classes, adding unnecessary surface area and coupling to configuration patterns.
- *Inject a config object interface*: Creates another abstraction that still needs to be implemented per service.
- *Reference KafkaCommon.cs*: Would create a dependency on `Skysim.Logger.Common`, which is not allowed.

### 4. Merge masking into `Skysim.Logger.Client/Masking` using `SensitiveFieldNames` values

**Decision:** Create a single `SensitiveDataMasker` in `Skysim.Logger.Client/Masking` that implements both `MaskJson(string)` and `Mask(LogEventMessage)`. The internal sensitive fields set is built from string literal values that match the constants in `Skysim.Logger.Contracts.Constants.SensitiveFieldNames`: `"password"`, `"access_token"`, `"refresh_token"`, `"authorization"`, `"otp"`, `"cardNumber"`, `"cvv"`, `"paymentSecret"`, `"secret"`, `"token"`.

**Rationale:** The two existing implementations (`Skysim.Logger.Api/Common/SensitiveDataMasker` and `Skysim.Logger.Common/Masking/SensitiveDataMasker`) both do the same thing — traverse JSON and replace sensitive field values with `"***"`. Merging them into one and using the canonical field names prevents future drift. Using string literals (not `nameof()`) keeps the set explicit and auditable against the Contracts constants.

**File deletions after merge:**
- `Skysim.Logger.Api/Common/SensitiveDataMasker.cs`
- `Skysim.Logger.Common/Masking/SensitiveDataMasker.cs`
- `Skysim.Logger.Common/Masking/SensitiveFields.cs`

### 5. Excluded paths configurable via constructor parameter

**Decision:** Add `IReadOnlyList<string>? excludedPathPrefixes` parameter to `LoggerMiddleware` constructor with a default value of `["/swagger", "/api/log-flows", "/api/log-actions", "/favicon.ico", "/health"]`.

**Rationale:** Different services may need different exclusion lists. Making it a constructor parameter (rather than a static) enables per-service configuration while keeping the default sensible for the Logger API use case.

### 6. `LoggerMiddleware` remains generic HTTP logging middleware

**Decision:** The middleware does not hard-code checkout/eSIM business actions. It logs HTTP request/response cycles as `ActionTypes.HttpRequest` with `FlowTypes.HttpAction`.

**Rationale:** `LoggerMiddleware` is a technical/infrastructure component. Business action logging (e.g., `ORDER_CREATED`, `PAYMENT_SUCCESS`) is the responsibility of a future `BusinessLogPublisher` (out of scope for this change). Keeping `LoggerMiddleware` generic makes it reusable across all services.

### 7. User identification and correlation

**Decision:**
- `flowId` is extracted from `X-Flow-Id`, `X-Correlation-Id`, `X-Request-ID`, or `context.TraceIdentifier` headers; falls back to a new GUID written to `X-Correlation-ID` response header.
- `correlationId` in `LogEventMessage` mirrors `flowId`.
- `userId` is extracted from JWT claims (`sub`, `NameIdentifier`, `userId`) when the request is authenticated; otherwise `null`.
- All behavior is preserved as-is from the current implementation.

**Rationale:** This behavior was already correct and proven. No changes needed.

## Project Structure After Change

```
backend/
├── Skysim.Logger.Client/                        # NEW
│   ├── Skysim.Logger.Client.csproj              # FrameworkReference(Microsoft.AspNetCore.App) + reference to Contracts
│   ├── Middlewares/
│   │   └── LoggerMiddleware.cs
│   ├── Producers/
│   │   ├── IKafkaLogProducer.cs
│   │   └── KafkaLogProducer.cs
│   └── Masking/
│       ├── ISensitiveDataMasker.cs
│       └── SensitiveDataMasker.cs
│
├── Skysim.Logger.Api/
│   ├── Middlewares/
│   │   └── RequestBodyBufferingMiddleware.cs  # DELETED
│   ├── Common/
│   │   └── SensitiveDataMasker.cs              # DELETED
│   ├── Infrastructure/
│   │   └── Kafka/
│   │       ├── IKafkaLogProducer.cs            # DELETED (moved to Client)
│   │       ├── KafkaLogProducer.cs              # DELETED (moved to Client)
│   │       ├── KafkaLogProducerOptions.cs      # DELETED (not needed)
│   │       ├── KafkaConsumerOptions.cs          # KEPT (used by consumer)
│   │       ├── LoggerOptions.cs                 # KEPT (used by Api config)
│   │       └── ...
│   └── ...
│
├── Skysim.Logger.Common/
│   └── Masking/
│       ├── SensitiveDataMasker.cs              # DELETED
│       └── SensitiveFields.cs                  # DELETED
│
├── Skysim.Logger.Contracts/
│   └── Constants/
│       └── SensitiveFieldNames.cs              # ALREADY EXISTS (source of truth)
│
└── Skysim.sln
```

## Dependency Rules

| Project | Can Reference |
|---|---|
| `Skysim.Logger.Client` | `Skysim.Logger.Contracts` only |
| `Skysim.Logger.Api` | `Skysim.Logger.Client`, `Skysim.Logger.Contracts`, `Skysim.Logger.Infrastructure`, `Skysim.Logger.Common` |
| `Skysim.Logger.Api.Tests` | `Skysim.Logger.Client`, all above |

`Skysim.Logger.Client` must never reference `Skysim.Logger.Api`, `Skysim.Logger.Infrastructure`, or `Skysim.Logger.Common`.

## Risks / Trade-offs

| Risk | Mitigation |
|---|---|
| Breaking existing tests that reference moved types | Update test project references and namespace imports; tests remain functionally identical. |
| Losing `IOptions<KafkaConsumerOptions>` integration in `KafkaLogProducer` | `Skysim.Logger.Api` wraps primitive parameters with its own Options in `Program.cs` when instantiating `KafkaLogProducer`. |
| Duplicating helper logic from `KafkaCommon.cs` | Only minimal inline helpers (delay calculation, Acks parsing) are duplicated; no shared helper classes created. |
| `EnableBuffering()` called twice (once in middleware, once elsewhere) | `EnableBuffering()` is idempotent; calling it multiple times is safe. |
| `Skysim.Logger.Client` grows to include too many concerns | This change is scoped tightly. Future features (extension methods, `BusinessActionFilter`, etc.) are explicitly out of scope. |

## Migration Plan

1. Create `Skysim.Logger.Client` project with `<FrameworkReference Include="Microsoft.AspNetCore.App" />` and reference to `Skysim.Logger.Contracts`.
2. Add the new project to `Skysim.sln`.
3. Create `Middlewares/`, `Producers/`, `Masking/` folders and create files with updated namespaces.
4. Update `LoggerMiddleware` to call `EnableBuffering()` internally and accept excluded path configuration.
5. Update `KafkaLogProducer` to use primitive constructor parameters with inline helper methods (no external helper class references).
6. Merge masking implementations using `SensitiveFieldNames` values; delete duplicates.
7. Add `Skysim.Logger.Api` → `Skysim.Logger.Client` project reference.
8. Update `Skysim.Logger.Api/Program.cs` to register `LoggerMiddleware`, `IKafkaLogProducer`, and `ISensitiveDataMasker` from the new library, wiring primitive parameters from Api's own config objects.
9. Delete old files from `Skysim.Logger.Api`, `Skysim.Logger.Common`.
10. Update `Skysim.Logger.Api.Tests` project references if needed.
11. Run `dotnet build` and `dotnet test` — all 162 tests must pass.

**Rollback:** If issues arise, revert to the pre-change commit. All changes are contained within the `Skysim.Logger.Client` extraction and file deletions.

## Open Questions

1. Should `Skysim.Logger.Api` remove its reference to `Skysim.Logger.Client` after the change? **Deferred to a future cleanup change.**
2. Should `Skysim.Logger.Client` ship as a NuGet package eventually? **Deferred — currently using project references.**
