# Implementation Tasks

## 1. Keep Contract Compatible and Add Minimal HTTP Fields

- [x] 1.1 Review current `LogEventMessage.cs` in `Skysim.Logger.Contracts/Events/`
- [x] 1.2 Keep existing required fields unchanged: `eventId`, `flowId`, `flowType`, `actionType`, `serviceName`, `status`, `createdAt`
- [x] 1.3 Add only missing flat HTTP fields if needed: `sourceService`, `method`, `path`, `queryString`, `statusCode`, `durationMs`, `requestBody`, `responseBody`
- [x] 1.4 Do not remove existing fields in this phase
- [x] 1.5 Do not modify `Logger.Api` consumer/persistence unless strictly required for compile compatibility

## 2. Refactor LoggerMiddleware

- [x] 2.1 Review current `LoggerMiddleware.cs` implementation
- [x] 2.2 Refactor `LoggerMiddleware` to directly read `HttpContext` and build one HTTP log event per request
- [x] 2.3 Remove unnecessary nested request/response builders if they make the middleware hard to explain
- [x] 2.4 Use the existing `SensitiveDataMasker`; do not rewrite masking unless required
- [x] 2.5 Use `X-Flow-Id` consistently when generating a new flowId
- [x] 2.6 Add 32KB body size limit with `"[too large]"` placeholder
- [x] 2.7 Extract `sourceService` from `X-Source-Service` header
- [x] 2.8 Get `serviceName` from `LoggerOptions`, not from Kafka producer
- [x] 2.9 Verify Kafka publish failure does not break the business request

## 3. Verify SensitiveDataMasker

- [x] 3.1 Review current `SensitiveDataMasker.cs` implementation
- [x] 3.2 Verify it masks standard sensitive fields: `password`, `token`, `accessToken`, `refreshToken`, `authorization`, `secret`, `apiKey`
- [x] 3.3 Verify it handles null, empty, and non-JSON input gracefully
- [x] 3.4 Do not rewrite the masker if it already satisfies these checks

## 4. Test Middleware Behavior

- [x] 4.1 Verify flowId extraction priority: `X-Flow-Id` → `X-Correlation-Id` → `X-Request-Id` → generate GUID
- [x] 4.2 Verify `X-Flow-Id` response header is returned when flowId is generated
- [x] 4.3 Verify request body is captured and masked
- [x] 4.4 Verify response body is captured and masked
- [x] 4.5 Verify error information is captured when an exception occurs
- [x] 4.6 Verify Kafka publish failure does not break business request

## 5. Build and Verify

- [x] 5.1 Build `Skysim.Logger.Client` project
- [x] 5.2 Build `Skysim.Logger.SampleService` project
- [x] 5.3 Run tests if any exist for LoggerMiddleware
- [ ] 5.4 Start infrastructure
- [ ] 5.5 Start `Skysim.Logger.Api` and `Skysim.Logger.SampleService`
- [ ] 5.6 Test with Postman: send request with `X-Flow-Id` and `X-Source-Service` headers
- [ ] 5.7 Test with Postman: send request without `X-Flow-Id` header and verify new flowId is generated
- [ ] 5.8 Verify Kafka UI shows one middleware HTTP log message with flattened HTTP fields
- [ ] 5.9 Verify sensitive data is masked in Kafka message
- [x] 5.10 Verify no frontend files are modified
- [x] 5.11 Verify no new business action logging is added in this phase
