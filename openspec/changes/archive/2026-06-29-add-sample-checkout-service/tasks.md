## 1. Project Setup

- [x] 1.1 Create `backend/Skysim.Logger.SampleService/` folder structure
- [x] 1.2 Create `Skysim.Logger.SampleService.csproj` with .NET 8, references to Contracts and Client only
- [x] 1.3 Add `Skysim.Logger.SampleService` project to `Skysim.Logger.sln`

## 2. Configuration

- [x] 2.1 Create `appsettings.json` with Kafka, Logging, and Logger configuration sections
- [x] 2.2 Create `appsettings.Development.json` with local overrides if needed

## 3. DTOs

- [x] 3.1 Create `DTOs/CheckoutEsimRequest.cs` with customerEmail, customerPhone, packageCode, quantity properties
- [x] 3.2 Create `DTOs/CheckoutEsimResponse.cs` with flowId, orderId, checkoutType, status, message properties

## 4. FlowIdSeedingMiddleware (Local to SampleService)

- [x] 4.1 Create `Middlewares/FlowIdSeedingMiddleware.cs`
- [x] 4.2 Implement middleware logic:
  - If request does not contain `X-Flow-Id` header, generate a new GUID
  - Set `Request.Headers["X-Flow-Id"]` to the flowId value
  - If `X-Flow-Id` already exists, keep it unchanged
  - Call `_next(context)` to continue pipeline
- [x] 4.3 Do NOT modify `Skysim.Logger.Client`

## 5. Controller

- [x] 5.1 Create `Controllers/CheckoutController.cs` with POST `/api/checkout/esim` endpoint
- [x] 5.2 Read flowId from `Request.Headers["X-Flow-Id"]` (set by FlowIdSeedingMiddleware)
- [x] 5.3 Return the same flowId in `CheckoutEsimResponse.flowId`
- [x] 5.4 Implement checkout type detection from Authorization header presence (GUEST if absent, AUTHENTICATED if present)
- [x] 5.5 Do NOT validate JWT tokens in this phase
- [x] 5.6 Return mock response with flowId, orderId, checkoutType, status, message

## 6. Middleware and Producer Configuration

- [x] 6.1 Register `ISensitiveDataMasker` (SensitiveDataMasker) in DI container
- [x] 6.2 Register `IKafkaLogProducer` (KafkaLogProducer) in DI container with configuration from appsettings.json
- [x] 6.3 Register `FlowIdSeedingMiddleware` in the pipeline BEFORE `LoggerMiddleware`
- [x] 6.4 Register `LoggerMiddleware` in the ASP.NET Core request pipeline
- [x] 6.5 Configure LoggerMiddleware service name as "sample-checkout-service"
- [x] 6.6 Do NOT exclude `/api/checkout/*` paths from logging

## 7. Program.cs Assembly

- [x] 7.1 Create `Program.cs` with minimal hosting setup
- [x] 7.2 Add controllers, logging, and configuration services
- [x] 7.3 Add Swagger/OpenAPI for Development environment only
- [x] 7.4 Do NOT add JWT authentication to Swagger

## 8. Dependency Boundary Verification

- [x] 8.1 Verify `SampleService.csproj` references only `Skysim.Logger.Client` and `Skysim.Logger.Contracts`
- [x] 8.2 Verify `SampleService.csproj` does NOT reference `Skysim.Logger.Api`
- [x] 8.3 Verify `SampleService.csproj` does NOT reference `Skysim.Logger.Infrastructure`
- [x] 8.4 Verify `SampleService.csproj` does NOT reference `Skysim.Logger.Common`

## 9. Build and Verification

- [x] 9.1 Build project: `dotnet build backend/Skysim.Logger.SampleService/Skysim.Logger.SampleService.csproj`
- [x] 9.2 Verify solution includes SampleService: `dotnet sln backend/Skysim.Logger.sln list`
- [x] 9.3 Run dependency check:
  ```bash
  grep -c "Skysim.Logger.Api" backend/Skysim.Logger.SampleService/Skysim.Logger.SampleService.csproj
  # Should return 0
  ```
- [x] 9.4 Run dependency check:
  ```bash
  grep -c "Skysim.Logger.Infrastructure" backend/Skysim.Logger.SampleService/Skysim.Logger.SampleService.csproj
  # Should return 0
  ```
- [x] 9.5 Run dependency check:
  ```bash
  grep -c "Skysim.Logger.Common" backend/Skysim.Logger.SampleService/Skysim.Logger.SampleService.csproj
  # Should return 0
  ```
- [x] 9.6 Validate OpenSpec: `openspec validate add-sample-checkout-service --strict`
- [x] 9.7 Run tests (if any): `dotnet test backend/Skysim.Logger.Api.Tests/`

## 10. Manual FlowId Consistency Verification

- [ ] 10.1 Start SampleService in Development mode
- [ ] 10.2 POST `/api/checkout/esim` without `X-Flow-Id` header:
  ```bash
  curl -X POST http://localhost:5000/api/checkout/esim \
    -H "Content-Type: application/json" \
    -d '{"customerEmail":"test@example.com","customerPhone":"+1234567890","packageCode":"PKG001","quantity":1}'
  ```
- [ ] 10.3 Verify response contains a generated flowId (GUID)
- [ ] 10.4 Verify Kafka consumer received log with the SAME flowId (full DB verification deferred to `verify-logger-e2e-pipeline`)
- [ ] 10.5 POST `/api/checkout/esim` with `X-Flow-Id` header:
  ```bash
  curl -X POST http://localhost:5000/api/checkout/esim \
    -H "Content-Type: application/json" \
    -H "X-Flow-Id: my-custom-flow-123" \
    -d '{"customerEmail":"test@example.com","customerPhone":"+1234567890","packageCode":"PKG001","quantity":1}'
  ```
- [ ] 10.6 Verify response contains `flowId = "my-custom-flow-123"`
- [ ] 10.7 Verify Kafka consumer received log with `flowId = "my-custom-flow-123"`
