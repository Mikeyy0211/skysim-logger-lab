# 04 - Logger Database & API Design

## 1. Mục tiêu

Tài liệu này mô tả thiết kế database và API tra cứu log cho module Logger trong project `skysim-logger-lab`.

Mục tiêu chính:

- Thiết kế schema PostgreSQL để lưu log theo flow và action.
- Đảm bảo hỗ trợ search, filter, pagination, sorting.
- Tách dữ liệu tổng quan và payload chi tiết để tối ưu truy vấn.
- Thiết kế API contract để Frontend ReactJS có thể sử dụng trực tiếp.
- Chuẩn bị nội dung review thiết kế Logger Module.

---

## 2. Nguyên tắc thiết kế

Logger không chỉ lưu text log đơn giản. Logger cần hỗ trợ truy vết một flow nghiệp vụ từ đầu đến cuối.

Với flow `CHECKOUT_ESIM`, một giao dịch có thể đi qua nhiều bước:

```text
ORDER_CREATED
PAYMENT_REQUESTED
PAYMENT_SUCCESS
PROVIDER_REQUESTED
ESIM_ACTIVATED
EMAIL_SENT
```

Vì vậy database cần tách thành 3 nhóm dữ liệu:

| Nhóm dữ liệu | Bảng | Mục đích |
|---|---|---|
| Flow summary | `log_flows` | Lưu thông tin tổng quan của một flow, dùng để search/filter nhanh |
| Flow timeline | `log_actions` | Lưu từng bước/action trong flow |
| Payload detail | `log_action_details` | Lưu request/response/error payload dạng JSONB |

Lý do tách bảng:

- API danh sách log không cần load payload nặng.
- Query search/filter nhanh hơn.
- Payload chi tiết chỉ được load khi user mở detail.
- Dễ mở rộng thêm flow khác ngoài checkout eSIM.

---

## 3. Database schema overview

```text
log_flows
   1 ──── n
        log_actions
              1 ──── 1
                   log_action_details
```

Quan hệ:

- Một `log_flow` có nhiều `log_actions`.
- Một `log_action` có tối đa một `log_action_details`.
- `flow_id` dùng để gom các action thuộc cùng một flow.
- `event_id` dùng để chống ghi trùng Kafka message.

---

## 4. Bảng `log_flows`

### 4.1 Mục đích

Bảng `log_flows` lưu thông tin tổng quan của một flow nghiệp vụ.

Ví dụ:

- Một lượt guest checkout eSIM.
- Một lượt authenticated checkout eSIM.
- Sau này có thể mở rộng cho refund, sync package, reconciliation.

Bảng này phục vụ API danh sách log và các filter chính.

### 4.2 Field đề xuất

| Field | Type | Required | Ý nghĩa |
|---|---:|---:|---|
| `id` | BIGSERIAL | Yes | Khóa chính nội bộ |
| `flow_id` | VARCHAR(100) | Yes | Mã flow dùng để trace |
| `flow_type` | VARCHAR(50) | Yes | Ví dụ: `CHECKOUT_ESIM` |
| `checkout_type` | VARCHAR(30) | No | `GUEST` hoặc `AUTHENTICATED` |
| `status` | VARCHAR(30) | Yes | `RUNNING`, `SUCCESS`, `FAILED`, `PARTIAL_FAILED` |
| `customer_email` | VARCHAR(255) | No | Email khách hàng |
| `customer_phone` | VARCHAR(50) | No | SĐT khách hàng |
| `user_id` | VARCHAR(100) | No | User ID nếu đã đăng nhập |
| `order_id` | VARCHAR(100) | No | Mã đơn hàng |
| `payment_id` | VARCHAR(100) | No | Mã thanh toán |
| `total_steps` | INT | No | Tổng số action đã nhận |
| `success_steps` | INT | No | Số action thành công |
| `failed_steps` | INT | No | Số action lỗi |
| `last_action_type` | VARCHAR(100) | No | Action gần nhất |
| `last_message` | TEXT | No | Message gần nhất |
| `started_at` | TIMESTAMP | No | Thời điểm bắt đầu flow |
| `completed_at` | TIMESTAMP | No | Thời điểm kết thúc flow |
| `created_at` | TIMESTAMP | Yes | Thời điểm tạo record |
| `updated_at` | TIMESTAMP | Yes | Thời điểm cập nhật cuối |

### 4.3 SQL DDL

```sql
CREATE TABLE log_flows (
    id BIGSERIAL PRIMARY KEY,
    flow_id VARCHAR(100) NOT NULL UNIQUE,
    flow_type VARCHAR(50) NOT NULL,
    checkout_type VARCHAR(30),
    status VARCHAR(30) NOT NULL,

    customer_email VARCHAR(255),
    customer_phone VARCHAR(50),
    user_id VARCHAR(100),

    order_id VARCHAR(100),
    payment_id VARCHAR(100),

    total_steps INT NOT NULL DEFAULT 0,
    success_steps INT NOT NULL DEFAULT 0,
    failed_steps INT NOT NULL DEFAULT 0,

    last_action_type VARCHAR(100),
    last_message TEXT,

    started_at TIMESTAMP,
    completed_at TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW()
);
```

---

## 5. Bảng `log_actions`

### 5.1 Mục đích

Bảng `log_actions` lưu từng bước xử lý trong một flow.

Ví dụ một flow checkout thành công sẽ có các action:

```text
1. ORDER_CREATED
2. PAYMENT_REQUESTED
3. PAYMENT_SUCCESS
4. PROVIDER_REQUESTED
5. ESIM_ACTIVATED
6. EMAIL_SENT
```

### 5.2 Field đề xuất

| Field | Type | Required | Ý nghĩa |
|---|---:|---:|---|
| `id` | BIGSERIAL | Yes | Khóa chính |
| `event_id` | VARCHAR(100) | Yes | Unique id của Kafka message |
| `flow_id` | VARCHAR(100) | Yes | Mã flow |
| `step_order` | INT | No | Thứ tự action trong flow |
| `service_name` | VARCHAR(100) | Yes | Service phát sinh action |
| `action_type` | VARCHAR(100) | Yes | Loại action |
| `status` | VARCHAR(30) | Yes | `SUCCESS`, `FAILED`, `PROCESSING` |
| `message` | TEXT | No | Message mô tả |
| `error_code` | VARCHAR(100) | No | Mã lỗi nếu có |
| `error_message` | TEXT | No | Nội dung lỗi nếu có |
| `created_at` | TIMESTAMP | Yes | Thời điểm tạo action |
| `updated_at` | TIMESTAMP | Yes | Thời điểm cập nhật action |

### 5.3 SQL DDL

```sql
CREATE TABLE log_actions (
    id BIGSERIAL PRIMARY KEY,
    event_id VARCHAR(100) NOT NULL UNIQUE,
    flow_id VARCHAR(100) NOT NULL,
    step_order INT,
    service_name VARCHAR(100) NOT NULL,
    action_type VARCHAR(100) NOT NULL,
    status VARCHAR(30) NOT NULL,

    message TEXT,
    error_code VARCHAR(100),
    error_message TEXT,

    created_at TIMESTAMP NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMP NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_log_actions_flow
        FOREIGN KEY (flow_id)
        REFERENCES log_flows(flow_id)
);
```

### 5.4 Idempotency rule

`event_id` phải unique.

Nếu Kafka Consumer nhận lại cùng một message do retry/rebalance, hệ thống kiểm tra `event_id`:

- Nếu `event_id` chưa tồn tại: insert action mới.
- Nếu `event_id` đã tồn tại: bỏ qua, không insert trùng.

---

## 6. Bảng `log_action_details`

### 6.1 Mục đích

Bảng `log_action_details` lưu payload chi tiết của action.

Payload có thể gồm:

- Request payload.
- Response payload.
- Error payload.
- Metadata.

Payload thường nặng, nên không để trực tiếp trong `log_actions`.

### 6.2 Field đề xuất

| Field | Type | Required | Ý nghĩa |
|---|---:|---:|---|
| `id` | BIGSERIAL | Yes | Khóa chính |
| `action_id` | BIGINT | Yes | FK đến `log_actions.id` |
| `request_payload` | JSONB | No | Dữ liệu request |
| `response_payload` | JSONB | No | Dữ liệu response |
| `error_payload` | JSONB | No | Dữ liệu lỗi |
| `metadata` | JSONB | No | Dữ liệu bổ sung |
| `created_at` | TIMESTAMP | Yes | Thời điểm tạo |

### 6.3 SQL DDL

```sql
CREATE TABLE log_action_details (
    id BIGSERIAL PRIMARY KEY,
    action_id BIGINT NOT NULL UNIQUE,
    request_payload JSONB,
    response_payload JSONB,
    error_payload JSONB,
    metadata JSONB,
    created_at TIMESTAMP NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_log_action_details_action
        FOREIGN KEY (action_id)
        REFERENCES log_actions(id)
);
```

---

## 7. Index design

### 7.1 Index cho `log_flows`

```sql
CREATE INDEX idx_log_flows_customer_email ON log_flows(customer_email);
CREATE INDEX idx_log_flows_customer_phone ON log_flows(customer_phone);
CREATE INDEX idx_log_flows_user_id ON log_flows(user_id);
CREATE INDEX idx_log_flows_order_id ON log_flows(order_id);
CREATE INDEX idx_log_flows_payment_id ON log_flows(payment_id);
CREATE INDEX idx_log_flows_flow_type ON log_flows(flow_type);
CREATE INDEX idx_log_flows_checkout_type ON log_flows(checkout_type);
CREATE INDEX idx_log_flows_status ON log_flows(status);
CREATE INDEX idx_log_flows_created_at ON log_flows(created_at);
```

### 7.2 Index cho `log_actions`

```sql
CREATE INDEX idx_log_actions_flow_id ON log_actions(flow_id);
CREATE INDEX idx_log_actions_service_name ON log_actions(service_name);
CREATE INDEX idx_log_actions_action_type ON log_actions(action_type);
CREATE INDEX idx_log_actions_status ON log_actions(status);
CREATE INDEX idx_log_actions_created_at ON log_actions(created_at);
```

### 7.3 Lý do index

Các field được index là các field thường dùng để:

- Search log theo khách hàng.
- Search log theo đơn hàng/thanh toán.
- Filter theo status.
- Filter theo thời gian.
- Xem timeline theo `flow_id`.

---

## 8. Status design

### 8.1 Flow status

| Status | Ý nghĩa |
|---|---|
| `RUNNING` | Flow đang xử lý |
| `SUCCESS` | Flow hoàn tất thành công |
| `FAILED` | Flow thất bại ở bước chính |
| `PARTIAL_FAILED` | Nghiệp vụ chính thành công nhưng bước phụ lỗi, ví dụ gửi email lỗi |

### 8.2 Action status

| Status | Ý nghĩa |
|---|---|
| `PROCESSING` | Action đang xử lý |
| `SUCCESS` | Action thành công |
| `FAILED` | Action thất bại |
| `SKIPPED` | Action bị bỏ qua nếu có rule nghiệp vụ |

---

## 9. Mapping Kafka message vào database

Kafka message mẫu:

```json
{
  "eventId": "evt-001",
  "flowId": "CHECKOUT-20260619-000001",
  "flowType": "CHECKOUT_ESIM",
  "checkoutType": "GUEST",
  "correlationId": "corr-abc-123",
  "serviceName": "OrderService",
  "actionType": "ORDER_CREATED",
  "status": "SUCCESS",
  "stepOrder": 1,
  "customerEmail": "customer@gmail.com",
  "customerPhone": "090xxxxxxx",
  "userId": null,
  "orderId": "ORD0001",
  "paymentId": null,
  "message": "Order created successfully",
  "requestPayload": {},
  "responsePayload": {},
  "errorPayload": null,
  "metadata": {},
  "createdAt": "2026-06-19T10:00:00Z"
}
```

Mapping:

| Kafka field | Database table | Database field |
|---|---|---|
| `flowId` | `log_flows` | `flow_id` |
| `flowType` | `log_flows` | `flow_type` |
| `checkoutType` | `log_flows` | `checkout_type` |
| `customerEmail` | `log_flows` | `customer_email` |
| `customerPhone` | `log_flows` | `customer_phone` |
| `userId` | `log_flows` | `user_id` |
| `orderId` | `log_flows` | `order_id` |
| `paymentId` | `log_flows` | `payment_id` |
| `actionType` | `log_flows` | `last_action_type` |
| `message` | `log_flows` | `last_message` |
| `eventId` | `log_actions` | `event_id` |
| `flowId` | `log_actions` | `flow_id` |
| `stepOrder` | `log_actions` | `step_order` |
| `serviceName` | `log_actions` | `service_name` |
| `actionType` | `log_actions` | `action_type` |
| `status` | `log_actions` | `status` |
| `message` | `log_actions` | `message` |
| `errorCode` | `log_actions` | `error_code` |
| `errorMessage` | `log_actions` | `error_message` |
| `requestPayload` | `log_action_details` | `request_payload` |
| `responsePayload` | `log_action_details` | `response_payload` |
| `errorPayload` | `log_action_details` | `error_payload` |
| `metadata` | `log_action_details` | `metadata` |

---

## 10. Flow cập nhật database khi consumer nhận message

```text
1. Consumer nhận Kafka message.
2. Parse JSON.
3. Validate required fields.
4. Check eventId đã tồn tại trong log_actions chưa.
5. Nếu eventId đã tồn tại: bỏ qua message.
6. Nếu eventId chưa tồn tại:
   6.1 Begin DB transaction.
   6.2 Upsert log_flows theo flow_id.
   6.3 Insert log_actions.
   6.4 Insert log_action_details nếu có payload.
   6.5 Update counter trong log_flows.
   6.6 Update last_action_type, last_message, updated_at.
   6.7 Commit DB transaction.
7. Commit Kafka offset sau khi DB transaction thành công.
```

---

## 11. Logger API design

Logger API cần phục vụ trực tiếp cho Frontend ReactJS.

API tối thiểu:

```text
GET /api/log-flows
GET /api/log-flows/{flowId}
GET /api/log-actions/{actionId}
```

---

## 12. API 1 - Search log flows

### 12.1 Endpoint

```http
GET /api/log-flows
```

### 12.2 Query parameters

| Param | Required | Ý nghĩa |
|---|---:|---|
| `customerEmail` | No | Tìm theo email |
| `customerPhone` | No | Tìm theo SĐT |
| `userId` | No | Tìm theo user ID |
| `orderId` | No | Tìm theo mã đơn |
| `paymentId` | No | Tìm theo mã thanh toán |
| `flowType` | No | Ví dụ `CHECKOUT_ESIM` |
| `checkoutType` | No | `GUEST` hoặc `AUTHENTICATED` |
| `status` | No | `RUNNING`, `SUCCESS`, `FAILED`, `PARTIAL_FAILED` |
| `fromDate` | No | Lọc từ ngày |
| `toDate` | No | Lọc đến ngày |
| `page` | No | Trang hiện tại, default 1 |
| `pageSize` | No | Số record/trang, default 20 |
| `sortBy` | No | Field sort, default `createdAt` |
| `sortDirection` | No | `asc` hoặc `desc`, default `desc` |

### 12.3 Request example

```http
GET /api/log-flows?customerEmail=guest@gmail.com&status=SUCCESS&page=1&pageSize=20&sortBy=createdAt&sortDirection=desc
```

### 12.4 Response example

```json
{
  "items": [
    {
      "flowId": "CHECKOUT-20260619-000001",
      "flowType": "CHECKOUT_ESIM",
      "checkoutType": "GUEST",
      "status": "SUCCESS",
      "customerEmail": "guest@gmail.com",
      "customerPhone": "090xxxxxxx",
      "userId": null,
      "orderId": "ORD0001",
      "paymentId": "PAY0001",
      "totalSteps": 6,
      "successSteps": 6,
      "failedSteps": 0,
      "lastActionType": "EMAIL_SENT",
      "lastMessage": "Email sent successfully",
      "createdAt": "2026-06-19T10:00:00Z",
      "updatedAt": "2026-06-19T10:03:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalItems": 1,
  "totalPages": 1
}
```

### 12.5 Notes

- API này không trả payload chi tiết.
- Chỉ trả summary đủ để hiển thị danh sách.
- Luôn dùng pagination.
- Cần giới hạn `pageSize` tối đa, ví dụ 100.

---

## 13. API 2 - Get flow detail

### 13.1 Endpoint

```http
GET /api/log-flows/{flowId}
```

### 13.2 Response example

```json
{
  "flowId": "CHECKOUT-20260619-000001",
  "flowType": "CHECKOUT_ESIM",
  "checkoutType": "GUEST",
  "status": "SUCCESS",
  "customerEmail": "guest@gmail.com",
  "customerPhone": "090xxxxxxx",
  "userId": null,
  "orderId": "ORD0001",
  "paymentId": "PAY0001",
  "totalSteps": 6,
  "successSteps": 6,
  "failedSteps": 0,
  "lastActionType": "EMAIL_SENT",
  "lastMessage": "Email sent successfully",
  "createdAt": "2026-06-19T10:00:00Z",
  "updatedAt": "2026-06-19T10:03:00Z",
  "actions": [
    {
      "actionId": 1,
      "eventId": "evt-001",
      "stepOrder": 1,
      "serviceName": "OrderService",
      "actionType": "ORDER_CREATED",
      "status": "SUCCESS",
      "message": "Order created successfully",
      "errorCode": null,
      "errorMessage": null,
      "createdAt": "2026-06-19T10:00:00Z"
    },
    {
      "actionId": 2,
      "eventId": "evt-002",
      "stepOrder": 2,
      "serviceName": "PaymentService",
      "actionType": "PAYMENT_REQUESTED",
      "status": "SUCCESS",
      "message": "Payment request created",
      "errorCode": null,
      "errorMessage": null,
      "createdAt": "2026-06-19T10:01:00Z"
    }
  ]
}
```

### 13.3 Notes

- API này trả timeline action của một flow.
- Actions nên sort theo `step_order`, sau đó `created_at`.
- Không nhất thiết trả payload nặng ở API này.
- Payload nên lấy qua API action detail.

---

## 14. API 3 - Get action detail

### 14.1 Endpoint

```http
GET /api/log-actions/{actionId}
```

### 14.2 Response example

```json
{
  "actionId": 1,
  "eventId": "evt-001",
  "flowId": "CHECKOUT-20260619-000001",
  "stepOrder": 1,
  "serviceName": "OrderService",
  "actionType": "ORDER_CREATED",
  "status": "SUCCESS",
  "message": "Order created successfully",
  "errorCode": null,
  "errorMessage": null,
  "createdAt": "2026-06-19T10:00:00Z",
  "requestPayload": {
    "packageId": "PKG001",
    "customerEmail": "guest@gmail.com"
  },
  "responsePayload": {
    "orderId": "ORD0001",
    "status": "CREATED"
  },
  "errorPayload": null,
  "metadata": {
    "correlationId": "corr-abc-123"
  }
}
```

### 14.3 Notes

- API này mới load payload chi tiết.
- Cần mask sensitive data trước khi lưu hoặc trước khi trả ra API.

---

## 15. Validation rules cho API

### 15.1 Pagination

- `page` default = 1.
- `pageSize` default = 20.
- `pageSize` max = 100.
- Nếu `page < 1`, trả validation error hoặc set về 1.

### 15.2 Sorting

Chỉ cho sort theo các field whitelist:

```text
createdAt
updatedAt
status
flowType
checkoutType
orderId
paymentId
```

Không cho client truyền sort field tùy ý để tránh lỗi query hoặc risk injection.

### 15.3 Date range

- Nếu có `fromDate`, filter `created_at >= fromDate`.
- Nếu có `toDate`, filter `created_at <= toDate`.
- Nếu `fromDate > toDate`, trả validation error.

---

## 16. Security và data masking

Không lưu hoặc không trả raw sensitive data.

Các field cần mask:

```text
password
access_token
refresh_token
authorization
otp
cardNumber
cvv
paymentSecret
secret
token
```

Ví dụ:

```json
{
  "authorization": "***MASKED***",
  "password": "***MASKED***"
}
```

---

## 19. Definition of Done

Hoàn thành phần thiết kế database và API khi có đủ:

- Thiết kế bảng `log_flows`.
- Thiết kế bảng `log_actions`.
- Thiết kế bảng `log_action_details`.
- Có SQL DDL bản nháp.
- Có index design.
- Có mapping Kafka message vào DB.
- Có flow xử lý database khi consumer nhận message.
- Có API danh sách log.
- Có API chi tiết flow.
- Có API chi tiết action.
- Có pagination, sorting, filtering rule.
- Có data masking rule.

