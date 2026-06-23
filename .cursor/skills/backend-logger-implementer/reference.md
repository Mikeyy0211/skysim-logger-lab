# Backend Logger Reference

## Database Schema

### Table: log_flows
Stores flow summary for fast searching/filtering.

| Column | Type | Notes |
|--------|------|-------|
| id | SERIAL | Primary key |
| flow_id | UUID | Unique, indexed |
| flow_type | VARCHAR(100) | e.g., CHECKOUT_ESIM |
| checkout_type | VARCHAR(50) | GUEST or AUTHENTICATED |
| status | VARCHAR(50) | PENDING, IN_PROGRESS, SUCCESS, FAILED |
| customer_email | VARCHAR(255) | Indexed |
| customer_phone | VARCHAR(50) | Indexed |
| user_id | VARCHAR(100) | Indexed, nullable |
| order_id | VARCHAR(100) | Indexed, nullable |
| payment_id | VARCHAR(100) | Indexed, nullable |
| total_steps | INT | Default 0 |
| success_steps | INT | Default 0 |
| failed_steps | INT | Default 0 |
| last_action_type | VARCHAR(100) | nullable |
| last_message | TEXT | nullable |
| started_at | TIMESTAMP | |
| completed_at | TIMESTAMP | nullable |
| created_at | TIMESTAMP | Default now |
| updated_at | TIMESTAMP | Default now |

### Table: log_actions
Stores each action/timeline step in a flow.

| Column | Type | Notes |
|--------|------|-------|
| id | SERIAL | Primary key |
| event_id | UUID | Unique, idempotency key |
| flow_id | UUID | Foreign key, indexed |
| step_order | INT | Sequence in flow |
| service_name | VARCHAR(100) | |
| action_type | VARCHAR(100) | e.g., ORDER_CREATED |
| status | VARCHAR(50) | SUCCESS, FAILED, PENDING |
| message | TEXT | nullable |
| error_code | VARCHAR(100) | nullable |
| error_message | TEXT | nullable |
| created_at | TIMESTAMP | Default now |
| updated_at | TIMESTAMP | Default now |

### Table: log_action_details
Stores heavy JSON payloads.

| Column | Type | Notes |
|--------|------|-------|
| id | SERIAL | Primary key |
| action_id | INT | Foreign key to log_actions |
| request_payload | JSONB | nullable |
| response_payload | JSONB | nullable |
| error_payload | JSONB | nullable |
| metadata | JSONB | nullable |
| created_at | TIMESTAMP | Default now |

## Indexes

```sql
CREATE INDEX idx_log_flows_customer_email ON log_flows(customer_email);
CREATE INDEX idx_log_flows_customer_phone ON log_flows(customer_phone);
CREATE INDEX idx_log_flows_user_id ON log_flows(user_id);
CREATE INDEX idx_log_flows_order_id ON log_flows(order_id);
CREATE INDEX idx_log_flows_payment_id ON log_flows(payment_id);
CREATE INDEX idx_log_flows_status ON log_flows(status);
CREATE INDEX idx_log_flows_created_at ON log_flows(created_at);
CREATE INDEX idx_log_flows_flow_id ON log_flows(flow_id);

CREATE INDEX idx_log_actions_flow_id ON log_actions(flow_id);
CREATE INDEX idx_log_actions_event_id ON log_actions(event_id);
CREATE INDEX idx_log_actions_service_name ON log_actions(service_name);
CREATE INDEX idx_log_actions_action_type ON log_actions(action_type);
CREATE INDEX idx_log_actions_created_at ON log_actions(created_at);
```

## Kafka Message Format

```json
{
  "eventId": "uuid",
  "flowId": "uuid",
  "flowType": "CHECKOUT_ESIM",
  "serviceName": "OrderService",
  "actionType": "ORDER_CREATED",
  "status": "SUCCESS",
  "message": "Order created successfully",
  "createdAt": "2026-06-19T10:00:00Z",
  "metadata": {
    "customerEmail": "test@example.com",
    "customerPhone": "+1234567890",
    "orderId": "ORD-123",
    "paymentId": "PAY-456",
    "userId": "USR-789",
    "checkoutType": "GUEST"
  },
  "requestPayload": { ... },
  "responsePayload": { ... }
}
```

## Sensitive Fields to Mask

```csharp
private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
{
    "password",
    "access_token",
    "refresh_token",
    "authorization",
    "otp",
    "cardNumber",
    "cvv",
    "paymentSecret",
    "secret",
    "token"
};
```

## Action Types Enum

```csharp
public static class ActionTypes
{
    public const string OrderCreated = "ORDER_CREATED";
    public const string PaymentRequested = "PAYMENT_REQUESTED";
    public const string PaymentSuccess = "PAYMENT_SUCCESS";
    public const string ProviderRequested = "PROVIDER_REQUESTED";
    public const string EsimActivated = "ESIM_ACTIVATED";
    public const string EmailSent = "EMAIL_SENT";
    public const string OrderFailed = "ORDER_FAILED";
    public const string PaymentFailed = "PAYMENT_FAILED";
    public const string ProviderFailed = "PROVIDER_FAILED";
    public const string EsimActivationFailed = "ESIM_ACTIVATION_FAILED";
    public const string EmailFailed = "EMAIL_FAILED";
}
```

## Status Enum

```csharp
public static class FlowStatuses
{
    public const string Pending = "PENDING";
    public const string InProgress = "IN_PROGRESS";
    public const string Success = "SUCCESS";
    public const string Failed = "FAILED";
}
```

## Checkout Types

```csharp
public static class CheckoutTypes
{
    public const string Guest = "GUEST";
    public const string Authenticated = "AUTHENTICATED";
}
```

## Kafka Topics

```csharp
public static class KafkaTopics
{
    public const string ActionLogs = "skysim.action.logs";
    public const string DeadLetterQueue = "skysim.action.logs.dlq";
}
```
