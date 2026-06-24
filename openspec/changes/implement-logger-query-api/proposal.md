## Why

The Kafka consumer pipeline (`implement-logger-kafka-consumer-persistence`) successfully ingests log events from Kafka into PostgreSQL, but there is no way to query that data. Operators and support staff need read-only REST APIs to search flows by customer email, phone, order ID, payment ID, status, and date range; to view flow timelines; and to inspect action payloads. Without these APIs, the logged data is invisible.

## What Changes

- New `GET /api/log-flows` endpoint: paginated list of flows with filtering by `customerEmail`, `customerPhone`, `userId`, `orderId`, `paymentId`, `status`, `serviceName`, `fromDate`, `toDate` and sorting.
- New `GET /api/log-flows/{flowId}` endpoint: single flow summary with ordered action timeline (no heavy payloads).
- New `GET /api/log-flows/{flowId}/actions` endpoint: paginated action list for a flow with `serviceName` included.
- New `GET /api/log-actions/{actionId}/details` endpoint: action summary plus `requestPayload`, `responsePayload`, `errorPayload` (masked) from `log_action_details`.
- Query service layer with `IQueryService` / `QueryService` separating business logic from controllers.
- Query repository interfaces and implementations extending the existing read/write repositories.
- DTOs: `LogFlowSummaryDto`, `LogFlowDetailDto`, `LogActionDto`, `LogActionDetailsDto`, `PagedResponse<T>`, `ApiErrorResponse`.
- FluentValidation request validators for query parameters.
- Swagger (Swashbuckle) XML comments and `OpenApiOperation` attributes on all endpoints.
- Unit tests covering query/filter/pagination logic with in-memory SQLite.
- Extension of `Program.cs` to register query services and controllers.

## Capabilities

### New Capabilities

- `log-flow-list-api`: Read-only paginated list of `log_flows` with filter and sort support. Produces `specs/log-flow-list-api/spec.md`.
- `log-flow-detail-api`: Single flow with ordered action timeline. Produces `specs/log-flow-detail-api/spec.md`.
- `log-action-list-api`: Paginated action list scoped to a flow. Produces `specs/log-action-list-api/spec.md`.
- `log-action-detail-api`: Action with heavy payload details. Produces `specs/log-action-detail-api/spec.md`.

### Modified Capabilities

- `logger-query-api`: The existing capability in `define-logger-technical-design/specs/logger-query-api/spec.md` is being **implemented** — requirements are unchanged; this change is the implementation phase.

## Impact

- **Backend**: New files under `Contracts/DTOs/`, `Services/Query/`, `Infrastructure/Persistence/Repositories/`, `Controllers/`, `Validators/`. `Program.cs` updated to register new services.
- **Tests**: New test files under `Skysim.Logger.Api.Tests/`.
- **Database**: No schema changes; uses existing `log_flows`, `log_actions`, `log_action_details` tables.
- **Kafka**: No impact.
- **Frontend**: Out of scope.
- **Authentication**: Out of scope (stub placeholder only).
