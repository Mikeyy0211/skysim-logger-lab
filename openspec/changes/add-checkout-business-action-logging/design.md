## Context

The logger module currently supports only HTTP middleware logging via `LoggerMiddleware`. When `POST /api/checkout/esim` is called, SampleService publishes an HTTP_REQUEST log with flowType=HTTP_ACTION and empty business fields (customerEmail, customerPhone, checkoutType, orderId, paymentId).

Operations need to trace checkout flows by customerEmail, customerPhone, orderId, or paymentId. They need to see which service (OrderService, PaymentService, etc.) handled each step of the checkout process.

The existing `KafkaLogConsumerService` persists business fields via `MapFlowFromMessage`. However, the current implementation always overwrites all fields on upsert, which means:
- If HTTP_ACTION log creates a flow first, then CHECKOUT_ESIM events update it correctly
- But if HTTP_ACTION log arrives AFTER CHECKOUT_ESIM events, it overwrites business fields with nulls

Since HTTP middleware logs arrive when the HTTP response is sent, and business events are published after the controller returns, the order depends on timing. To ensure safety, the upsert logic must merge fields correctly.

## Goals / Non-Goals

**Goals:**

- Add business action logging to SampleService that publishes CHECKOUT_ESIM flow events
- Support both GUEST (no Authorization header) and AUTHENTICATED (with Authorization header) checkout types
- Simulate multiple service names to demonstrate cross-service flow tracking
- Populate business fields in log_flows (customerEmail, customerPhone, orderId, paymentId)
- Keep existing HTTP middleware logging unchanged
- Ensure LogFlow upsert correctly merges business fields without overwriting existing values with nulls

**Non-Goals:**

- No JWT validation in SampleService
- No new Kafka topics or message contracts
- No frontend changes
- No new contracts or DTOs beyond existing LogEventMessage
- No userId extraction from JWT claims (keep null for AUTHENTICATED for now)
- No changes to totalSteps/successSteps/failedSteps behavior beyond existing logic

## Decisions

### Decision 1: Create a BusinessActionLogger service in SampleService

**Choice**: Create a new `BusinessActionLogger` class that injects `IKafkaLogProducer` and exposes methods to publish business action events.

**Rationale**: Keeps business logging logic separate from the controller. Easy to test. Follows single responsibility principle.

**Alternative**: Add business logging directly in `CheckoutController`. Rejected because it would mix HTTP handling with business event publishing.

### Decision 2: Controller awaits business logging

**Choice**: `CheckoutController` SHALL await `BusinessActionLogger.PublishCheckoutFlowAsync` before returning the HTTP response.

**Rationale**: Ensures business events are published before the response is sent. The method catches and logs errors internally, so publish failures do not affect the HTTP response.

**Alternative**: Fire-and-forget. Rejected because it cannot guarantee business events are sent before timing issues with Kafka.

### Decision 3: BusinessActionLogger catches and logs errors without throwing

**Choice**: `BusinessActionLogger.PublishCheckoutFlowAsync` SHALL catch all exceptions from `IKafkaLogProducer.PublishAsync`, log them, and complete without throwing.

**Rationale**: Ensures checkout processing succeeds even if Kafka publishing fails. Errors are logged for debugging.

**Alternative**: Let exceptions propagate. Rejected because a Kafka failure should not fail the HTTP checkout response.

### Decision 4: Merge business fields on upsert conflict

**Choice**: Update `MapFlowFromMessage` in `KafkaLogConsumerService` to merge business fields on conflict. Use incoming non-null values to fill nulls in the existing flow.

**Merge rules:**
- flowType: Only update to CHECKOUT_ESIM if existing is HTTP_ACTION (upgrade path)
- checkoutType: Use incoming if non-null, preserve existing otherwise
- customerEmail: Use incoming if non-null, preserve existing otherwise
- customerPhone: Use incoming if non-null, preserve existing otherwise
- userId: Use incoming if non-null, preserve existing otherwise
- orderId: Use incoming if non-null, preserve existing otherwise
- paymentId: Use incoming if non-null, preserve existing otherwise
- lastActionType/lastMessage: Use conditional logic based on flow type and action type (see below)

**lastActionType/lastMessage merge rules:**

1. **HTTP_ACTION-only flows**: HTTP_REQUEST SHALL update lastActionType and lastMessage normally, preserving existing middleware logging behavior.

2. **CHECKOUT_ESIM flows with HTTP_REQUEST arriving later**: Preserve the last business action (e.g., EMAIL_SENT), do not overwrite with HTTP_REQUEST.

3. **Implementation logic**:
   - If incoming action is HTTP_REQUEST AND existing flow is CHECKOUT_ESIM: preserve lastActionType/lastMessage
   - Otherwise: update lastActionType/lastMessage from incoming message

**Rationale**: Ensures business fields are captured when CHECKOUT_ESIM events arrive, even if HTTP_ACTION was processed first. Also prevents HTTP_REQUEST from overwriting business data if it arrives later. HTTP_ACTION-only flows remain unchanged.

### Decision 5: Simulate service names for each business action

**Choice**: Each business action is published with a simulated service name matching the real service that would handle it in production:

| Action Type | Service Name |
|-------------|--------------|
| ORDER_CREATED | OrderService |
| PAYMENT_REQUESTED | PaymentService |
| PAYMENT_SUCCESS | PaymentService |
| PROVIDER_REQUESTED | CoreService |
| ESIM_ACTIVATED | ProviderService |
| EMAIL_SENT | NotificationService |

**Rationale**: Demonstrates cross-service flow tracking without implementing real services. Operations can see which service handled each step.

### Decision 6: Generate orderId and paymentId in controller

**Choice**: Generate `orderId` as `ORD-{GUID}` and `paymentId` as `PAY-{GUID}` in `CheckoutController`, include them in both the response and business action messages.

**Rationale**: The controller already generates `orderId`. Adding `paymentId` generation keeps the response complete for client consumption.

### Decision 7: No new action types or flow types

**Choice**: Use existing `ActionTypes` and `FlowTypes` constants (ActionTypes.OrderCreated, FlowTypes.CheckoutEsim).

**Rationale**: The existing contracts already support all needed action types and flow types. No changes to Contracts project.

## Technical Approach

### Modified File: `KafkaLogConsumerService.MapFlowFromMessage`

```csharp
private static void MapFlowFromMessage(LogFlow flow, LogEventMessage message)
{
    // Upgrade flowType: HTTP_ACTION -> CHECKOUT_ESIM
    if (flow.FlowType == FlowTypes.HttpAction && message.FlowType == FlowTypes.CheckoutEsim)
    {
        flow.FlowType = message.FlowType;
    }

    // Merge business fields: use incoming non-null values
    flow.CheckoutType ??= message.CheckoutType;
    flow.CustomerEmail ??= message.CustomerEmail;
    flow.CustomerPhone ??= message.CustomerPhone;
    flow.UserId ??= message.UserId;
    flow.OrderId ??= message.OrderId;
    flow.PaymentId ??= message.PaymentId;

    // Update status from latest event
    flow.Status = message.Status;
    flow.StartedAt = message.CreatedAt;

    // Preserve lastActionType/lastMessage: HTTP_REQUEST after CHECKOUT_ESIM should not overwrite business action
    // HTTP_ACTION-only flows: update normally from HTTP_REQUEST
    // CHECKOUT_ESIM flows with HTTP_REQUEST arriving later: preserve last business action
    bool isExistingBusinessFlow = flow.FlowType == FlowTypes.CheckoutEsim;
    bool isHttpRequest = message.ActionType == ActionTypes.HttpRequest;

    if (isHttpRequest && isExistingBusinessFlow)
    {
        // Preserve existing lastActionType/lastMessage (business action preserved)
    }
    else
    {
        flow.LastActionType = message.ActionType;
        flow.LastMessage = message.Message;
    }

    if (message.Status == StatusTypes.Success)
    {
        flow.SuccessSteps++;
        if (IsTerminalAction(message.ActionType, message.Status))
        {
            flow.CompletedAt = DateTime.UtcNow;
        }
    }
    else if (message.Status == StatusTypes.Failed)
    {
        flow.FailedSteps++;
        flow.CompletedAt = DateTime.UtcNow;
    }
    flow.TotalSteps++;
}
```

### New File: `Services/BusinessActionLogger.cs`

```csharp
public interface IBusinessActionLogger
{
    Task PublishCheckoutFlowAsync(CheckoutEsimRequest request, string flowId, string checkoutType,
        string orderId, string? paymentId, CancellationToken ct = default);
}

public class BusinessActionLogger : IBusinessActionLogger
{
    private readonly IKafkaLogProducer _producer;
    private readonly ILogger<BusinessActionLogger> _logger;

    public async Task PublishCheckoutFlowAsync(...)
    {
        // Publish 6 business events sequentially
        // Catch and log errors without throwing
    }
}
```

### Modified File: `CheckoutController.cs`

- Inject `IBusinessActionLogger`
- Generate `paymentId` matching pattern `"PAY-{GUID}"`
- Await `PublishCheckoutFlowAsync` after creating response
- Include business fields in response

### Business Event Sequence

1. ORDER_CREATED (OrderService)
2. PAYMENT_REQUESTED (PaymentService) - includes paymentId
3. PAYMENT_SUCCESS (PaymentService) - includes paymentId
4. PROVIDER_REQUESTED (CoreService)
5. ESIM_ACTIVATED (ProviderService)
6. EMAIL_SENT (NotificationService)

### Message Fields for Each Business Event

| Field | Value |
|-------|-------|
| eventId | New Guid per event |
| flowId | Same as HTTP log flowId |
| flowType | CHECKOUT_ESIM |
| serviceName | Per action (see table above) |
| actionType | Per action |
| status | SUCCESS |
| createdAt | Current UTC time |
| checkoutType | GUEST or AUTHENTICATED |
| customerEmail | From request |
| customerPhone | From request |
| userId | null (no JWT validation) |
| orderId | Generated in controller |
| paymentId | Generated (for payment actions) |
| message | Descriptive message |
| durationMs | Small increment (10-100ms) |

## Timeline Expectation

Since HTTP middleware logging remains enabled, a flow detail may contain:
- One HTTP_REQUEST technical action (published by LoggerMiddleware)
- Plus six CHECKOUT_ESIM business actions (published by BusinessActionLogger)

This is acceptable. The timeline will show all 7 actions. Business fields will be populated from the CHECKOUT_ESIM events.

## Risks / Trade-offs

- [Risk] Event ordering in Kafka (HTTP log vs business events)
  - [Mitigation] Merge logic ensures business fields are preserved regardless of arrival order
- [Risk] Kafka publish failure causes checkout to fail
  - [Mitigation] BusinessActionLogger catches errors and logs them; checkout succeeds even if publishing fails
- [Risk] Multiple events per request increases Kafka message volume
  - [Mitigation] Acceptable for demo; production would batch or use separate topic
- [Trade-off] Timeline contains both HTTP_REQUEST and business actions
  - Accepted: Manual verification checks that 6 business actions exist, not that timeline has exactly 6 items
- [Trade-off] No real service integration
  - Accepted for training/demo

## Migration Plan

1. Update `MapFlowFromMessage` in `KafkaLogConsumerService` with merge logic
2. Implement `BusinessActionLogger` service
3. Update `CheckoutController` to inject and await the service
4. Add `paymentId` field to `CheckoutEsimResponse`
5. Register `BusinessActionLogger` in `Program.cs`
6. Add unit tests for `BusinessActionLogger`
7. Add unit tests for `MapFlowFromMessage` merge behavior
8. Run `dotnet build` to verify compilation
9. Run `dotnet test` to verify all tests pass
10. Manual test: POST /api/checkout/esim and verify via Logger.Api GET /api/log-flows/{flowId}

## Open Questions

None. All decisions are finalized.
