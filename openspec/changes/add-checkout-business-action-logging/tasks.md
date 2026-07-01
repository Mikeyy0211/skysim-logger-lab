## 1. Update LogFlowRepository Upsert Merge Logic

- [x] 1.1 Update `MapFlowFromMessage` in `KafkaLogConsumerService.cs` to use merge logic
- [x] 1.2 Add flowType upgrade rule: HTTP_ACTION -> CHECKOUT_ESIM
- [x] 1.3 Add business field merge rule: preserve existing non-null values
- [x] 1.4 Add lastActionType/lastMessage preservation for HTTP_REQUEST after business actions

## 2. Create BusinessActionLogger Service

- [x] 2.1 Create `backend/Skysim.Logger.SampleService/Services/` directory
- [x] 2.2 Create `IBusinessActionLogger.cs` interface with `PublishCheckoutFlowAsync` method
- [x] 2.3 Create `BusinessActionLogger.cs` implementing the interface with all 6 business events publishing logic
- [x] 2.4 Add required using statements for Contracts constants (ActionTypes, FlowTypes, CheckoutTypes, StatusTypes)
- [x] 2.5 Implement error handling: catch exceptions from producer, log them, do not throw

## 3. Update CheckoutController

- [x] 3.1 Add `IBusinessActionLogger` to constructor injection
- [x] 3.2 Generate `paymentId` matching pattern `"PAY-{GUID}"`
- [x] 3.3 Add `paymentId` field to `CheckoutEsimResponse` DTO
- [x] 3.4 Update response to include generated `paymentId`
- [x] 3.5 Await `PublishCheckoutFlowAsync` after creating response (not fire-and-forget)

## 4. Register BusinessActionLogger in DI

- [x] 4.1 Register `IBusinessActionLogger` as scoped service in `Program.cs`

## 5. Add Unit Tests for SampleService

- [x] 5.1 Create or update `backend/Skysim.Logger.SampleService.Tests/` test project
- [x] 5.2 Create `BusinessActionLoggerTests.cs`
- [x] 5.3 Add test for publishing all 6 events with correct fields
- [x] 5.4 Add test for using same flowId across all events
- [x] 5.5 Add test for setting correct checkoutType
- [x] 5.6 Add test for handling publish errors gracefully (does not throw)

## 6. Add Unit Tests for Logger.Api Merge Behavior

- [x] 6.1 Create `KafkaLogConsumerServiceTests.cs` or add to existing test project
- [x] 6.2 Add test: HTTP_ACTION creates flow, then CHECKOUT_ESIM updates business fields
- [x] 6.3 Add test: CHECKOUT_ESIM exists, HTTP_ACTION arrives later and must not clear business fields
- [x] 6.4 Add test: CHECKOUT_ESIM exists, HTTP_ACTION arrives later and must not downgrade flowType
- [x] 6.5 Add test: HTTP_ACTION-only flow still works (existing behavior preserved)
- [x] 6.6 Add test: HTTP_ACTION-only flow should still set lastActionType = HTTP_REQUEST
- [x] 6.7 Add test: CHECKOUT_ESIM flow preserves lastActionType when HTTP_REQUEST arrives later

## 7. Build and Test

- [x] 7.1 Run `dotnet build` to verify compilation
- [x] 7.2 Run `dotnet test` to verify all tests pass

## 8. Manual Verification

- [ ] 8.1 Start local infrastructure with Docker Compose
- [ ] 8.2 Start Logger.Api
- [ ] 8.3 Start SampleService
- [ ] 8.4 Send POST request to `/api/checkout/esim` with test data
- [ ] 8.5 Query `GET /api/log-flows/{flowId}` to verify business fields are populated
- [ ] 8.6 Verify flowType is CHECKOUT_ESIM
- [ ] 8.7 Verify customerEmail, customerPhone, orderId, paymentId are present
- [ ] 8.8 Verify timeline shows all 6 business actions with correct service names (timeline may also include 1 HTTP_REQUEST action)
