# 01 - Skysim Architecture Overview

## 1. Mục tiêu

Tài liệu này dùng để ghi chú lại kiến trúc tổng thể hệ thống Skysim trong giai đoạn Tuần 1 - Backend Foundation.

Mục tiêu chính:

- Hiểu các thành phần chính trong hệ thống Skysim.
- Hiểu request đi từ Frontend đến Backend như thế nào.
- Hiểu vai trò của API Gateway, Authentication, Backend Services, Kafka, Database, Redis.
- Xác định vị trí và vai trò của Logger Service trong kiến trúc tổng thể.

# 2. Thành phần chính của hệ thống Skysim

### 2.1 Frontend Layer

Bao gồm:

- Website B2C: phục vụ khách hàng cuối
- Web Portal B2B: phục vụ đại lý/đối tác
- CMS: phục vụ quản trị và chăm sóc khách hàng

Vai trò:

- Hiển thị dữ liệu cho người dùng
- Gửi request đến backend thông qua KONG Gateway
- Không gọi trực tiếp vào từng backend service

### 2.2 Gateway Layer

Thành phần chính:

- Cloudflare/Nginx
- KONG Gateway

Vai trò của KONG:

- Routing request đến đúng backend service
- Authentication/Authorization
- Rate limiting
- Logging/Monitoring
- Load balencing
- SSL termination
- API versioning

### 2.3 Authentication Layer

Thành phần:

- Keycloak/Authen

Vai trò:

- Quản lí đăng nhập
- Cấp và xác thực JWT
- Quản lí user, role, permission
- Hỗ trợ phân quyền truy cập API

### 2.4 Backend Microservices

Các service chính:

- Customer Service: xử lí nghiệp vụ khách hàng B2C
- Partner Service: xử lí nghiệp vụ đối tác B2B
- Core Service: xử lý đơn hàng, sản phẩm, nghiệp vụ eSIM chính
- Provider Service: giao tiếp với nhà cung cấp eSIM
- Payment Service: xử lí. thanh toán
- Notification Service: gửi email/thông báo.
- Commission Service: xử lý hoa hồng.
- Loyalty Service: xử lý khách hàng thân thiết.
- Logger Service: lưu log tập trung toàn hệ thống.

### 2.5 Infrastructure/Data Layer

Bao gồm:

- PostgreSQL/Oracle: lưu dữ liệu nghiệp vụ và log.
- Kafka: message broker/event streaming giữa các service.
- Redis: cache dữ liệu.
- Prometheus/Grafana: monitoring hệ thống.

## 3. Luồng xử lý request tổng quát

Luồng request cơ bản:


User
  ↓
Frontend Website / Portal / CMS
  ↓
KONG Gateway
  ↓
Keycloak/Authen kiểm tra JWT nếu API cần xác thực
  ↓
Backend Service tương ứng
  ↓
Database / Kafka / External Provider
  ↓
Response trả ngược lại Frontend

Giải thích:
1. Người dùng thao tác trên Website, Portal hoặc CMS
2. FE gửi request đến KONG Gateway
3. Kong kiểm tra route, auth, rate limit và chuyển request
4. Nếu API yêu cầu đăng nhập, token được xác thực thông qua Keycloak/Authen
5. BE service xử lí nghiệp vụ
6. Service có thể gọi database, gọi service khác qua REST API hoặc publish event qua Kafka
7. Response được trả về FE

## 4. REST API và Kafka trong hệ thống

### 4.1 REST API

REST API dùng cho các tác vụ cần phản hồi ngay.

Ví dụ:
- Frontend lấy danh sách package.
- Frontend tạo đơn hàng.
- Service kiểm tra trạng thái eSIM.
- Service lấy thông tin user.

Đặc điểm:
- Giao tiếp đồng bộ.
- Bên gọi thường chờ response.
- Phù hợp với nghiệp vụ realtime.

### 4.2 Kafka

Kafka dùng cho các tác vụ bất đồng bộ.

Ví dụ:
- Publish event OrderCreated.
- Publish event PaymentSuccess.
- Publish event EsimActivated.
- Gửi notification.
- Ghi audit log.
- Retry processing.

Đặc điểm:
- Giao tiếp bất đồng bộ.
- Giảm phụ thuộc trực tiếp giữa các service.
- Phù hợp với event-driven architecture.
- Có thể retry và scale consumer.


## 5. Vai trò Logger Service

Logger Service là service lưu log tập trung toàn hệ thống.

Mục tiêu:
- Thu thập log từ nhiều backend service.
- Chuẩn hóa format log.
- Lưu log vào database.
- Hỗ trợ tra cứu log theo thời gian, service, user, trạng thái.
- Hỗ trợ debug, đối soát, monitoring và điều tra sự cố.

Trong flow checkout eSIM, Logger giúp vận hành biết được:
- Đơn hàng đã được tạo chưa.
- Thanh toán đã thành công chưa.
- Provider đã được gọi chưa.
- eSIM đã kích hoạt chưa.
- Email đã gửi cho khách chưa.
- Nếu lỗi thì lỗi xảy ra ở service nào, action nào.


## 6. Sơ đồ kiến trúc tổng quan bản nháp


```mermaid
flowchart TD

    %% ================= CLIENT =================
    subgraph CLIENT["Client Channels"]
        U[User]
        FE[Website / Web Portal / CMS]
        U --> FE
    end

    %% ================= INTERNAL =================
    subgraph INTERNAL["Internal - Skysim System"]
        KONG[KONG Gateway]
        AUTH[Keycloak / Authentication]
        KONG <-->|JWT Validation| AUTH

        FE -->|HTTP Request| KONG
        KONG --> CUSTOMER[Customer Service]
        KONG --> PARTNER[Partner Service]
        KONG --> CORE[Core Service]
        KONG --> PROVIDER[Provider Service]
        KONG --> PAYMENT[Payment Service]
        KONG --> NOTI[Notification Service]

        CUSTOMER --> DB[(Main Database)]
        PARTNER --> DB
        CORE --> DB
        PROVIDER --> DB
        PAYMENT --> DB

        CUSTOMER & PARTNER & CORE & PROVIDER & PAYMENT & NOTI -.-> REDIS[(Redis Cache)]
        CUSTOMER & PARTNER & CORE & PROVIDER & PAYMENT & NOTI --> KAFKA[(Kafka)]

        KAFKA --> LOGGER[Logger Service]
        LOGGER --> LOGDB[(PostgreSQL Logger DB)]
    end

    %% ================= EXTERNAL =================
    subgraph EXTERNAL["External Systems"]
        EXT[External Provider]
        PAYGW[Payment Gateway]
    end

    PROVIDER -->|Provider API| EXT
    PAYMENT -->|Payment API| PAYGW
```


## 7. Luồng checkout eSIM bản nháp

```text
Customer
  ↓
Frontend Checkout Page
  ↓
KONG Gateway
  ↓
Keycloak xác thực JWT
  ↓
Order Service tạo đơn hàng
  ↓
Payment Service xử lý thanh toán
  ↓
Core Service kích hoạt eSIM (gọi Provider Service)
  ↓
Notification Service gửi email/thông báo
  ↓
Customer nhận thông tin eSIM
```

---

### 7.1 Technical HTTP Logging + Flow Propagation

**Ý chính:** LoggerMiddleware tự động capture mọi HTTP request/response và publish lên Kafka ngay lập tức. FlowId được forward qua các service.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant FE as Frontend / Caller
    participant KONG as KONG
    participant PARTNER as Partner Service
    participant PAY as Payment Service
    participant PROV as Provider Service
    participant KAFKA as Kafka
    participant LOG as Logger Consumer
    participant DB as PostgreSQL

    User->>FE: Checkout eSIM
    FE->>KONG: Request + JWT<br/>X-Flow-Id=flow-123

    KONG->>PARTNER: /order/create<br/>X-Flow-Id=flow-123
    PARTNER-->>KAFKA: HTTP_REQUEST<br/>partner-service, flow-123
    KAFKA-->>LOG: consume immediately
    LOG->>DB: upsert flow + insert action


    PARTNER->>PAY: /payment/check<br/>X-Flow-Id=flow-123
    PAY-->>KAFKA: HTTP_REQUEST<br/>payment-service, flow-123
    KAFKA-->>LOG: consume immediately
    LOG->>DB: append action

    PAY->>PROV: /provider/esim<br/>X-Flow-Id=flow-123
    PROV-->>KAFKA: HTTP_REQUEST<br/>provider-service, flow-123
    KAFKA-->>LOG: consume immediately
    LOG->>DB: append action

    PROV-->>PAY: result
    PAY-->>PARTNER: result
    PARTNER-->>KONG: response + X-Flow-Id
    KONG-->>FE: response + X-Flow-Id
```

**Rule quan trọng:**

- Nếu request đã có `X-Flow-Id`, `LoggerMiddleware` dùng lại flowId đó.
- Nếu request thiếu `X-Flow-Id`, `LoggerMiddleware` tạo flowId mới.
- `FlowContextForwardingHandler` chỉ có tác dụng khi service gọi downstream bằng đúng `HttpClient` đã gắn handler.
- Nếu một endpoint được gọi từ caller/path khác, caller đó cũng phải truyền `X-Flow-Id`, nếu không service sẽ sinh flowId riêng.

**Đặc điểm:**

- `actionType` = `HTTP_REQUEST`
- Payload: request/response (masked), duration, status, userId
- Dùng cho: debug latency, trace request path, performance monitoring

---

### 7.2 Business Event Logging + Incremental Persist

**Ý chính:** Business code publish events tại mỗi milestone. Logger Consumer consume ngay và append incremental vào cùng flow.

```mermaid
sequenceDiagram
    autonumber
    actor User
    participant FE as Frontend
    participant KONG as KONG
    participant PARTNER as Partner Service
    participant PAY as Payment Service
    participant PROV as Provider Service
    participant KAFKA as Kafka
    participant LOG as Logger Consumer
    participant DB as PostgreSQL

    User->>FE: Checkout eSIM
    FE->>KONG: Request + JWT

    KONG->>PARTNER: Create Order
    PARTNER->>KAFKA: ORDER_CREATED<br/>flow-123, SUCCESS
    KAFKA-->>LOG: consume
    LOG->>DB: upsert flow + insert action(step=1)

    PARTNER->>PAY: Process Payment
    PAY->>KAFKA: PAYMENT_SUCCESS<br/>flow-123, SUCCESS
    KAFKA-->>LOG: consume
    LOG->>DB: append action(step=2)

    PAY->>PROV: Activate eSIM
    PROV->>KAFKA: ESIM_ACTIVATED<br/>flow-123, SUCCESS
    KAFKA-->>LOG: consume
    LOG->>DB: append action(step=3)

    Note over DB: Flow completed:<br/>totalSteps=3, successSteps=3<br/>status=SUCCESS
```

**Đặc điểm:**

- `actionType` = business event name: `ORDER_CREATED`, `PAYMENT_SUCCESS`, `ESIM_ACTIVATED`, ...
- Payload: request/response, status, error details (nếu failed)
- Dùng cho: business tracking, audit trail, customer support lookup

**Upsert log_flows:**

| Event status | Action |
|--------------|--------|
| SUCCESS | `successSteps++` |
| FAILED | `failedSteps++`, status → FAILED |
| Final | `completedAt`, final status |

**Không phải final step:** Logger Consumer chạy song song, không phải đợi tất cả service xong mới log.