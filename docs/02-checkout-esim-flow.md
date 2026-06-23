# 02 - Checkout eSIM Flow & Logger Position

## 1. Mục tiêu

Tài liệu này phân tích flow checkout eSIM trong hệ thống Skysim và xác định vị trí của **Logger Service** trong flow nghiệp vụ.

Mục tiêu chính:

- Hiểu các bước chính trong quá trình mua eSIM.
- Phân biệt **Guest Checkout** và **Authenticated Checkout**.
- Xác định các service tham gia flow.
- Xác định các action cần ghi log.
- Xác định Logger Service nhận log từ đâu và lưu vào đâu.
- Làm rõ vì sao Logger nên nhận log qua Kafka thay vì các service gọi trực tiếp Logger API.

---

## 2. Flow checkout eSIM tổng quát

Luồng nghiệp vụ chính:

```text
Customer
  ↓
Frontend Checkout Page
  ↓
KONG Gateway
  ↓
Order Service tạo đơn hàng
  ↓
Payment Service xử lý thanh toán
  ↓
Core / Provider Service kích hoạt eSIM
  ↓
Notification Service gửi email/thông báo
  ↓
Customer nhận thông tin eSIM
```

Giải thích vai trò từng service:

| Service | Vai trò |
|---|---|
| Frontend | Hiển thị checkout page và gửi request |
| KONG Gateway | Routing, authentication, rate limit, logging |
| Order Service | Tạo và quản lý đơn hàng |
| Payment Service | Xử lý yêu cầu thanh toán |
| Core Service | Xử lý nghiệp vụ eSIM chính |
| Provider Service | Giao tiếp với nhà cung cấp eSIM |
| Notification Service | Gửi email/thông báo cho khách hàng |
| Logger Service | Lưu log tập trung phục vụ truy vết và debug |

---

## 3. Guest Checkout

**Guest Checkout** là trường hợp khách hàng mua eSIM nhưng không đăng nhập.

Đặc điểm:

- Không có JWT.
- Không có `userId`.
- Có thể có `customerEmail` và `customerPhone`.
- Sau khi tạo đơn sẽ có `orderId`.
- Sau khi thanh toán sẽ có `paymentId`.

Dữ liệu định danh chính:

```json
{
  "flowType": "CHECKOUT_ESIM",
  "checkoutType": "GUEST",
  "userId": null,
  "customerEmail": "guest@gmail.com",
  "customerPhone": "090xxxxxxx",
  "orderId": "ORD001",
  "paymentId": "PAY001"
}
```

Ghi chú:

- Với guest checkout, không thể truy vết bằng `userId`.
- Vì vậy Logger cần hỗ trợ tìm kiếm bằng email, phone, orderId hoặc paymentId.
- `userId` phải cho phép nullable.

---

## 4. Authenticated Checkout

**Authenticated Checkout** là trường hợp khách hàng đã đăng nhập trước khi mua eSIM.

Đặc điểm:

- Có JWT.
- Có `userId` lấy từ token.
- Có `customerEmail` và `customerPhone`.
- Có `orderId` sau khi tạo đơn.
- Có `paymentId` sau khi thanh toán.

Dữ liệu định danh chính:

```json
{
  "flowType": "CHECKOUT_ESIM",
  "checkoutType": "AUTHENTICATED",
  "userId": "USER001",
  "customerEmail": "user@gmail.com",
  "customerPhone": "090xxxxxxx",
  "orderId": "ORD002",
  "paymentId": "PAY002"
}
```

Ghi chú:

- Dù đã có `userId`, vẫn nên lưu thêm email, phone, orderId và paymentId.
- Lý do: đội vận hành/support thường tra cứu nhanh theo email, số điện thoại hoặc mã đơn hàng.

---

## 5. Cách thiết kế flow

Không nên tách **Guest Checkout** và **Authenticated Checkout** thành hai flow khác nhau.

Đề xuất:

- Dùng chung `flowType = CHECKOUT_ESIM`.
- Phân biệt bằng `checkoutType = GUEST` hoặc `AUTHENTICATED`.
- Với Guest Checkout, `userId = null`.
- Với Authenticated Checkout, `userId` lấy từ JWT.
- Luôn lưu thêm email, phone, orderId, paymentId để hỗ trợ tra cứu vận hành.

Lý do:

- Cùng là nghiệp vụ mua eSIM.
- Logger dễ mở rộng và dễ filter.
- Frontend tra cứu log dễ hiển thị.
- Support có thể tìm theo email, phone, orderId hoặc paymentId.
- Sau này có thể mở rộng thêm các flow khác như refund, renew package, sync provider mà không làm thay đổi cấu trúc Logger.

---

## 6. Danh sách action cần log

Các action chính trong flow thành công:

| Step | Service | Action Type | Status |
|---:|---|---|---|
| 1 | Order Service | `ORDER_CREATED` | `SUCCESS` |
| 2 | Payment Service | `PAYMENT_REQUESTED` | `SUCCESS` |
| 3 | Payment Service | `PAYMENT_SUCCESS` | `SUCCESS` |
| 4 | Core / Provider Service | `PROVIDER_REQUESTED` | `SUCCESS` |
| 5 | Core / Provider Service | `ESIM_ACTIVATED` | `SUCCESS` |
| 6 | Notification Service | `EMAIL_SENT` | `SUCCESS` |

Các action lỗi có thể phát sinh:

| Service | Action Type | Ý nghĩa |
|---|---|---|
| Order Service | `ORDER_FAILED` | Tạo đơn thất bại |
| Payment Service | `PAYMENT_FAILED` | Thanh toán thất bại |
| Provider Service | `PROVIDER_FAILED` | Gọi provider thất bại |
| Core / Provider Service | `ESIM_ACTIVATION_FAILED` | Kích hoạt eSIM thất bại |
| Notification Service | `EMAIL_FAILED` | Gửi email/thông báo thất bại |

---

## 7. Mapping service với action log

| Service | Action publish lên Kafka |
|---|---|
| Order Service | `ORDER_CREATED`, `ORDER_FAILED` |
| Payment Service | `PAYMENT_REQUESTED`, `PAYMENT_SUCCESS`, `PAYMENT_FAILED` |
| Core Service / Provider Service | `PROVIDER_REQUESTED`, `ESIM_ACTIVATED`, `PROVIDER_FAILED`, `ESIM_ACTIVATION_FAILED` |
| Notification Service | `EMAIL_SENT`, `EMAIL_FAILED` |
| Logger Service | Consume toàn bộ action từ Kafka và lưu vào PostgreSQL |

---

## 8. Vị trí Logger trong flow

Các service nghiệp vụ không ghi trực tiếp vào database Logger. Thay vào đó, các service publish action log vào Kafka.

Luồng log:

```text
Order / Payment / Core / Provider / Notification
  ↓ publish action log
Kafka topic: skysim.action.logs
  ↓ consume
Logger Service
  ↓ save
PostgreSQL Logger DB
  ↓ query
Logger API
  ↓
ReactJS Log Viewer
```

Cách hiểu:

- Flow nghiệp vụ chính vẫn chạy qua Order, Payment, Core/Provider, Notification.
- Flow log chạy song song, bất đồng bộ qua Kafka.
- Logger không nên block checkout chính.
- Nếu Logger hoặc DB log tạm lỗi, nghiệp vụ checkout không nên bị dừng hoàn toàn.

---

## 9. Vì sao dùng Kafka cho Logger

Logger nên nhận log thông qua Kafka thay vì để service gọi trực tiếp Logger API.

Lý do:

- Giảm phụ thuộc trực tiếp giữa service nghiệp vụ và Logger.
- Tránh làm chậm flow checkout chính.
- Hỗ trợ xử lý bất đồng bộ.
- Hỗ trợ retry khi Logger hoặc database tạm thời lỗi.
- Dễ scale Logger Consumer khi lượng log tăng.
- Phù hợp với event-driven architecture.
- Có thể mở rộng thêm consumer khác nếu sau này cần analytics, monitoring hoặc alerting.

---

## 10. Dữ liệu Logger cần lưu ở cấp flow

Bảng flow dùng để tra cứu nhanh.

Các field đề xuất:

| Field | Ý nghĩa |
|---|---|
| `flowId` | ID định danh một flow checkout |
| `flowType` | Loại flow, ví dụ `CHECKOUT_ESIM` |
| `checkoutType` | `GUEST` hoặc `AUTHENTICATED` |
| `status` | Trạng thái flow: `RUNNING`, `SUCCESS`, `FAILED`, `PARTIAL_FAILED` |
| `customerEmail` | Email khách hàng |
| `customerPhone` | Số điện thoại khách hàng |
| `userId` | ID user, nullable với guest |
| `orderId` | Mã đơn hàng |
| `paymentId` | Mã giao dịch thanh toán |
| `totalSteps` | Tổng số bước đã ghi nhận |
| `successSteps` | Số bước thành công |
| `failedSteps` | Số bước thất bại |
| `lastActionType` | Action gần nhất |
| `lastMessage` | Message gần nhất |
| `startedAt` | Thời điểm bắt đầu flow |
| `completedAt` | Thời điểm kết thúc flow |
| `createdAt` | Thời điểm tạo bản ghi |
| `updatedAt` | Thời điểm cập nhật gần nhất |

---

## 11. Dữ liệu Logger cần lưu ở cấp action

Bảng action dùng để xem timeline từng bước.

Các field đề xuất:

| Field | Ý nghĩa |
|---|---|
| `eventId` | ID duy nhất của event, dùng cho idempotency |
| `flowId` | ID flow mà action thuộc về |
| `stepOrder` | Thứ tự bước trong flow |
| `serviceName` | Service phát sinh action |
| `actionType` | Loại action, ví dụ `ORDER_CREATED` |
| `status` | Trạng thái action: `SUCCESS`, `FAILED`, `PENDING` |
| `message` | Mô tả ngắn |
| `errorCode` | Mã lỗi nếu có |
| `errorMessage` | Nội dung lỗi nếu có |
| `createdAt` | Thời điểm action xảy ra |
| `updatedAt` | Thời điểm cập nhật gần nhất |

---

## 12. Dữ liệu chi tiết của action

Payload chi tiết nên tách riêng để tránh làm bảng action bị nặng.

Các field đề xuất:

| Field | Ý nghĩa |
|---|---|
| `actionId` | Khóa liên kết tới action |
| `requestPayload` | Dữ liệu request |
| `responsePayload` | Dữ liệu response |
| `errorPayload` | Dữ liệu lỗi |
| `metadata` | Thông tin mở rộng |
| `createdAt` | Thời điểm tạo bản ghi |

Lý do tách payload riêng:

- Payload có thể rất lớn.
- API danh sách log không cần load payload chi tiết.
- Query danh sách sẽ nhẹ hơn.
- Chỉ load payload khi người dùng mở màn hình chi tiết action.

---

## 13. Các trạng thái flow đề xuất

| Status | Ý nghĩa |
|---|---|
| `RUNNING` | Flow đang xử lý |
| `SUCCESS` | Flow hoàn tất thành công |
| `FAILED` | Flow thất bại ở bước nghiệp vụ quan trọng |
| `PARTIAL_FAILED` | Nghiệp vụ chính thành công nhưng bước phụ bị lỗi, ví dụ gửi email thất bại |

Ví dụ:

- `PAYMENT_FAILED` → flow có thể là `FAILED`.
- `ESIM_ACTIVATION_FAILED` → flow là `FAILED`.
- `EMAIL_FAILED` sau khi eSIM đã activated → flow có thể là `PARTIAL_FAILED`.

---

## 14. flowId, correlationId, orderId khác nhau thế nào

| Field | Ý nghĩa | Ví dụ |
|---|---|---|
| `flowId` | ID truy vết toàn bộ flow nghiệp vụ | `CHECKOUT-20260619-000001` |
| `correlationId` | ID trace kỹ thuật qua request/service | `corr-abc-123` |
| `orderId` | ID đơn hàng nghiệp vụ | `ORD001` |

Cách hiểu:

- `flowId` dùng cho Logger để gom các action trong cùng một flow.
- `correlationId` dùng để trace request kỹ thuật.
- `orderId` là mã đơn hàng thật trong nghiệp vụ bán hàng.

Không nên chỉ dùng `orderId` làm `flowId` vì ở giai đoạn đầu checkout có thể chưa tạo orderId. Ngoài ra sau này Logger có thể cần trace các flow khác không liên quan đến order.

---

## 15. Kết luận

Flow checkout eSIM nên được trace bằng một `flowId` duy nhất. Cả Guest Checkout và Authenticated Checkout dùng chung `flowType = CHECKOUT_ESIM`, phân biệt bằng `checkoutType`.

Logger Service nhận action log từ Kafka, sau đó lưu vào PostgreSQL. Thiết kế này giúp hệ thống dễ truy vết, dễ debug, giảm phụ thuộc giữa các service và không làm chậm flow nghiệp vụ chính.

Output của Thứ 3:

- File `docs/02-checkout-esim-flow.md`.
- Flow checkout eSIM tổng quát.
- Phân biệt Guest Checkout và Authenticated Checkout.
- Danh sách action cần log.
- Mapping service với action.
- Vị trí Logger trong flow.
- Open questions để confirm với mentor.
