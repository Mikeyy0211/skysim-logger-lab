## 1. Project Setup

- [x] 1.1 Create `backend/Skysim.Logger.Client/Skysim.Logger.Client.csproj` as a .NET 8 class library targeting `net8.0`
- [x] 1.2 Add `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to the Client project for ASP.NET Core types used by LoggerMiddleware
- [x] 1.3 Add exactly one `<ProjectReference Include="..\Skysim.Logger.Contracts\Skysim.Logger.Contracts.csproj" />` to the Client project
- [x] 1.4 Add `Confluent.Kafka` NuGet package reference (version `2.6.1`) to the Client project
- [x] 1.5 Create folder structure: `Middlewares/`, `Producers/`, `Masking/`
- [x] 1.6 Add `backend/Skysim.Logger.Client/Skysim.Logger.Client.csproj` to `backend/Skysim.sln` solution file
- [x] 1.7 Add `Skysim.Logger.Client` project reference to `Skysim.Logger.Api.csproj`

## 2. SensitiveDataMasker Extraction and Merge

- [x] 2.1 Create `Skysim.Logger.Client/Masking/ISensitiveDataMasker.cs` with `string MaskJson(string json)` and `LogEventMessage Mask(LogEventMessage message)` methods
- [x] 2.2 Create `Skysim.Logger.Client/Masking/SensitiveDataMasker.cs` merging behavior from both existing maskers
- [x] 2.3 Build the internal sensitive fields `HashSet<string>` using string literals that match the values of `Skysim.Logger.Contracts.Constants.SensitiveFieldNames` constants: `"password"`, `"access_token"`, `"refresh_token"`, `"authorization"`, `"otp"`, `"cardNumber"`, `"cvv"`, `"paymentSecret"`, `"secret"`, `"token"` — do NOT use `nameof()` and do NOT duplicate literals that are missing from `SensitiveFieldNames`
- [x] 2.4 Implement `MaskJson(string)` — traverse JSON recursively, mask sensitive field values with `"***"`, preserve non-sensitive values
- [x] 2.5 Implement `Mask(LogEventMessage)` — call `MaskJson` on serialized JSON, return deserialized masked message
- [x] 2.6 Verify `dotnet build` passes for the new project

## 3. KafkaLogProducer Extraction

- [x] 3.1 Create `Skysim.Logger.Client/Producers/IKafkaLogProducer.cs` with `Task PublishAsync(LogEventMessage message, CancellationToken cancellationToken = default)`
- [x] 3.2 Create `Skysim.Logger.Client/Producers/KafkaLogProducer.cs` using primitive constructor parameters: `string bootstrapServers`, `string acks`, `int retryMaxAttempts`, `int retryBaseDelayMs`, `string serviceName`
- [x] 3.3 Copy the minimal private helper logic needed for retry (exponential backoff delay calculation and `Acks` parsing) directly into `KafkaLogProducer` — do NOT create shared helper classes and do NOT reference `Skysim.Logger.Common` or `KafkaCommon.cs`
- [x] 3.4 Ensure `PublishAsync` sets `message.ServiceName` from constructor parameter
- [x] 3.5 Ensure topic remains `skysim.action.logs` and message key uses `flowId` (fallback to `eventId` if null)
- [x] 3.6 Implement `IDisposable` pattern for `IProducer` cleanup
- [x] 3.7 Add internal constructor accepting `IProducer<string, byte[]>` for unit testability
- [x] 3.8 Do NOT create Options classes in `Skysim.Logger.Client`

## 4. LoggerMiddleware Extraction

- [x] 4.1 Create `Skysim.Logger.Client/Middlewares/LoggerMiddleware.cs` with constructor accepting: `RequestDelegate next`, `IKafkaLogProducer producer`, `ISensitiveDataMasker masker`, `ILogger<LoggerMiddleware> logger`, `IReadOnlyList<string>? excludedPathPrefixes`
- [x] 4.2 Use `excludedPathPrefixes` parameter with default value of `["/swagger", "/api/log-flows", "/api/log-actions", "/favicon.ico", "/health"]`
- [x] 4.3 Add `context.Request.EnableBuffering()` call at start of `InvokeAsync` — this replaces `RequestBodyBufferingMiddleware` so downstream handlers can still read the body
- [x] 4.4 Preserve all existing `LoggerMiddleware` behavior: flowId extraction, correlationId generation, userId extraction from JWT, request/response body capture, response buffering, masking before publish
- [x] 4.5 Ensure `ResponseBodyBufferingStream` inner class moves to `Skysim.Logger.Client` with the middleware
- [x] 4.6 Update all namespace references to use `Skysim.Logger.Contracts`, `Skysim.Logger.Client.Masking`, `Skysim.Logger.Client.Producers`
- [x] 4.7 The middleware must remain a generic HTTP logging component — do NOT hard-code checkout/eSIM business actions

## 5. Update Skysim.Logger.Api

- [x] 5.1 Update `Skysim.Logger.Api/Program.cs` to register `IKafkaLogProducer` and `ISensitiveDataMasker` from `Skysim.Logger.Client` in the DI container
- [x] 5.2 Update `Skysim.Logger.Api/Program.cs` to register `LoggerMiddleware` from `Skysim.Logger.Client` in the pipeline
- [x] 5.3 Wire up primitive parameters for `KafkaLogProducer` using values from `KafkaConsumerOptions` and `LoggerOptions` (Api's own configuration objects — these remain in `Skysim.Logger.Api`)
- [x] 5.4 Delete `Skysim.Logger.Api/Middlewares/LoggerMiddleware.cs`
- [x] 5.5 Delete `Skysim.Logger.Api/Middlewares/RequestBodyBufferingMiddleware.cs`
- [x] 5.6 Delete `Skysim.Logger.Api/Common/SensitiveDataMasker.cs`
- [x] 5.7 Delete `Skysim.Logger.Api/Infrastructure/Kafka/IKafkaLogProducer.cs` (moved to Client)
- [x] 5.8 Delete `Skysim.Logger.Api/Infrastructure/Kafka/KafkaLogProducer.cs` (moved to Client)
- [x] 5.9 Delete `Skysim.Logger.Api/Infrastructure/Kafka/KafkaLogProducerOptions.cs` (producer-specific Options; no longer needed when using Client's primitive-parameter constructor)
- [x] 5.10 Do NOT delete `Skysim.Logger.Api/Infrastructure/Kafka/LoggerOptions.cs` (used by Api's own config and KafkaLogConsumerService)
- [x] 5.11 Do NOT delete `Skysim.Logger.Api/Infrastructure/Kafka/KafkaConsumerOptions.cs` (used by KafkaLogConsumerService, DlqPublisher — out of scope for this change)

## 6. Update Skysim.Logger.Common

- [x] 6.1 Delete `Skysim.Logger.Common/Masking/SensitiveDataMasker.cs`
- [x] 6.2 Delete `Skysim.Logger.Common/Masking/SensitiveFields.cs`

## 7. Update Skysim.Logger.Api.Tests

- [x] 7.1 Add `Skysim.Logger.Client` project reference to `Skysim.Logger.Api.Tests.csproj`
- [x] 7.2 Update `LoggerMiddlewareTests.cs` namespace imports to reference `Skysim.Logger.Client.Middlewares`
- [x] 7.3 Update `KafkaLogProducerTests.cs` namespace imports to reference `Skysim.Logger.Client.Producers`
- [x] 7.4 Update `SensitiveDataMaskerTests.cs` namespace imports to reference `Skysim.Logger.Client.Masking`
- [x] 7.5 Verify `dotnet build` passes for all projects
- [x] 7.6 Run `dotnet test` — all 162 tests must pass

## 8. Verification

- [x] 8.1 Run `dotnet build` on the entire solution — must pass with no errors
- [x] 8.2 Run `dotnet test` — all 162 tests must pass
- [x] 8.3 Verify `Skysim.Logger.Client.csproj` has exactly one `<ProjectReference>` to `Skysim.Logger.Contracts`
- [x] 8.4 Verify `Skysim.Logger.Client.csproj` has zero references to `Skysim.Logger.Api`, `Skysim.Logger.Infrastructure`, `Skysim.Logger.Common`
- [x] 8.5 Verify `Skysim.Logger.Client.csproj` has `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
- [x] 8.6 Verify deleted files are actually removed from disk
- [x] 8.7 Run `openspec validate extract-logger-client --strict` to confirm all artifacts are valid
