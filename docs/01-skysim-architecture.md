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
```mermaid
sequenceDiagram
    autonumber
    actor User
    participant FE as Frontend<br/>(Web Portal)
    participant KONG as KONG Gateway
    participant AUTH as Keycloak
    participant ORD as Order Service
    participant PAY as Payment Service
    participant CORE as Core Service
    participant PROV as Provider Service
    participant EXT as External Provider
    participant KAFKA as Kafka<br/>(skysim.action.logs)
    participant NOTI as Notification Service
    participant LOG as Logger Service

    User->>FE: Bắt đầu checkout eSIM
    FE->>KONG: HTTP Request + JWT
    KONG->>AUTH: Validate JWT
    AUTH-->>KONG: OK / userId

    KONG->>ORD: Tạo đơn hàng
    ORD->>KAFKA: publish ORDER_CREATED

    KONG->>PAY: Request payment (orderId)
    PAY->>KAFKA: publish PAYMENT_REQUESTED
    PAY->>EXT: Gọi Payment Gateway
    EXT-->>PAY: Payment success callback
    PAY->>KAFKA: publish PAYMENT_SUCCESS

    KONG->>CORE: Tiếp tục fulfillment
    CORE->>PROV: Request eSIM activation
    PROV->>KAFKA: publish PROVIDER_REQUESTED
    PROV->>EXT: Provider API request eSIM
    EXT-->>PROV: eSIM profile / LDU
    PROV->>KAFKA: publish ESIM_ACTIVATED

    par Notification
        KAFKA->>NOTI: consume ESIM_ACTIVATED
        NOTI->>NOTI: Gửi email/SMS
        NOTI->>KAFKA: publish EMAIL_SENT
    and Logger
        KAFKA->>LOG: consume events
        LOG->>LOG: Persist to PostgreSQL
    end
```


Song song với nghiệp vụ chính, các service publish action log vào Kafka:

```text
Order Service          → ORDER_CREATED
Payment Service        → PAYMENT_REQUESTED / PAYMENT_SUCCESS
Core Service           → PROVIDER_REQUESTED / ESIM_ACTIVATED (eSIM)
                        → SIM_SERIAL_SUBMITTED / SIM_CONNECTION_REQUESTED (SIM)
Notification Service   → EMAIL_SENT
                      ↓
                    Kafka
                      ↓
                Logger Service
                      ↓
              PostgreSQL Logger DB
```

```mermaid
flowchart TB
    KAFKA[(Kafka Topic<br/>skysim.action.logs)]
    CONSUME[Consume Message]
    VALIDATE[Parse JSON<br/>Validate Required Fields]
    IDEMPOTENT[Check Idempotency<br/>by eventId]
    DBTX[Begin DB Transaction]
    SAVE1[Upsert log_flows]
    SAVE2[Insert log_actions]
    SAVE3[Insert log_action_details]
    COMMITDB[Commit DB Transaction]
    COMMITOFFSET[Commit Kafka Offset]

    KAFKA --> CONSUME
    CONSUME --> VALIDATE
    VALIDATE --> IDEMPOTENT
    IDEMPOTENT --> DBTX
    DBTX --> SAVE1
    SAVE1 --> SAVE2
    SAVE2 --> SAVE3
    SAVE3 --> COMMITDB
    COMMITDB --> COMMITOFFSET
```

```mermaid
flowchart TB
    REQ[HTTP Request] --> MIDDLEWARE[Logging Middleware]

    MIDDLEWARE --> MASK[Mask Sensitive Data]
    MASK --> NEXT[Call Next Middleware / Controller]

    NEXT --> RESPONSE[HTTP Response]
    NEXT --> EXCEPTION[Exception]

    RESPONSE --> LOG[Create Technical Log]
    EXCEPTION --> LOG

    LOG --> STORE[Store / Publish Log]
```

## 8. Ghi chú về phạm vi local development

Hiện tại chưa được cấp source code dự án thật, nên trong giai đoạn đầu em sẽ tự dựng một local workspace mô phỏng module Logger.

Local workspace gồm:

- Backend: .NET Web API cho Logger Service.
- Frontend: ReactJS app cho màn hình tra cứu log.
- Infrastructure: PostgreSQL, Kafka, Kafka UI chạy bằng Docker.
- Docs: tài liệu phân tích và thiết kế.

Mục tiêu của local workspace:

- Không phụ thuộc source thật.
- Chủ động chứng minh được flow Service → Kafka → Logger Consumer → PostgreSQL → API → ReactJS.
- Có nền tảng để implement Logger Module ở Tuần 2.

