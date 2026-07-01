## Why

The logger module currently supports only HTTP middleware logging in SampleService. Business fields like customerEmail, customerPhone, checkoutType, orderId, and paymentId are null in log_flows because the existing flow type is HTTP_ACTION. Operations need to trace checkout flows by email/phone/orderId and view the business timeline showing which service handled each action.

HTTP middleware logs and business action logs reuse the same flowId. The LogFlowRepository upsert must merge business fields correctly: a CHECKOUT_ESIM event must not be overwritten by a later HTTP_ACTION event.

## What Changes

- **Add business action logging to SampleService**: When `POST /api/checkout/esim` is called, SampleService will publish a sequence of business action logs to Kafka in addition to the existing HTTP middleware log.
- **Simulate multiple service names**: Each business action will be published with a simulated service name (OrderService, PaymentService, CoreService, ProviderService, NotificationService) to demonstrate cross-service flow tracking.
- **Populate business fields**: The business action messages will include customerEmail, customerPhone, checkoutType, orderId, paymentId, and userId (when Authorization header is present).
- **Support guest and authenticated flows**: The checkoutType will be determined by the presence of an Authorization header (GUEST vs AUTHENTICATED).
- **Update LogFlowRepository upsert merge logic**: On conflict, merge nullable business fields from incoming events without overwriting existing non-null values.
- **Keep implementation simple**: No JWT validation, no new contracts, reuse existing LogEventMessage and action types.

## Capabilities

### New Capabilities

- `checkout-business-action-logging`: Publish business action logs for CHECKOUT_ESIM flows to enable operations to search and trace checkout flows by customerEmail, customerPhone, orderId, or paymentId.

### Modified Capabilities

- `sample-checkout-service`: Extend the existing checkout endpoint to publish business action logs after handling the checkout request.
- `log-flow-repository`: Update upsert conflict resolution to merge business fields correctly, preventing HTTP_ACTION from overwriting CHECKOUT_ESIM flow data.

## Impact

- **Backend (SampleService)**: New `BusinessActionLogger` service that publishes business events to Kafka using existing `IKafkaLogProducer`.
- **Backend (Logger.Api)**: Update `MapFlowFromMessage` in `KafkaLogConsumerService` to use merge logic instead of overwrite. On conflict, preserve existing business fields when incoming event has null values.
- **Backend (Logger.Infrastructure)**: No changes to repository; merge logic lives in consumer service.
- **Kafka**: Same `skysim.action.logs` topic, same message format, additional messages per checkout request.
- **No frontend changes**: Existing FE detail integration can display business fields if persisted correctly.
- **No infrastructure changes**: Local Kafka config remains unchanged.
