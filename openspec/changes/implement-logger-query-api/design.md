# Design: implement-logger-query-api

## Context

The approved Logger technical design (`define-logger-technical-design`) establishes a 3-table PostgreSQL schema and a Kafka consumer that persists events to `log_flows`, `log_actions`, and `log_action_details`. The `logger-query-api` spec in that same change defines the API surface: four read-only endpoints for listing flows, getting flow detail, listing actions for a flow, and getting action details with masked payloads.

The Kafka consumer (`implement-logger-kafka-consumer-persistence`) is complete and working. This change implements the query API layer on top of the existing persistence layer. The `SensitiveDataMasker` is already implemented and will be reused for masking payloads in the action detail endpoint.

**Current state:** PostgreSQL tables exist with data. `SensitiveDataMasker`, `SensitiveDataMasker`, and existing repository interfaces (`ILogFlowRepository`, `ILogActionRepository`, `ILogActionDetailRepository`) are available.

**Constraints:** Keep controllers thin (validate + call service, return response). Use DTOs for all API outputs. No database schema changes. No authentication in this phase. Use `FluentValidation` for query parameter validation.

**Stakeholders:** Frontend developer (consumes the APIs), Backend developer (implements), Mentor (reviews).

---

## Goals / Non-Goals

**Goals:**

- Implement `GET /api/log-flows` with pagination, sorting, and filtering (customerEmail, customerPhone, userId, orderId, paymentId, status, serviceName, fromDate, toDate).
- Implement `GET /api/log-flows/{flowId}` returning flow summary with ordered action timeline.
- Implement `GET /api/log-flows/{flowId}/actions` returning paginated actions for a flow with `serviceName` and filtering by `serviceName`.
- Implement `GET /api/log-actions/{actionId}/details` returning action with masked payloads.
- Use `SensitiveDataMasker` for payload masking.
- Return DTOs, not EF entities.
- Add Swagger documentation with `OpenApiOperation` attributes.
- Add unit tests for query/filter/pagination logic using in-memory SQLite.
- Extend `Program.cs` with DI registrations for new services.

**Non-Goals:**

- Middleware logging / Action Filter logging.
- Kafka Producer.
- Authentication / JWT.
- Frontend ReactJS.
- Changes to the database schema.
- Writing to the database (read-only).

---

## Decisions

### Decision 1 — Extend existing repositories with read-only query methods rather than creating separate query repositories

- **Why**: The existing `ILogFlowRepository` / `ILogActionRepository` interfaces have a single responsibility (write). Adding query methods directly to the same interfaces would mix concerns. Instead, create separate `ILogFlowQueryService` / `ILogActionQueryService` in a `Services/Query/` folder, using `IServiceScope` to get `DbContext` directly for complex queries with `Include()`.
- **Alternative**: Create full `ILogFlowQueryRepository` interfaces — rejected: extra abstraction layer with no benefit since services already act as the abstraction.
- **Consequence**: Query logic lives in `Services/Query/`. Repository interfaces stay write-only. `QueryService` classes use `LoggerDbContext` via DI.

### Decision 2 — Use `IQueryable<T>` with `AsNoTracking()` for all query methods

- **Why**: `IQueryable<T>` allows composing filters at the service layer before hitting the database. `AsNoTracking()` is appropriate since these are read-only operations and improves EF Core performance by skipping change-tracking overhead.
- **Consequence**: All query methods return `IQueryable<T>` or `Task<PagedResult<T>>`. The `DbContext` is injected via `IDbContextFactory<LoggerDbContext>` so each query service method creates its own scoped context.

### Decision 3 — `serviceName` filter uses a JOIN from `log_flows` to `log_actions`

- **Why**: `serviceName` is on `log_actions`, not `log_flows`. Filtering flows by `serviceName` means "return flows that have at least one action with this service name". Implemented as a subquery or `Exists()` on `LogActions`.
- **Alternative**: Return all actions with `serviceName` filter and group — rejected; the list API returns `LogFlowSummaryDto`, not actions.
- **Consequence**: Query for `serviceName` filter: `db.LogFlows.Where(f => db.LogActions.Any(a => a.FlowId == f.FlowId && a.ServiceName == filter.ServiceName))`.

### Decision 4 — `SensitiveDataMasker` is applied to `string?` JSON payload fields in the detail endpoint

- **Why**: `requestPayload`, `responsePayload`, `errorPayload`, and `metadata` are stored as `string?` in EF Core (PostgreSQL `jsonb` mapped as `string`). The masker operates on the deserialized JSON object tree (`JsonElement` from `JsonDocument.Parse()`), then the masked result is serialized back to a string.
- **Implementation**: In `LogActionQueryService.GetDetailsAsync()`, after loading the `LogActionDetail`, deserialize each non-null payload string to `JsonDocument`, call `SensitiveDataMasker.Mask()` recursively, serialize back to string, and map to DTO.
- **Consequence**: Payloads are masked on read. If masking logic changes, existing stored data is unaffected.

### Decision 5 — Use `FluentValidation` for query parameter DTOs

- **Why**: Consistent with the existing `LogEventMessageValidator`. Query filter/ pagination DTOs (`LogFlowListQuery`, `LogActionListQuery`) are validated with `AbstractValidator<T>`.
- **Consequence**: Validation runs in the controller or via `[DisabledValidation]` bypass for Swagger. Invalid pagination params return HTTP 400 with structured error body.

### Decision 6 — Use `System.Text.Json` with `JsonNamingPolicy.CamelCase` globally for API responses

- **Why**: The spec requires camelCase field names. Setting `JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase` at the `AddControllers()` level applies it to all JSON serialization, avoiding per-DTO attributes.
- **Consequence**: All API response DTOs use PascalCase C# properties but serialize as camelCase JSON.

---

## API Contract Summary

### `GET /api/log-flows`

**Query Parameters:**

| Parameter | Type | Required | Default | Notes |
|---|---|---|---|---|
| `customerEmail` | string | No | — | Exact match, case-insensitive |
| `customerPhone` | string | No | — | Exact match |
| `userId` | string | No | — | Exact match |
| `orderId` | string | No | — | Exact match |
| `paymentId` | string | No | — | Exact match |
| `flowType` | string | No | — | Exact match |
| `checkoutType` | string | No | — | Exact match |
| `status` | string | No | — | Exact match |
| `serviceName` | string | No | — | JOIN filter — flow has action with this serviceName |
| `fromDate` | date (YYYY-MM-DD) | No | — | `created_at >= fromDate` |
| `toDate` | date (YYYY-MM-DD) | No | — | `created_at <= toDate` (end of day) |
| `page` | int | No | 1 | Min 1 |
| `pageSize` | int | No | 20 | Min 1, Max 100 |
| `sortBy` | string | No | `createdAt` | `createdAt` \| `updatedAt` \| `completedAt` \| `status` |
| `sortDirection` | string | No | `desc` | `asc` \| `desc` |

**Response:** `PagedResponse<LogFlowSummaryDto>` — HTTP 200

### `GET /api/log-flows/{flowId}`

**Path Parameters:** `flowId` (string)

**Response:** `LogFlowDetailDto` (flow + `timeline: LogActionDto[]`) — HTTP 200, HTTP 404

### `GET /api/log-flows/{flowId}/actions`

**Path Parameters:** `flowId` (string)

**Query Parameters:** `serviceName`, `page`, `pageSize`, `sortBy` (only `stepOrder`), `sortDirection`

**Response:** `PagedResponse<LogActionDto>` — HTTP 200, HTTP 404

### `GET /api/log-actions/{actionId}/details`

**Path Parameters:** `actionId` (GUID)

**Response:** `LogActionDetailsDto` (action + masked payloads) — HTTP 200, HTTP 404

---

## DTOs

```csharp
// LogFlowSummaryDto — used in list responses
public record LogFlowSummaryDto(
    string FlowId,
    string FlowType,
    string? CheckoutType,
    string Status,
    string? CustomerEmail,
    string? CustomerPhone,
    string? UserId,
    string? OrderId,
    string? PaymentId,
    int TotalSteps,
    int SuccessSteps,
    int FailedSteps,
    string? LastActionType,
    string? LastMessage,
    DateTime StartedAt,
    DateTime? CompletedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

// LogFlowDetailDto — used in GET /api/log-flows/{flowId}
public record LogFlowDetailDto(LogFlowSummaryDto Flow, List<LogActionDto> Timeline);

// LogActionDto — used in timeline and action list
public record LogActionDto(
    Guid Id,
    Guid EventId,
    string FlowId,
    int StepOrder,
    string ServiceName,
    string ActionType,
    string Status,
    string? Message,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime? RequestTime,
    DateTime? ResponseTime,
    int? DurationMs,
    string? CorrelationId,
    DateTime CreatedAt);

// LogActionDetailsDto — used in GET /api/log-actions/{actionId}/details
public record LogActionDetailsDto(LogActionDto Action, string? RequestPayload, string? ResponsePayload, string? ErrorPayload, string? Metadata);

// PagedResponse<T>
public record PagedResponse<T>(List<T> Items, int Page, int PageSize, long TotalItems, int TotalPages);

// ApiErrorResponse
public record ApiErrorResponse(ApiErrorDetail Error);
public record ApiErrorDetail(string Code, string Message, List<ApiFieldError>? Details);
public record ApiFieldError(string Field, string Message);
```

---

## Project Structure

```
backend/Skysim.Logger.Api/
  Contracts/
    DTOs/
      LogFlowSummaryDto.cs
      LogFlowDetailDto.cs
      LogActionDto.cs
      LogActionDetailsDto.cs
      PagedResponse.cs
      ApiErrorResponse.cs
      Queries/
        LogFlowListQuery.cs
        LogActionListQuery.cs
  Services/
    Query/
      ILogFlowQueryService.cs
      LogFlowQueryService.cs
      ILogActionQueryService.cs
      LogActionQueryService.cs
  Controllers/
    LogFlowsController.cs
    LogActionsController.cs
  Validators/
    LogFlowListQueryValidator.cs
    LogActionListQueryValidator.cs
  Common/
    PagedResult.cs              ← internal helper for pagination calculation
backend/Skysim.Logger.Api.Tests/
  QueryServiceTests/
    LogFlowQueryServiceTests.cs
    LogActionQueryServiceTests.cs
```

---

## Pagination Logic

`PagedResult<T>` is an internal helper that accepts `IQueryable<T>`, applies `Skip((page-1)*pageSize)`, `Take(pageSize)`, and executes `CountAsync()` separately for `totalItems`. The service returns `PagedResponse<T>`.

```csharp
public async Task<PagedResponse<T>> ToPagedResponseAsync<T>(
    IQueryable<T> query, int page, int pageSize, CancellationToken ct)
{
    var totalItems = await query.CountAsync(ct);
    var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
    var items = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(ct);
    return new PagedResponse<T>(items, page, pageSize, totalItems, totalPages);
}
```

---

## Risks / Trade-offs

- **[Risk] `serviceName` filter requires a JOIN — O(N) query on large datasets**: Mitigation: `log_actions.flow_id` has an index. The `EXISTS` subquery is translated to a semi-join and is efficient with proper indexes. Revisit if query times exceed 500ms.
- **[Risk] Masking payloads on every detail call adds latency**: Mitigation: masking is O(data size) and only happens on the detail endpoint. The list endpoint never touches payloads. For very large payloads (>1 MB), consider lazy loading or streaming.
- **[Risk] Pagination without covering indexes may be slow**: Mitigation: `log_flows.created_at` already has an index per the existing schema. Verify `EXPLAIN ANALYZE` results for common pagination offsets.
- **[Risk] `SensitiveDataMasker` operates on `JsonElement` trees converted from `string`**: Mitigation: re-serialize masked JSON back to string; this handles the current `string?` EF Core mapping. If the mapping changes to `JsonDocument`, update accordingly.

---

## Open Questions

1. **Should `GET /api/log-flows/{flowId}/actions` be separate from `GET /api/log-flows/{flowId}` or merged?** Decision: keep separate — `GET /api/log-flows/{flowId}` returns the full timeline (all actions) for the detail view; `GET /api/log-flows/{flowId}/actions` adds pagination and `serviceName` filter for the frontend's action list panel.
2. **Should `LogFlowListQueryValidator` run in the controller or via an action filter?** Decision: run in controller action via `validator.ValidateAndThrow()` (with a try/catch returning 400). Simpler than an action filter for this phase.
3. **Should we add a `GET /api/log-flows/{flowId}/actions/{actionId}` shortcut?** Decision: no — `GET /api/log-actions/{actionId}/details` covers this use case. The flow-scoped path is redundant.
4. **Should `sortBy=status` on `log_flows` use a deterministic secondary sort?** Decision: yes — when sorting by `status`, add `created_at DESC` as a secondary sort to avoid arbitrary ordering of same-status rows.
