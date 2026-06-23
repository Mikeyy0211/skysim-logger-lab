---
name: backend-logger-implementer
description: Implement Backend Logger Service for skysim-logger-lab project. Use when building the Logger API, Kafka Consumer, PostgreSQL persistence, Middleware Logging, Action Log Publisher, or Log Query APIs. Applies ASP.NET Core / .NET 8 patterns with thin controllers, service-based business logic, DTOs for request/response, and PostgreSQL with EF Core.
disable-model-invocation: true
---

# Backend Logger Implementer

Implement production-style Backend Logger Service for the skysim-logger-lab project.

## Core Requirements

### Architecture
- Implement production-style ASP.NET Core / .NET 8 code
- Keep Controllers thin (validate request shape, call services, return response)
- Put business logic in services/use cases
- Use DTOs for API requests/responses
- Do not expose database entities directly

### Database (PostgreSQL with EF Core)
- Use `log_flows` table for flow summary (fast search/filter)
- Use `log_actions` table for each action/timeline step
- Use `log_action_details` table for heavy JSON payloads
- Add indexes for: customerEmail, customerPhone, userId, orderId, paymentId, status, createdAt, flowId, serviceName, actionType
- Do not load heavy payloads in list APIs
- Load payloads only in detail APIs
- Always implement pagination for list APIs

### Kafka Consumer
- Main topic: `skysim.action.logs`
- Message key: flowId
- Use manual offset commit
- **Never commit Kafka offset before database save succeeds**
- Ensure idempotency using eventId as unique key
- Avoid duplicate log_actions
- Implement retry strategy for temporary failures
- Dead-letter topic (if used): `skysim.action.logs.dlq`

### Enums and Constants
Use explicit enums/constants for:
- Flow types
- Checkout types (GUEST, AUTHENTICATED)
- Action types: ORDER_CREATED, PAYMENT_REQUESTED, PAYMENT_SUCCESS, PROVIDER_REQUESTED, ESIM_ACTIVATED, EMAIL_SENT, ORDER_FAILED, PAYMENT_FAILED, PROVIDER_FAILED, ESIM_ACTIVATION_FAILED, EMAIL_FAILED
- Statuses
- Kafka topic names

### API Endpoints
Implement these APIs:
- `GET /api/log-flows` - List with filtering, pagination, sorting
- `GET /api/log-flows/{flowId}` - Get flow detail
- `GET /api/log-actions/{actionId}` - Get action detail with payloads

List API filters:
- customerEmail, customerPhone, userId, orderId, paymentId
- flowType, checkoutType, status
- fromDate, toDate, page, pageSize, sortBy, sortDirection

Response format:
```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "totalItems": 100,
  "totalPages": 5
}
```

### Middleware Logging
Build reusable middleware or action filter for technical logging:
- Collect: service, action, requestTime, responseTime, duration, requestData, responseData, userId, exception, correlationId
- Mask sensitive fields: password, access_token, refresh_token, authorization, otp, cardNumber, cvv, paymentSecret, secret, token

### Action Log Publisher
Publish business action logs at important steps:
- ORDER_CREATED, PAYMENT_SUCCESS, ESIM_ACTIVATED, etc.
- Use eventId, flowId, flowType, serviceName, actionType, status, createdAt

## Code Generation Workflow

### Step 1: Explain Intended Files
Before generating code, explain the files to create/modify:
- Controllers (thin, only validate/call/return)
- Services/UseCases (business logic)
- DTOs (request/response models)
- Entities (database models)
- Infrastructure/Persistence (DbContext, repositories)
- Infrastructure/Kafka (consumer, publisher)
- Middlewares (technical logging)
- Enums/Constants

### Step 2: Generate Complete Code
- Generate complete, working code (no TODOs)
- Use async/await and CancellationToken
- Prefer explicit types over `var`
- Avoid magic strings/numbers
- No silent exception swallowing
- Log meaningful errors
- Return consistent API responses

### Step 3: Explain Run and Test
After code generation:
- Explain how to build and run
- Explain how to test the APIs
- Mention any prerequisites (Docker Compose for PostgreSQL/Kafka)

### Step 4: State Assumptions
Clearly mention any assumptions made:
- Database connection string location
- Kafka bootstrap servers
- Default pagination values
- Any missing configuration

## Folder Structure

Follow this structure:
```
Skysim.Logger.Api/
├── Controllers/
├── Contracts/DTOs/
├── Domain/Entities/
├── Infrastructure/Persistence/
├── Infrastructure/Kafka/
├── Services/UseCases/
├── Middlewares/
├── Options/
├── Common/
└── Enums/
```

## Business Flow Context

The main flow is `CHECKOUT_ESIM`:
- GUEST: no JWT, no userId, trace by email/phone/orderId/paymentId
- AUTHENTICATED: has JWT and userId, also store email/phone/orderId/paymentId for support

## Additional Resources

- For detailed database schema, see [reference.md](reference.md)
- For code examples, see [examples.md](examples.md)
