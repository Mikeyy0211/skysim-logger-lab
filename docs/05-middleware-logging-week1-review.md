# 05 - Middleware Logging Design & Week 1 Review Summary

## 1. Mục tiêu

Tài liệu này hoàn thiện phần thiết kế cuối của Tuần 1 - Backend Foundation.

Mục tiêu chính:

- Thiết kế Middleware Logging dùng chung cho các backend service.
- Phân biệt rõ Technical Logging và Business Action Logging.
- Chuẩn hóa format log trước khi publish lên Kafka.
- Xác định cách lấy UserId, CorrelationId, Request/Response, Exception.
- Xác định rule mask dữ liệu nhạy cảm.
- Tổng hợp nội dung review cuối Tuần 1.

---

## 2. Bối cảnh

Trong hệ thống Skysim, nhiều service cùng tham gia xử lý một flow nghiệp vụ như checkout eSIM:

```text
Frontend
  ↓
KONG Gateway
  ↓
Order Service
  ↓
Payment Service
  ↓
Core / Provider Service
  ↓
Notification Service
```

Nếu mỗi service ghi log theo một format khác nhau thì việc truy vết lỗi sẽ khó khăn. Vì vậy cần có một Middleware Logging hoặc Action Filter dùng chung để chuẩn hóa log kỹ thuật.

Logger Service sẽ nhận log/event qua Kafka và lưu vào PostgreSQL để phục vụ:

- Tra cứu log.
- Debug lỗi.
- Theo dõi timeline flow.
- Đối soát giao dịch.
- Monitoring vận hành.
- Điều tra sự cố.

---

## 3. Phân biệt Technical Logging và Business Action Logging

### 3.1 Technical Logging

Technical Logging là log ở tầng kỹ thuật HTTP/API.

Thông tin cần thu thập:

- Service name.
- HTTP method.
- Request path.
- Request query.
- Request body.
- Response body.
- HTTP status code.
- Request time.
- Response time.
- Duration.
- UserId nếu có.
- CorrelationId.
- Exception nếu có.

Ví dụ:

```json
{
  "service": "OrderService",
  "action": "POST /api/orders",
  "correlationId": "corr-abc-123",
  "userId": "USER001",
  "requestTime": "2026-06-19T10:00:00Z",
  "responseTime": "2026-06-19T10:00:01Z",
  "duration": 1000,
  "statusCode": 200,
  "requestData": {},
  "responseData": {},
  "exception": null
}
```

Technical Logging phù hợp để trả lời:

- API nào được gọi?
- Request mất bao lâu?
- Status code là gì?
- Có exception không?
- User nào gọi API?
- Request/response là gì?

---

### 3.2 Business Action Logging

Business Action Logging là log ở tầng nghiệp vụ.

Nó không chỉ ghi API chạy thành công hay thất bại, mà ghi rõ hành động nghiệp vụ đã xảy ra.

Ví dụ trong flow checkout eSIM:

- ORDER_CREATED
- PAYMENT_REQUESTED
- PAYMENT_SUCCESS
- PROVIDER_REQUESTED
- ESIM_ACTIVATED
- EMAIL_SENT

Ví dụ message:

```json
{
  "eventId": "evt-001",
  "flowId": "CHECKOUT-20260619-000001",
  "flowType": "CHECKOUT_ESIM",
  "checkoutType": "GUEST",
  "serviceName": "PaymentService",
  "actionType": "PAYMENT_SUCCESS",
  "status": "SUCCESS",
  "stepOrder": 3,
  "orderId": "ORD001",
  "paymentId": "PAY001",
  "message": "Payment completed successfully",
  "createdAt": "2026-06-19T10:01:00Z"
}
```

Business Action Logging phù hợp để trả lời:

- Đơn hàng đã tạo chưa?
- Thanh toán đã thành công chưa?
- Provider đã được gọi chưa?
- eSIM đã kích hoạt chưa?
- Email đã gửi chưa?
- Flow đang dừng ở bước nào?
- Lỗi xảy ra ở action nghiệp vụ nào?

---

### 3.3 Kết luận phân biệt


| Loại log                | Mục đích                | Ví dụ                                                |
| ----------------------- | ----------------------- | ---------------------------------------------------- |
| Technical Logging       | Debug kỹ thuật HTTP/API | `POST /api/orders`, duration, status code, exception |
| Business Action Logging | Trace flow nghiệp vụ    | `ORDER_CREATED`, `PAYMENT_SUCCESS`, `ESIM_ACTIVATED` |


Kết luận:

- Middleware Logging dùng để ghi technical log tự động.
- Business Action Log nên được publish tại các điểm quan trọng trong business flow.
- Hai loại log này bổ trợ nhau, không thay thế hoàn toàn cho nhau.

---

## 4. Thiết kế Middleware Logging

### 4.1 Vị trí của Middleware

Middleware nằm trong pipeline của từng backend service.

Luồng xử lý:

```text
HTTP Request
  ↓
Logging Middleware
  ↓
Controller
  ↓
Service / UseCase
  ↓
Database / Kafka / External API
  ↓
Logging Middleware capture response
  ↓
HTTP Response
```

Middleware sẽ:

1. Nhận request.
2. Tạo hoặc lấy CorrelationId.
3. Ghi nhận requestTime.
4. Capture request body nếu được phép.
5. Gọi middleware tiếp theo.
6. Capture response body nếu được phép.
7. Ghi nhận responseTime và duration.
8. Capture exception nếu có.
9. Mask dữ liệu nhạy cảm.
10. Publish technical log lên Kafka.

---

### 4.2 Log format đề xuất

```json
{
  "eventId": "evt-tech-001",
  "logType": "TECHNICAL",
  "service": "OrderService",
  "action": "POST /api/orders",
  "correlationId": "corr-abc-123",
  "userId": "USER001",
  "requestTime": "2026-06-19T10:00:00Z",
  "responseTime": "2026-06-19T10:00:01Z",
  "duration": 1000,
  "httpMethod": "POST",
  "path": "/api/orders",
  "queryString": "",
  "statusCode": 200,
  "requestData": {},
  "responseData": {},
  "exception": null,
  "createdAt": "2026-06-19T10:00:01Z"
}
```

---

## 5. Field giải thích


| Field         | Ý nghĩa                          | Bắt buộc |
| ------------- | -------------------------------- | -------- |
| eventId       | ID duy nhất của log event        | Có       |
| logType       | TECHNICAL hoặc ACTION            | Có       |
| service       | Tên service phát sinh log        | Có       |
| action        | API/action kỹ thuật              | Có       |
| correlationId | Mã trace request                 | Có       |
| userId        | User đang gọi API, nếu có        | Không    |
| requestTime   | Thời điểm nhận request           | Có       |
| responseTime  | Thời điểm trả response           | Có       |
| duration      | Thời gian xử lý ms               | Có       |
| httpMethod    | GET/POST/PUT/DELETE              | Có       |
| path          | API path                         | Có       |
| statusCode    | HTTP status code                 | Có       |
| requestData   | Request payload đã mask          | Không    |
| responseData  | Response payload đã mask         | Không    |
| exception     | Thông tin exception đã chuẩn hóa | Không    |
| createdAt     | Thời điểm tạo log                | Có       |


---

## 6. CorrelationId Design

### 6.1 CorrelationId là gì?

CorrelationId là mã dùng để trace một request kỹ thuật khi đi qua nhiều service.

Ví dụ:

```text
Frontend gửi request tạo order
  ↓ correlationId = corr-abc-123
KONG
  ↓
Order Service
  ↓
Payment Service
  ↓
Provider Service
```

Tất cả service có thể dùng chung `correlationId = corr-abc-123` để trace kỹ thuật.

---

### 6.2 Cách lấy hoặc tạo CorrelationId

Rule đề xuất:

1. Nếu request header đã có `X-Correlation-Id` thì dùng lại.
2. Nếu chưa có thì middleware tạo mới GUID.
3. Middleware gắn `X-Correlation-Id` vào response header.
4. Khi service publish Kafka message, nên include correlationId.
5. Khi service gọi service khác, nên forward correlationId qua header.

Header:

```text
X-Correlation-Id: corr-abc-123
```

---

### 6.3 flowId khác correlationId thế nào?


| Khái niệm     | Mục đích                                 | Ví dụ                    |
| ------------- | ---------------------------------------- | ------------------------ |
| flowId        | Gom các action của một flow nghiệp vụ    | CHECKOUT-20260619-000001 |
| correlationId | Trace request kỹ thuật qua nhiều service | corr-abc-123             |
| orderId       | Mã đơn hàng nghiệp vụ                    | ORD001                   |
| paymentId     | Mã giao dịch thanh toán                  | PAY001                   |


Kết luận:

- `flowId` dùng cho business tracing.
- `correlationId` dùng cho technical tracing.
- Không nên gộp hai khái niệm này thành một.

---

## 7. UserId Design

### 7.1 Authenticated request

Nếu request có JWT hợp lệ:

- Middleware lấy `userId` từ claim trong token.
- Có thể lấy thêm email/role nếu cần.

Ví dụ claim:

- `sub`
- `user_id`
- `nameidentifier`

### 7.2 Guest request

Nếu request không có JWT:

- `userId = null`.
- Vẫn có thể trace bằng:
  - customerEmail
  - customerPhone
  - orderId
  - paymentId
  - flowId
  - correlationId

---

## 8. Data Masking Rule

Logger không được lưu dữ liệu nhạy cảm dạng raw.

### 8.1 Các field phải mask

Các field cần mask:

- password
- access_token
- refresh_token
- authorization
- otp
- token
- secret
- cardNumber
- cvv
- paymentSecret
- privateKey

### 8.2 Giá trị sau khi mask

Ví dụ:

```json
{
  "password": "***MASKED***",
  "access_token": "***MASKED***",
  "authorization": "***MASKED***"
}
```

### 8.3 Vì sao cần mask?

Lý do:

- Tránh lộ token.
- Tránh lộ thông tin thanh toán.
- Tránh lộ OTP/password.
- Đáp ứng yêu cầu bảo mật và code review.
- Logger phục vụ vận hành nhưng không được trở thành nơi rò rỉ dữ liệu nhạy cảm.

---

## 9. Middleware Error Handling

Middleware cần xử lý exception rõ ràng.

Luồng lỗi:

```text
Request vào middleware
  ↓
Controller/Service throw exception
  ↓
Middleware catch exception
  ↓
Capture exception info
  ↓
Publish technical log với status FAILED
  ↓
Re-throw exception hoặc trả response theo global exception handler
```

Thông tin exception nên lưu:

- exceptionType
- message
- stackTrace nếu môi trường development
- errorCode nếu có
- service
- path
- correlationId

Không nên:

- Nuốt lỗi silently.
- Trả stack trace cho client production.
- Lưu dữ liệu nhạy cảm trong exception message.

---

## 10. Publish Technical Log lên Kafka

### 10.1 Topic đề xuất

Có 2 phương án:

#### Phương án A: Dùng chung topic action log

```text
skysim.action.logs
```

Ưu điểm:

- Đơn giản.
- Logger Consumer chỉ consume một topic.

Nhược điểm:

- Technical log và business action log lẫn trong cùng topic.
- Cần thêm `logType`.

#### Phương án B: Tách topic technical log

```text
skysim.technical.logs
```

Ưu điểm:

- Tách biệt rõ technical log và action log.
- Dễ scale consumer riêng.

Nhược điểm:

- Cần quản lý nhiều topic hơn.

### 10.2 Đề xuất hiện tại

Trong phase đầu, có thể dùng chung topic:

```text
skysim.action.logs
```

Nhưng message cần có:

```json
{
  "logType": "TECHNICAL"
}
```

Khi mở rộng, có thể tách technical log sang topic riêng:

```text
skysim.technical.logs
```

---

## 11. Middleware Integration Design

### 11.1 Cách tích hợp vào service

Trong ASP.NET Core, middleware có thể được đăng ký trong pipeline:

```csharp
app.UseMiddleware<RequestResponseLoggingMiddleware>();
```

Hoặc tạo extension:

```csharp
app.UseSkysimLogging();
```

### 11.2 Shared package

Khi triển khai thực tế, nên đóng gói middleware thành package dùng chung:

```text
Skysim.Shared.Logging
```

Các service khác chỉ cần add package và config:

```csharp
builder.Services.AddSkysimLogging(options =>
{
    options.ServiceName = "OrderService";
    options.KafkaTopic = "skysim.action.logs";
});
```

---

## 12. Cấu hình đề xuất

Ví dụ `appsettings.json`:

```json
{
  "SkysimLogging": {
    "ServiceName": "OrderService",
    "KafkaTopic": "skysim.action.logs",
    "EnableRequestBodyLogging": true,
    "EnableResponseBodyLogging": true,
    "MaxBodySizeToLogInBytes": 4096,
    "MaskSensitiveData": true,
    "SensitiveFields": [
      "password",
      "access_token",
      "refresh_token",
      "authorization",
      "otp",
      "token",
      "secret",
      "cardNumber",
      "cvv"
    ]
  }
}
```

---

## 13. Middleware Logging Processing Flow

```text
1. Receive HTTP request.
2. Read or generate correlationId.
3. Add correlationId to response header.
4. Capture requestTime.
5. Capture request method/path/query.
6. Capture request body if enabled and safe.
7. Execute next middleware.
8. Capture responseTime.
9. Calculate duration.
10. Capture statusCode.
11. Capture response body if enabled and safe.
12. Catch exception if thrown.
13. Mask sensitive data.
14. Build technical log message.
15. Publish message to Kafka.
```

---

## 14. Những lưu ý khi capture Request/Response Body

### 14.1 Request Body

Cần cẩn thận vì request body stream chỉ đọc một lần.

Trong ASP.NET Core cần enable buffering nếu muốn đọc request body rồi vẫn cho controller đọc tiếp.

Rule:

- Chỉ log body khi cần.
- Giới hạn kích thước body.
- Không log file upload.
- Không log binary content.
- Luôn mask sensitive data.

### 14.2 Response Body

Capture response body phức tạp hơn vì phải thay response stream tạm thời.

Rule:

- Chỉ log response body khi response nhỏ.
- Không log response file.
- Không log dữ liệu nhạy cảm.
- Nếu lỗi thì vẫn phải restore response stream.

---

## 15. Middleware Logging vs Logger Consumer

Middleware Logging và Logger Consumer là hai phần khác nhau.


| Thành phần         | Chạy ở đâu                 | Nhiệm vụ                                             |
| ------------------ | -------------------------- | ---------------------------------------------------- |
| Middleware Logging | Trong từng backend service | Thu thập request/response/exception và publish Kafka |
| Logger Consumer    | Trong Logger Service       | Consume Kafka message và lưu PostgreSQL              |


Luồng đầy đủ:

```text
Order Service Middleware
  ↓ publish technical log
Kafka
  ↓ consume
Logger Service
  ↓ save
PostgreSQL
```

---

## 16. Week 1 Review Summary

### 16.1 Những tài liệu đã hoàn thành

Trong Tuần 1, các tài liệu thiết kế gồm:

```text
01-skysim-architecture.md
02-checkout-esim-flow.md
03-kafka-message-consumer-design.md
04-logger-database-api-design.md
05-middleware-logging-week1-review.md
```

### 16.2 Nội dung đã nắm

Các nội dung chính:

- Kiến trúc tổng thể Skysim.
- Vị trí KONG Gateway, Keycloak/Authen, Kafka, PostgreSQL, Redis.
- Luồng checkout eSIM.
- Phân biệt Guest Checkout và Authenticated Checkout.
- Vị trí Logger Service.
- Kafka message contract.
- Logger Consumer processing flow.
- PostgreSQL schema cho Logger.
- API tra cứu log.
- Middleware Logging dùng chung.

### 16.3 Thiết kế Logger tổng quan

```text
Backend Services
  ↓ publish action/technical log
Kafka topic
  ↓ consume
Logger Consumer
  ↓ validate + idempotency + save
PostgreSQL
  ↓ query
Logger API
  ↓
ReactJS Log Viewer
```

---

