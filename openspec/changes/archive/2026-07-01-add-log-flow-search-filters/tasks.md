## 1. Constants and DTO Updates

- [x] 1.1 Add `RUNNING` and `PARTIAL_FAILED` constants to `Skysim.Logger.Contracts/Constants/StatusTypes.cs`
- [x] 1.2 Add `Search` property to `Skysim.Logger.Api/Contracts/Queries/LogFlowListQuery.cs`

## 2. Validator Updates

- [x] 2.1 Update `LogFlowListQueryValidator.cs` to validate `Search` max length (e.g., 200 chars)
- [x] 2.2 Add enum-aware validation for `Status` using `StatusTypes` constants: SUCCESS, FAILED, RUNNING, PARTIAL_FAILED (do NOT include IN_PROGRESS)
- [x] 2.3 Add enum-aware validation for `FlowType` using `FlowTypes` constants: CHECKOUT_ESIM, HTTP_ACTION
- [x] 2.4 Add enum-aware validation for `CheckoutType` using `CheckoutTypes` constants: GUEST, AUTHENTICATED

## 3. DTO Updates

- [x] 3.1 Add `LastServiceName` property (nullable string) to `Skysim.Logger.Api/Contracts/DTOs/LogFlowSummaryDto.cs`

## 4. Query Service Updates

- [x] 4.1 Update `LogFlowQueryService.GetListAsync()` to add search predicate across flowId, customerEmail, customerPhone, orderId, paymentId, userId, lastMessage
- [x] 4.2 Use `EF.Functions.ILike()` for case-insensitive PostgreSQL search. Guard each field with null check: `field != null && EF.Functions.ILike(field, $"%{search}%")`
- [x] 4.3 Change default sort from `createdAt` to `updatedAt` descending in `ApplySorting()`
- [x] 4.4 Compute `LastServiceName` in `GetListAsync()` using bulk-fetch of actions (no N+1). For CHECKOUT_ESIM flows: ignore HTTP_REQUEST actions, return serviceName of latest business action. For HTTP_ACTION flows: return serviceName of latest action by stepOrder. For flows with no actions: return null.
- [x] 4.5 Update `GetByFlowIdAsync()` to use the same lastServiceName logic (CHECKOUT_ESIM ignores HTTP_REQUEST, HTTP_ACTION uses latest stepOrder)

**Note:** `ILike` is Npgsql-specific and not supported by the EF Core InMemory provider. For unit tests, use `ToLower().Contains()` as the fallback expression. For `lastServiceName`, a bulk fetch of actions per page is used to avoid N+1 queries and allow in-memory filtering by flowType.

## 5. Unit Tests

- [x] 5.1 Update existing test `GetListAsync_SortByCreatedAtDesc_IsDefault` to `GetListAsync_SortByUpdatedAtDesc_IsDefault` to reflect new default sort
- [x] 5.2 Add test for search matching flowId (case-insensitive)
- [x] 5.3 Add test for search matching customerEmail (case-insensitive)
- [x] 5.4 Add test for search matching customerPhone (case-insensitive)
- [x] 5.5 Add test for search matching orderId (case-insensitive)
- [x] 5.6 Add test for search matching paymentId (case-insensitive)
- [x] 5.7 Add test for search matching userId (case-insensitive)
- [x] 5.8 Add test for search matching lastMessage (case-insensitive)
- [x] 5.9 Add test for search case-insensitivity (search "DEMO" matches "demo")
- [x] 5.10 Add test for search combined with flowType filter
- [x] 5.11 Add test for search combined with status filter
- [x] 5.12 Add test for search combined with checkoutType filter
- [x] 5.13 Add test for search combined with multiple filters
- [x] 5.14 Add test for pagination with search
- [x] 5.15 Add test for invalid status enum value returning 400
- [x] 5.16 Add test for invalid flowType enum value returning 400
- [x] 5.17 Add test for invalid checkoutType enum value returning 400
- [x] 5.18 Add test for empty/null search is ignored (no validation error)
- [x] 5.19 Add test for search exceeding max length returning 400
- [x] 5.20 Add test for lastServiceName reflecting the latest business action by StepOrder for CHECKOUT_ESIM flows
- [x] 5.21 Add test for lastServiceName being null when flow has no actions
- [x] 5.22 Add test for lastServiceName returning NotificationService for CHECKOUT_ESIM flow with EMAIL_SENT action
- [x] 5.23 Add test for lastServiceName returning sample-checkout-service for HTTP_ACTION flow with HTTP_REQUEST action
- [x] 5.24 Add test for CHECKOUT_ESIM flow with EMAIL_SENT then HTTP_REQUEST returns lastServiceName = NotificationService (HTTP_REQUEST ignored)
- [x] 5.25 Add test for HTTP_ACTION flow still uses latest stepOrder (HTTP_REQUEST included)

## 6. Build and Verification

- [x] 6.1 Run `dotnet build` and verify no build errors
- [x] 6.2 Run `dotnet test` and verify all tests pass
- [ ] 6.3 Manually verify `GET /api/log-flows` without params still works
- [ ] 6.4 Manually verify `GET /api/log-flows?search=detail.demo@example.com` returns matching flows
- [ ] 6.5 Manually verify `GET /api/log-flows?search=0900000003` returns matching flows
- [ ] 6.6 Manually verify `GET /api/log-flows?search=ORD` returns matching flows
- [ ] 6.7 Manually verify `GET /api/log-flows?search=PAY` returns matching flows
- [ ] 6.8 Manually verify `GET /api/log-flows?search=demo-business-flow` returns matching flows
- [ ] 6.9 Manually verify `GET /api/log-flows?status=INVALID` returns 400
- [ ] 6.10 Manually verify `GET /api/log-flows?flowType=INVALID` returns 400
- [ ] 6.11 Manually verify `GET /api/log-flows?checkoutType=INVALID` returns 400
- [ ] 6.12 Manually verify `GET /api/log-flows?search=detail.demo@example.com&flowType=CHECKOUT_ESIM&status=SUCCESS` works with combined filters
- [ ] 6.13 Manually verify `GET /api/log-flows` returns results sorted by `updatedAt` descending
- [ ] 6.14 Manually verify `GET /api/log-flows` response includes `lastServiceName` for flows with actions
- [ ] 6.15 Manually verify `GET /api/log-flows` response includes `lastServiceName: null` for flows without actions
- [ ] 6.16 Manually verify CHECKOUT_ESIM flow with EMAIL_SENT then HTTP_REQUEST returns lastServiceName = NotificationService (not sample-checkout-service)
- [ ] 6.17 Manually verify HTTP_ACTION flow returns lastServiceName = sample-checkout-service
- [ ] 6.18 Manually verify CHECKOUT_ESIM flow with only HTTP_REQUEST returns lastServiceName = sample-checkout-service (no business actions exist)
