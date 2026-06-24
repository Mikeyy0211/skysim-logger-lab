# Tasks: implement-logger-query-api

This change implements the read-only Query API layer for the Skysim Logger. Follow the task order — later tasks depend on earlier ones. Each task has a concrete output and a verification step.

## 1. DTOs & Query Models

- [x] 1.1 Create `Contracts/DTOs/LogFlowSummaryDto.cs`: record with all flow fields (flowId, flowType, checkoutType, status, customerEmail, customerPhone, userId, orderId, paymentId, totalSteps, successSteps, failedSteps, lastActionType, lastMessage, startedAt, completedAt, createdAt, updatedAt). Use PascalCase properties.
- [x] 1.2 Create `Contracts/DTOs/LogFlowDetailDto.cs`: record containing `LogFlowSummaryDto Flow` and `List<LogActionDto> Timeline`.
- [x] 1.3 Create `Contracts/DTOs/LogActionDto.cs`: record with all action fields (Id, EventId, FlowId, StepOrder, ServiceName, ActionType, Status, Message, ErrorCode, ErrorMessage, RequestTime, ResponseTime, DurationMs, CorrelationId, CreatedAt). Include ServiceName as a top-level field.
- [x] 1.4 Create `Contracts/DTOs/LogActionDetailsDto.cs`: record containing `LogActionDto Action` and string? fields `RequestPayload`, `ResponsePayload`, `ErrorPayload`, `Metadata`.
- [x] 1.5 Create `Contracts/DTOs/PagedResponse.cs`: generic record with `List<T> Items`, `int Page`, `int PageSize`, `long TotalItems`, `int TotalPages`.
- [x] 1.6 Create `Contracts/DTOs/ApiErrorResponse.cs`: record with `ApiErrorDetail Error`. Create `ApiErrorDetail` (string Code, string Message, List<ApiFieldError>? Details) and `ApiFieldError` (string Field, string Message).
- [x] 1.7 Create `Contracts/DTOs/Queries/LogFlowListQuery.cs`: flat query model class with nullable properties for all filter params (customerEmail, customerPhone, userId, orderId, paymentId, flowType, checkoutType, status, serviceName, fromDate, toDate, page, pageSize, sortBy, sortDirection). Add `int Page` defaulting to 1, `int PageSize` defaulting to 20.
- [x] 1.8 Create `Contracts/DTOs/Queries/LogActionListQuery.cs`: flat query model class with flowId (required), serviceName, page, pageSize, sortBy, sortDirection.

## 2. Query Service Interfaces & Implementations

- [x] 2.1 Create `Services/Query/ILogFlowQueryService.cs`: interface with `Task<PagedResponse<LogFlowSummaryDto>> GetListAsync(LogFlowListQuery query, CancellationToken ct)` and `Task<LogFlowDetailDto?> GetByFlowIdAsync(string flowId, CancellationToken ct)`.
- [x] 2.2 Create `Services/Query/LogFlowQueryService.cs`: inject `IDbContextFactory<LoggerDbContext>` and `SensitiveDataMasker`. Implement `GetListAsync`: build `IQueryable<LogFlow>` with `AsNoTracking()`, apply filters (customerEmail with `EF.Functions.ILike` or `Contains`, phone/userId/orderId/paymentId exact match, flowType/checkoutType/status exact match, serviceName via `Any(a => a.ServiceName == filter.ServiceName)` subquery, fromDate/toDate range), apply sorting (createdAt/updatedAt/completedAt/status, asc/desc), then call `ToPagedResponseAsync()`. Implement `GetByFlowIdAsync`: load flow with `Include(f => f.Actions).ThenInclude(a => a.Detail).AsNoTracking()`, return `null` if not found, map to `LogFlowDetailDto` with timeline ordered by `StepOrder`. Do NOT include payloads in timeline.
- [x] 2.3 Create `Services/Query/ILogActionQueryService.cs`: interface with `Task<PagedResponse<LogActionDto>> GetByFlowIdAsync(LogActionListQuery query, CancellationToken ct)` and `Task<LogActionDetailsDto?> GetDetailsAsync(Guid actionId, CancellationToken ct)`.
- [x] 2.4 Create `Services/Query/LogActionQueryService.cs`: inject `IDbContextFactory<LoggerDbContext>` and `SensitiveDataMasker`. Implement `GetByFlowIdAsync`: query `LogActions` filtered by flowId and optional serviceName, ordered by stepOrder, apply pagination. Implement `GetDetailsAsync`: load action with `Include(a => a.Detail)`, return `null` if not found. For each non-null payload string, deserialize to `JsonDocument`, call `SensitiveDataMasker.Mask()`, serialize back to string, then map to `LogActionDetailsDto`.

## 3. FluentValidation Validators

- [x] 3.1 Create `Validators/LogFlowListQueryValidator.cs` using `AbstractValidator<LogFlowListQuery>`: `page` >= 1, `pageSize` between 1 and 100, `sortBy` must be one of `createdAt`, `updatedAt`, `completedAt`, `status` (or null for default), `sortDirection` must be `asc` or `desc` (or null for default), `fromDate` and `toDate` must be valid ISO-8601 date strings. All filter fields are optional strings.
- [x] 3.2 Create `Validators/LogActionListQueryValidator.cs` using `AbstractValidator<LogActionListQuery>`: `flowId` is required and non-empty, `page` >= 1, `pageSize` between 1 and 100, `serviceName` is optional string.
- [x] 3.3 Register validators in `Program.cs`: `builder.Services.AddScoped<IValidator<LogFlowListQuery>, LogFlowListQueryValidator>()` and `builder.Services.AddScoped<IValidator<LogActionListQuery>, LogActionListQueryValidator>()`.

## 4. Controllers

- [x] 4.1 Create `Controllers/LogFlowsController.cs`: `[ApiController]`, `[Route("api/log-flows")]`. Inject `ILogFlowQueryService`, `ILogActionQueryService`, `IValidator<LogFlowListQuery>`, `IValidator<LogActionListQuery>`.
  - `GET /` — parse query params into `LogFlowListQuery`, validate, return `PagedResponse<LogFlowSummaryDto>` or 400.
  - `GET /{flowId}` — return `LogFlowDetailDto` or 404.
  - `GET /{flowId}/actions` — parse query params into `LogActionListQuery`, validate, return `PagedResponse<LogActionDto>` or 404 (flow not found) or 400.
  - Add `[ProducesResponseType]` attributes for all outcomes.
  - Add `[OpenApiOperation]` tags for Swagger.
- [x] 4.2 Create `Controllers/LogActionsController.cs`: `[ApiController]`, `[Route("api/log-actions")]`. Inject `ILogActionQueryService`.
  - `GET /{actionId}/details` — return `LogActionDetailsDto` or 404.
  - Add `[ProducesResponseType]` attributes and `[OpenApiOperation]` tags.

## 5. Program.cs Updates

- [x] 5.1 Add `builder.Services.AddControllers().AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase)` for camelCase JSON.
- [x] 5.2 Register query services: `builder.Services.AddScoped<ILogFlowQueryService, LogFlowQueryService>()`, `builder.Services.AddScoped<ILogActionQueryService, LogActionQueryService>()`.
- [x] 5.3 Ensure `AddEndpointsApiExplorer()` and `AddSwaggerGen()` are present (already there from previous change).

## 6. Swagger Documentation

- [x] 6.1 Add `[OpenApiOperation(...)]` and `[ProducesResponseType(typeof(PagedResponse<LogFlowSummaryDto>), StatusCodes.Status200OK)]`, `[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]`, `[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]` to all controller actions.
- [x] 6.2 Enable Swagger XML comments: add `<GenerateDocumentationFile>true</GenerateDocumentationFile>` and `<NoWarn>$(NoWarn);1591</NoWarn>` to `Skysim.Logger.Api.csproj`, call `builder.Services.AddEndpointsApiExplorer().AddXmlDocumentation()` using `Swashbuckle.AspNetCore`. Document each endpoint summary in code comments.

## 7. Unit Tests

- [x] 7.1 Create `QueryServiceTests/LogFlowQueryServiceTests.cs`: use SQLite in-memory. Test cases — empty DB returns empty page; pagination returns correct subset; sortBy createdAt desc default; sortBy status with secondary sort; filter by customerEmail (case-insensitive); filter by status; filter by date range; filter by serviceName (JOIN); filter by multiple fields (AND); unknown flowId returns null; existing flowId returns detail with timeline ordered by stepOrder.
- [x] 7.2 Create `QueryServiceTests/LogActionQueryServiceTests.cs`: use SQLite in-memory. Test cases — paginated action list scoped to flowId; filter by serviceName; unknown flowId returns empty page; unknown actionId returns null; existing actionId returns detail with masked payloads (verify `password` -> `***`); null payloads remain null.
- [x] 7.3 Create `QueryServiceTests/LogFlowListQueryValidatorTests.cs`: test cases — valid query passes; page=0 fails; pageSize=0 fails; pageSize=101 fails; invalid sortBy fails; invalid sortDirection fails; valid with all optional fields passes.
- [x] 7.4 Create `QueryServiceTests/LogActionListQueryValidatorTests.cs`: test cases — valid query passes; empty flowId fails; page=0 fails; pageSize=200 fails.
- [x] 7.5 Run `dotnet test backend/Skysim.Logger.Api.Tests/`. All tests must pass with no warnings.

## 8. Verification & Completion

- [x] 8.1 Run `openspec status --change "implement-logger-query-api"` and confirm all artifacts are `done`.
- [x] 8.2 Review `design.md` decisions against implementation — confirm all decisions followed.
- [x] 8.3 All tasks marked complete in this file.
