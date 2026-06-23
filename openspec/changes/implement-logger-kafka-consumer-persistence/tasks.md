# Tasks: implement-logger-kafka-consumer-persistence

This change implements Phase 2 of the Skysim Logger module: Kafka Consumer and PostgreSQL persistence. Follow the task order — later tasks depend on earlier ones. Each task has a concrete output and a verification step.

## 1. Project Setup & NuGet Packages

- [x] 1.1 Add NuGet packages to `backend/Skysim.Logger.Api/Skysim.Logger.Api.csproj`: `Confluent.Kafka` (latest), `Npgsql.EntityFrameworkCore.PostgreSQL` (latest .NET 8 compatible), `Microsoft.EntityFrameworkCore.Design`, `Polly`, `FluentValidation.AspNetCore`. Run `dotnet restore` and confirm no conflicts.
- [x] 1.2 Create the folder structure under `backend/Skysim.Logger.Api/`: `Contracts/DTOs/`, `Domain/Entities/`, `Domain/Enums/`, `Infrastructure/Persistence/Repositories/`, `Infrastructure/Kafka/`, `Common/`. Confirm folders are recognized by the IDE.
- [x] 1.3 Add Kafka and PostgreSQL connection config to `backend/Skysim.Logger.Api/appsettings.json` under `Kafka`, `ConnectionStrings`, and `Logger` sections per the design's Kafka Configuration section. **Do not hard-code production passwords.**

## 2. Enums & DTOs (Kafka Contract)

- [x] 2.1 Create `Domain/Enums/FlowType.cs` with string-value enum members: `CheckoutEsim`. Apply `[JsonStringEnumConverter]`.
- [x] 2.2 Create `Domain/Enums/CheckoutType.cs` with string-value enum members: `Guest`, `Authenticated`. Apply `[JsonStringEnumConverter]`.
- [x] 2.3 Create `Domain/Enums/Status.cs` with string-value enum members: `Success`, `Failed`, `InProgress`. Apply `[JsonStringEnumConverter]`.
- [x] 2.4 Create `Domain/Enums/DetailType.cs` with string-value enum members: `Request`, `Response`, `Error`, `Metadata`. Apply `[JsonStringEnumConverter]`.
- [x] 2.5 Create `Contracts/DTOs/LogEventMessage.cs` with all required fields (`eventId`, `flowId`, `flowType`, `serviceName`, `actionType`, `status`, `createdAt`) and all optional fields (`checkoutType`, `userId`, `customerEmail`, `customerPhone`, `orderId`, `paymentId`, `message`, `requestTime`, `responseTime`, `duration`, `requestData`, `responseData`, `errorCode`, `errorMessage`, `exception`, `correlationId`). Use `JsonElement?` for JSON payload fields. Apply `[JsonStringEnumConverter]` on enum properties. Add a `DetailType?` helper if needed.
- [x] 2.6 Create `Contracts/DTOs/LogEventMessageValidator.cs` using FluentValidation: required fields non-null/non-empty, `eventId` is a valid GUID, `flowId` is a non-empty string with max length 100, `flowType` and `checkoutType` match enum values, `status` matches enum values, `actionType` is in the canonical list, timestamps are valid ISO-8601. Unit test this validator with at least 5 cases (valid, missing required, invalid enum, invalid GUID, invalid timestamp).

## 3. EF Core Entities

- [x] 3.1 Create `Domain/Entities/LogFlow.cs`: all columns from the design's `log_flows` table, with `[Column("flow_id")]` etc. for snake_case mapping. Add `[Key("id")]` with `gen_random_uuid()` default. Add navigation property `ICollection<LogAction> Actions`. `FlowId` should be mapped as a string / varchar(100), not Guid.
- [x] 3.2 Create `Domain/Entities/LogAction.cs`: all columns from the design's `log_actions` table. `event_id` column has UNIQUE constraint. Add `[ForeignKey("flow_id")]` pointing to `LogFlow`. Add navigation property `LogActionDetail? Detail`. `FlowId` should be string / varchar(100) and FK to log_flows.flow_id.
- [x] 3.3 Create `Domain/Entities/LogActionDetail.cs`: all columns from the design's `log_action_details` table. `action_id` column has UNIQUE + FK constraint pointing to `LogAction`. Use `[Column(TypeName = "jsonb")]` for payload columns.

## 4. DbContext & Migrations

- [x] 4.1 Create `Infrastructure/Persistence/LoggerDbContext.cs`: `DbSet<LogFlow> LogFlows`, `DbSet<LogAction> LogActions`, `DbSet<LogActionDetail> LogActionDetails`. Configure relationships, column names (snake_case), and JSONB type for payload columns in `OnModelCreating`. Indexes are defined as fluent API here. Also created `LoggerDbContextFactory.cs` for design-time migration support.
- [x] 4.2 Run `dotnet ef migrations add InitialCreate --output-dir Infrastructure/Persistence/Migrations --project backend/Skysim.Logger.Api --startup-project backend/Skysim.Logger.Api`. Verified migration file contains `CreateTable` for all three tables with correct column types, indexes, and constraints.
- [x] 4.3 (Verification only — do not run against real DB in this task) Inspect the generated migration SQL: confirmed `event_id` has UNIQUE constraint (`idx_log_actions_event_id` unique: true), `flow_id` on `log_actions` has FK (`FK_log_actions_log_flows_flow_id`), `action_id` on `log_action_details` has UNIQUE FK (`idx_log_action_details_action_id` unique: true), jsonb columns, and all required indexes present.

## 5. Repositories (Persistence Layer)

- [ ] 5.1 Create `Infrastructure/Persistence/Repositories/ILogFlowRepository.cs` with `UpsertAsync(LogEventMessage, CancellationToken)`. The method signature returns the `LogFlow` entity (new or existing).
- [ ] 5.2 Create `Infrastructure/Persistence/Repositories/LogFlowRepository.cs`: implement `UpsertAsync` using raw SQL `INSERT ... ON CONFLICT (flow_id) DO UPDATE SET total_steps = total_steps + 1, success_steps = success_steps + 1 (or failed_steps), last_action_type = EXCLUDED.last_action_type, last_message = EXCLUDED.last_message, updated_at = now(), completed_at = <conditional>` based on terminal status. Parameterized inputs. Commit is caller-responsibility (transaction lives in the consumer).
- [ ] 5.3 Create `Infrastructure/Persistence/Repositories/ILogActionRepository.cs` with `InsertAsync(LogAction, CancellationToken)` returning `LogAction`.
- [ ] 5.4 Create `Infrastructure/Persistence/Repositories/LogActionRepository.cs`: implement `InsertAsync` using EF Core `Add()` then `SaveChangesAsync()`. The UNIQUE constraint on `event_id` causes `DbUpdateException` with PostgresException code `23505` on duplicate — wrap and rethrow as a custom `DuplicateEventException` so the consumer can handle it as an idempotent skip.
- [ ] 5.5 Create `Infrastructure/Persistence/Repositories/ILogActionDetailRepository.cs` with `UpsertAsync(LogActionDetail, CancellationToken)`.
- [ ] 5.6 Create `Infrastructure/Persistence/Repositories/LogActionDetailRepository.cs`: implement `UpsertAsync` using raw SQL `INSERT ... ON CONFLICT (action_id) DO UPDATE SET request_payload = EXCLUDED.request_payload, response_payload = EXCLUDED.response_payload, error_payload = EXCLUDED.error_payload, metadata = EXCLUDED.metadata, updated_at = now()`. Parameterized inputs.
- [ ] 5.7 Write unit tests for each repository method: insert happy path, upsert creates new row, upsert updates existing row, duplicate event throws `DuplicateEventException`.
Repository tests should use SQLite in-memory mode to verify relational behavior where possible.

## 6. Common Utilities (Masker & Retry)

- [ ] 6.1 Create `Common/SensitiveFields.cs`: a `HashSet<string>` of deny-list field names: `password`, `access_token`, `refresh_token`, `authorization`, `otp`, `cardNumber`, `cvv`, `paymentSecret`, `secret`, `token` (lowercase for case-insensitive comparison).
- [ ] 6.2 Create `Common/SensitiveDataMasker.cs`: a `Mask(object? root)` method that recursively traverses the object tree. For `IDictionary<string, object?>` / `JsonElement` nodes, if the key (case-insensitive) is in the deny-list, replace the value with `"***"`. For array nodes, recurse into each element. Preserve structure for non-sensitive nodes. Unit tests: top-level sensitive field, nested sensitive field, sensitive field inside array, non-sensitive field unchanged, null root handled gracefully.
- [ ] 6.3 Create `Common/RetryPolicyFactory.cs`: static factory that builds a Polly `AsyncRetryPolicy<DbException>` (and `Exception` for Kafka broker errors) with configurable `maxAttempts`, `initialDelayMs`, `backoffMultiplier`, `maxDelayMs` from `IOptions<KafkaConsumerOptions>`. Export a `ExecuteAsync(Func<CancellationToken, Task>)` helper.

## 7. Kafka Consumer (BackgroundService)

- [ ] 7.1 Create `Infrastructure/Kafka/KafkaConsumerOptions.cs`: POCO matching the `Kafka` config section in `appsettings.json`, registered via `IOptions<KafkaConsumerOptions>`.
- [ ] 7.2 Create `Infrastructure/Kafka/IDlqPublisher.cs` with `PublishAsync(ConsumeResult<byte[], byte[]>, string failureReason, int attempt, CancellationToken)`.
- [ ] 7.3 Create `Infrastructure/Kafka/DlqPublisher.cs`: implements `IDlqPublisher`. Uses `IProducer<byte[], byte[]>` to produce to `options.DlqTopic`. Preserves original message key. Adds Kafka headers: `failure_reason`, `failed_at` (ISO-8601 UTC), `consumer_attempt`. Produces the masked JSON payload.
- [ ] 7.4 Create `Infrastructure/Kafka/KafkaLogConsumerService.cs`: `BackgroundService` implementing the 8-step pipeline. Key methods:
  - `ExecuteAsync(ct)`: create `ConsumerBuilder`, build consumer, subscribe to `options.Topic`, loop `consumer.Consume(ct)` with manual commit.
  - `ProcessMessageAsync(ConsumeResult, ct)`: orchestrates the pipeline steps.
  - `TryPersistAsync(message, ct)`: begin transaction → upsert flow → insert action → insert/upsert details → commit → return success flag.
  - `HandleFailure(result, reason, attempt, ct)`: if attempt < maxAttempts → retry with backoff; else → publish to DLQ and return.
  - Use structured logging (`ILogger<KafkaLogConsumerService>`) for every step, retry, idempotent skip, and DLQ publish.
- [ ] 7.5 Wire everything in `Program.cs`: `builder.Services.AddDbContext<LoggerDbContext>()`, `builder.Services.AddScoped<ILogFlowRepository, LogFlowRepository>`, `builder.Services.AddScoped<ILogActionRepository, LogActionRepository>`, `builder.Services.AddScoped<ILogActionDetailRepository, LogActionDetailRepository>`, `builder.Services.AddSingleton<IDlqPublisher, DlqPublisher>`, `builder.Services.AddHostedService<KafkaLogConsumerService>()`, `builder.Services.Configure<KafkaConsumerOptions>(...)`, `builder.Services.AddSingleton<SensitiveDataMasker>`, register `SensitiveFields` and retry policy factory.

## 8. Basic Tests

- [ ] 8.1 Create `backend/Skysim.Logger.Api.Tests/Skysim.Logger.Api.Tests.csproj` referencing the main project and `Microsoft.EntityFrameworkCore.Sqlite`, `Polly`, `FluentAssertions`, `Moq`. Add `Confluent.Kafka` for consumer tests.
- [ ] 8.2 Write `LogEventMessageValidatorTests.cs`: test cases — valid message passes; missing `eventId` fails; missing `flowId` fails; invalid `status` ("WEIRD") fails; invalid `actionType` ("FOO_BAR") fails; valid `GUEST` checkout without `userId` passes.
- [ ] 8.3 Write `SensitiveDataMaskerTests.cs`: top-level password masked, nested `authorization` masked, array of objects with `token` masked, non-sensitive fields unchanged, null and non-container values handled.
- [ ] 8.4 Write `KafkaLogConsumerServiceTests.cs` using `Moq` to mock `IConsumer<byte[], byte[]>`, `ILogFlowRepository`, `ILogActionRepository`, `ILogActionDetailRepository`, `IDlqPublisher`. Test: happy path persists and commits; duplicate `eventId` returns idempotent_skip and commits; validation failure publishes to DLQ and commits; transient DB error triggers retry.
- [ ] 8.5 Run `dotnet test backend/Skysim.Logger.Api.Tests/`. Confirm all tests pass with no warnings as errors.

## 9. Local Smoke Test (Manual Verification)

- [ ] 9.1 Ensure infra is up: `docker compose -f infra/docker-compose.yml up -d`. Verify PostgreSQL (port 5432) and Kafka (port 9092) are reachable.
- [ ] 9.2 Apply migrations: `dotnet ef database update --project backend/Skysim.Logger.Api`. Confirm "Applying migration 'InitialCreate'..." succeeds with no errors.
- [ ] 9.3 Start the API: `dotnet run --project backend/Skysim.Logger.Api`. Confirm the consumer logs "Starting KafkaLogConsumerService" and "Subscribed to skysim.action.logs" within 10 seconds.
- [ ] 9.4 Produce a test message using `kafka-console-producer` (or a small inline script):
  ```bash
  docker exec skysim-kafka kafka-console-producer --bootstrap-server localhost:9092 --topic skysim.action.logs --property "parse.key=true" --property "key.separator=:"
  # Then type:
  # test-flow-id-001:{"eventId":"11111111-1111-1111-1111-111111111111","flowId":"test-flow-id-001","flowType":"CHECKOUT_ESIM","serviceName":"Order","actionType":"ORDER_CREATED","status":"SUCCESS","createdAt":"2026-06-23T08:00:00.000Z","checkoutType":"GUEST","customerEmail":"test@example.com","orderId":"ORD-001","message":"Order created successfully"}
  ```
- [ ] 9.5 Query PostgreSQL: `SELECT flow_id, flow_type, status, total_steps FROM log_flows;` — confirm one row exists. `SELECT event_id, flow_id, action_type, status FROM log_actions;` — confirm one row exists. `SELECT action_id, request_payload IS NOT NULL AS has_request FROM log_action_details;` — confirm one row exists.
- [ ] 9.6 Produce a duplicate message (same `eventId`). Restart the API. Confirm no second row is inserted in `log_actions`. Confirm idempotent_skip log entry.
- [ ] 9.7 Produce a message with invalid `status`: confirm it does not create rows and the consumer does not crash.
- [ ] 9.8 Confirm structured log output includes `{eventId, flowId, actionType, status, durationMs, outcome}` for persisted messages.

## 10. Verification & Completion

- [ ] 10.1 Run `openspec validate "implement-logger-kafka-consumer-persistence" --strict` and confirm no errors.
- [ ] 10.2 Run `openspec status --change "implement-logger-kafka-consumer-persistence"` and confirm all artifacts (`proposal`, `design`, `tasks`) are `done` and `applyRequires` is satisfied.
- [ ] 10.3 Review `design.md` decisions against the implementation — confirm every decision was followed and note any deviations.
- [ ] 10.4 Mark all todos in this file as complete. Confirm the output is consistent with `define-logger-technical-design` specs (Kafka contract, database design, consumer flow, cross-cutting concerns).
