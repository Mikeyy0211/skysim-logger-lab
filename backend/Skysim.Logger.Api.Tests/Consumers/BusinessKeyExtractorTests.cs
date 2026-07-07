using FluentAssertions;
using Skysim.Logger.Api.Consumers;
using Skysim.Logger.Contracts.Events;
using Skysim.Logger.Infrastructure.Entities;
using Xunit;

namespace Skysim.Logger.Api.Tests.Consumers;

public class BusinessKeyExtractorTests
{
    [Fact]
    public void ExtractOrderCode_FromNestedPayment_BillOrder_ReturnsValue()
    {
        const string payload = """
        {
          "errorCode": 0,
          "message": "Create payment success",
          "data": {
            "payment": {
              "transactionId": 1640,
              "fee": 0,
              "billOrder": "HPCLLLCEFY74842552",
              "transPaymentId": "ZQOX3QPYQ764615011",
              "transactionChannelId": "WAITING_ONEPAY"
            }
          }
        }
        """;

        var result = BusinessKeyExtractor.ExtractOrderCode(payload);

        result.Should().Be("HPCLLLCEFY74842552");
    }

    [Fact]
    public void ExtractPaymentId_FromNestedPayment_TransPaymentId_ReturnsValue()
    {
        const string payload = """
        {
          "data": {
            "payment": {
              "billOrder": "HPCLLLCEFY74842552",
              "transPaymentId": "ZQOX3QPYQ764615011",
              "transactionId": 1640
            }
          }
        }
        """;

        var result = BusinessKeyExtractor.ExtractPaymentId(payload);

        result.Should().Be("ZQOX3QPYQ764615011");
    }

    [Fact]
    public void ExtractTransactionId_FromNestedPayment_NumberField_ReturnsStringValue()
    {
        const string payload = """
        {
          "data": {
            "payment": {
              "transactionId": 1640,
              "billOrder": "HPCLLLCEFY74842552"
            }
          }
        }
        """;

        var result = BusinessKeyExtractor.ExtractTransactionId(payload);

        result.Should().Be("1640");
    }

    [Fact]
    public void ExtractTransactionId_FallsBackToTransId_WhenTransactionIdMissing()
    {
        const string payload = """
        {
          "data": {
            "payment": {
              "transId": "TX-FALLBACK-001",
              "billOrder": "ABC"
            }
          }
        }
        """;

        var result = BusinessKeyExtractor.ExtractTransactionId(payload);

        result.Should().Be("TX-FALLBACK-001");
    }

    [Fact]
    public void ExtractOrderCode_PrefersOrderCode_OverBillOrder()
    {
        const string payload = """
        {
          "data": {
            "payment": {
              "orderCode": "PRIMARY-CODE",
              "billOrder": "BILL-CODE"
            }
          }
        }
        """;

        var result = BusinessKeyExtractor.ExtractOrderCode(payload);

        result.Should().Be("PRIMARY-CODE");
    }

    [Fact]
    public void ExtractPaymentId_FallsBackToPaymentId_OverTransPaymentId()
    {
        const string payload = """
        {
          "data": {
            "payment": {
              "paymentId": "PAY-PRIMARY",
              "transPaymentId": "ZQOX3QPYQ764615011"
            }
          }
        }
        """;

        var result = BusinessKeyExtractor.ExtractPaymentId(payload);

        result.Should().Be("PAY-PRIMARY");
    }

    [Fact]
    public void Extract_AllKeys_FromWrapperWithJsonBodyString()
    {
        const string payload = """
        {
          "body": "{\"errorCode\":0,\"message\":\"Create payment success\",\"data\":{\"payment\":{\"transactionId\":1640,\"billOrder\":\"HPCLLLCEFY74842552\",\"transPaymentId\":\"ZQOX3QPYQ764615011\",\"transId\":\"TX-001\"}}}",
          "path": "/apis/partner/order/create",
          "headers": { "content-type": "application/json" }
        }
        """;

        BusinessKeyExtractor.ExtractOrderCode(payload).Should().Be("HPCLLLCEFY74842552");
        BusinessKeyExtractor.ExtractPaymentId(payload).Should().Be("ZQOX3QPYQ764615011");
        BusinessKeyExtractor.ExtractTransactionId(payload).Should().Be("1640");
    }

    [Fact]
    public void Extract_WhenBodyIsNotJson_DoesNotThrow_AndSearchesSiblings()
    {
        const string payload = """
        {
          "body": "this is not json",
          "sibling": {
            "transId": "TX-SIBLING-999"
          }
        }
        """;

        var act = () => BusinessKeyExtractor.ExtractTransactionId(payload);

        act.Should().NotThrow();
        BusinessKeyExtractor.ExtractTransactionId(payload).Should().Be("TX-SIBLING-999");
    }

    [Fact]
    public void Extract_WhenPayloadIsNull_ReturnsNull()
    {
        BusinessKeyExtractor.ExtractOrderCode(null).Should().BeNull();
        BusinessKeyExtractor.ExtractPaymentId(null).Should().BeNull();
        BusinessKeyExtractor.ExtractTransactionId(null).Should().BeNull();
    }

    [Fact]
    public void Extract_WhenPayloadIsEmpty_ReturnsNull()
    {
        BusinessKeyExtractor.ExtractOrderCode("").Should().BeNull();
        BusinessKeyExtractor.ExtractOrderCode("   ").Should().BeNull();
    }

    [Fact]
    public void Extract_WhenPayloadIsNotJson_ReturnsNull()
    {
        const string payload = "not a json payload at all";

        BusinessKeyExtractor.ExtractOrderCode(payload).Should().BeNull();
        BusinessKeyExtractor.ExtractPaymentId(payload).Should().BeNull();
        BusinessKeyExtractor.ExtractTransactionId(payload).Should().BeNull();
    }

    [Fact]
    public void Extract_WhenNoMatchingKey_ReturnsNull()
    {
        const string payload = """{ "data": { "unrelated": "value" } }""";

        BusinessKeyExtractor.ExtractOrderCode(payload).Should().BeNull();
        BusinessKeyExtractor.ExtractPaymentId(payload).Should().BeNull();
        BusinessKeyExtractor.ExtractTransactionId(payload).Should().BeNull();
    }

    [Fact]
    public void Extract_DeepNested_FindsValue()
    {
        const string payload = """
        {
          "level1": {
            "level2": {
              "level3": {
                "transId": "DEEP-VALUE"
              }
            }
          }
        }
        """;

        BusinessKeyExtractor.ExtractTransactionId(payload).Should().Be("DEEP-VALUE");
    }
}

public class EnrichBusinessKeysTests
{
    [Fact]
    public void EnrichBusinessKeys_FillsMissingFieldsFromResponseBody()
    {
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-1",
            FlowType = "HTTP_ACTION",
            ServiceName = "test",
            ActionType = "ORDER_CREATED",
            Status = "SUCCESS",
            CreatedAt = DateTime.UtcNow,
            ResponseBody = """
            {
              "data": {
                "payment": {
                  "transactionId": 1640,
                  "billOrder": "HPCLLLCEFY74842552",
                  "transPaymentId": "ZQOX3QPYQ764615011"
                }
              }
            }
            """
        };

        EnrichBusinessKeys(message);

        message.OrderCode.Should().Be("HPCLLLCEFY74842552");
        message.PaymentId.Should().Be("ZQOX3QPYQ764615011");
        message.TransactionId.Should().Be("1640");
    }

    [Fact]
    public void EnrichBusinessKeys_DoesNotOverwriteExistingNonEmptyValues()
    {
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-1",
            FlowType = "HTTP_ACTION",
            ServiceName = "test",
            ActionType = "ORDER_CREATED",
            Status = "SUCCESS",
            CreatedAt = DateTime.UtcNow,
            OrderCode = "EXISTING-CODE",
            PaymentId = "EXISTING-PAYMENT",
            TransactionId = "EXISTING-TX",
            ResponseBody = """
            { "data": { "payment": { "billOrder": "NEW", "transPaymentId": "NEW-PAY", "transactionId": 999 } } }
            """
        };

        EnrichBusinessKeys(message);

        message.OrderCode.Should().Be("EXISTING-CODE");
        message.PaymentId.Should().Be("EXISTING-PAYMENT");
        message.TransactionId.Should().Be("EXISTING-TX");
    }

    [Fact]
    public void EnrichBusinessKeys_WhenBodiesAreMissing_DoesNotThrow()
    {
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-1",
            FlowType = "HTTP_ACTION",
            ServiceName = "test",
            ActionType = "ORDER_CREATED",
            Status = "SUCCESS",
            CreatedAt = DateTime.UtcNow
        };

        var act = () => EnrichBusinessKeys(message);

        act.Should().NotThrow();
        message.OrderCode.Should().BeNull();
        message.PaymentId.Should().BeNull();
        message.TransactionId.Should().BeNull();
    }

    [Fact]
    public void EnrichBusinessKeys_FallsBackToRequestBody_WhenResponseBodyMissing()
    {
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-1",
            FlowType = "HTTP_ACTION",
            ServiceName = "test",
            ActionType = "ORDER_CREATED",
            Status = "SUCCESS",
            CreatedAt = DateTime.UtcNow,
            RequestBody = """{ "data": { "payment": { "billOrder": "FROM-REQ" } } }"""
        };

        EnrichBusinessKeys(message);

        message.OrderCode.Should().Be("FROM-REQ");
    }

    // Mirror of the production helper - keep in sync with KafkaLogConsumerService.EnrichBusinessKeys
    private static void EnrichBusinessKeys(LogEventMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.OrderCode) &&
            !string.IsNullOrWhiteSpace(message.PaymentId) &&
            !string.IsNullOrWhiteSpace(message.TransactionId))
        {
            return;
        }

        var candidates = new[] { message.ResponseBody, message.RequestBody };

        string? orderCode = message.OrderCode;
        string? paymentId = message.PaymentId;
        string? transactionId = message.TransactionId;

        foreach (var raw in candidates)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(orderCode))
            {
                orderCode = BusinessKeyExtractor.ExtractOrderCode(raw);
            }

            if (string.IsNullOrWhiteSpace(paymentId))
            {
                paymentId = BusinessKeyExtractor.ExtractPaymentId(raw);
            }

            if (string.IsNullOrWhiteSpace(transactionId))
            {
                transactionId = BusinessKeyExtractor.ExtractTransactionId(raw);
            }

            if (!string.IsNullOrWhiteSpace(orderCode) &&
                !string.IsNullOrWhiteSpace(paymentId) &&
                !string.IsNullOrWhiteSpace(transactionId))
            {
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(orderCode))
        {
            message.OrderCode = orderCode;
        }

        if (!string.IsNullOrWhiteSpace(paymentId))
        {
            message.PaymentId = paymentId;
        }

        if (!string.IsNullOrWhiteSpace(transactionId))
        {
            message.TransactionId = transactionId;
        }
    }
}

public class MapFlowFromMessageTransactionIdTests
{
    [Fact]
    public void MapFlowFromMessage_SetsTransactionIdFromMessage()
    {
        var flow = new LogFlow
        {
            Id = Guid.NewGuid(),
            FlowId = "tx-flow",
            FlowType = "CHECKOUT_ESIM",
            Status = "SUCCESS",
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "tx-flow",
            FlowType = "CHECKOUT_ESIM",
            ServiceName = "TestService",
            ActionType = "ORDER_CREATED",
            Status = "SUCCESS",
            CreatedAt = DateTime.UtcNow,
            TransactionId = "1640",
            OrderCode = "HPCLLLCEFY74842552",
            PaymentId = "ZQOX3QPYQ764615011"
        };

        MapFlowFromMessage(flow, message);

        flow.TransactionId.Should().Be("1640");
        flow.OrderCode.Should().Be("HPCLLLCEFY74842552");
        flow.PaymentId.Should().Be("ZQOX3QPYQ764615011");
    }

    [Fact]
    public void MapFlowFromMessage_DoesNotOverwriteExistingTransactionIdWithNull()
    {
        var flow = new LogFlow
        {
            Id = Guid.NewGuid(),
            FlowId = "tx-flow-2",
            FlowType = "CHECKOUT_ESIM",
            Status = "SUCCESS",
            TransactionId = "EXISTING-TX",
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "tx-flow-2",
            FlowType = "CHECKOUT_ESIM",
            ServiceName = "TestService",
            ActionType = "PAYMENT_SUCCESS",
            Status = "SUCCESS",
            CreatedAt = DateTime.UtcNow
            // TransactionId is null - should NOT clobber existing value
        };

        MapFlowFromMessage(flow, message);

        flow.TransactionId.Should().Be("EXISTING-TX");
    }

        // Mirror of the production mapper - keep in sync with KafkaLogConsumerService.MapFlowFromMessage
    private static void MapFlowFromMessage(LogFlow flow, LogEventMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.FlowType) &&
            (string.IsNullOrWhiteSpace(flow.FlowType) ||
             flow.FlowType == "HTTP_ACTION" ||
             message.FlowType == "CHECKOUT_ESIM"))
        {
            flow.FlowType = message.FlowType;
        }

        flow.CheckoutType ??= message.CheckoutType;
        flow.CustomerEmail ??= message.CustomerEmail;
        flow.CustomerPhone ??= message.CustomerPhone;
        flow.UserId ??= message.UserId;
        flow.UserEmail ??= message.UserEmail;
        flow.Username ??= message.Username;
        flow.PartnerId ??= message.PartnerId;
        flow.OrderId ??= message.OrderId;
        flow.OrderCode ??= message.OrderCode;
        flow.PaymentId ??= message.PaymentId;
        flow.TransactionId ??= message.TransactionId;

        flow.Status = message.Status;
        flow.StartedAt = message.CreatedAt;
        flow.LastActionType = message.ActionType;
        flow.LastMessage = message.Message;
        flow.TotalSteps++;
    }
}

public class CustomerInfoExtractorTests
{
    [Fact]
    public void ExtractCustomerEmail_FromEmailField_ReturnsValue()
    {
        const string payload = """
        {
          "invoicePartnerId": null,
          "paymentChannel": "ONEPAY",
          "modifyMoney": 24000,
          "isPartnerPayment": true,
          "paymentMoney": 19200,
          "fullname": "1",
          "email": "minzdapoet@gmail.com",
          "phoneNumber": "0867687325",
          "voucherCode": null
        }
        """;

        var result = BusinessKeyExtractor.ExtractCustomerEmail(payload);

        result.Should().Be("minzdapoet@gmail.com");
    }

    [Fact]
    public void ExtractCustomerPhone_FromPhoneNumberField_ReturnsValue()
    {
        const string payload = """
        {
          "invoicePartnerId": null,
          "paymentChannel": "ONEPAY",
          "modifyMoney": 24000,
          "isPartnerPayment": true,
          "paymentMoney": 19200,
          "fullname": "1",
          "email": "minzdapoet@gmail.com",
          "phoneNumber": "0867687325",
          "voucherCode": null
        }
        """;

        var result = BusinessKeyExtractor.ExtractCustomerPhone(payload);

        result.Should().Be("0867687325");
    }

    [Fact]
    public void ExtractCustomerEmail_FromWrapperWithJsonBodyString_ReturnsValue()
    {
        const string payload = """
        {
          "body": "{\"fullname\":\"Test\",\"email\":\"customer@test.com\",\"phoneNumber\":\"0909123456\"}",
          "path": "/apis/partner/order/create",
          "headers": { "content-type": "application/json" }
        }
        """;

        var result = BusinessKeyExtractor.ExtractCustomerEmail(payload);

        result.Should().Be("customer@test.com");
    }

    [Fact]
    public void ExtractCustomerPhone_FromWrapperWithJsonBodyString_ReturnsValue()
    {
        const string payload = """
        {
          "body": "{\"fullname\":\"Test\",\"email\":\"customer@test.com\",\"phoneNumber\":\"0909123456\"}",
          "path": "/apis/partner/order/create",
          "headers": { "content-type": "application/json" }
        }
        """;

        var result = BusinessKeyExtractor.ExtractCustomerPhone(payload);

        result.Should().Be("0909123456");
    }

    [Fact]
    public void ExtractCustomerEmail_DoesNotOverwriteWithNull_OrEmpty()
    {
        const string payload = """
        {
          "email": "",
          "phoneNumber": null
        }
        """;

        var result = BusinessKeyExtractor.ExtractCustomerEmail(payload);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractCustomerName_FromFullname_ReturnsValue()
    {
        const string payload = """
        {
          "fullname": "John Doe",
          "email": "john@example.com",
          "phoneNumber": "0909123456"
        }
        """;

        var result = BusinessKeyExtractor.ExtractCustomerName(payload);

        result.Should().Be("John Doe");
    }

    [Fact]
    public void ExtractCustomerEmail_PrefersCustomerEmail_OverEmail()
    {
        const string payload = """
        {
          "customerEmail": "preferred@test.com",
          "email": "fallback@test.com"
        }
        """;

        var result = BusinessKeyExtractor.ExtractCustomerEmail(payload);

        result.Should().Be("preferred@test.com");
    }

    [Fact]
    public void ExtractCustomerPhone_PrefersCustomerPhone_OverPhoneNumber()
    {
        const string payload = """
        {
          "customerPhone": "preferred-phone",
          "phoneNumber": "fallback-phone"
        }
        """;

        var result = BusinessKeyExtractor.ExtractCustomerPhone(payload);

        result.Should().Be("preferred-phone");
    }

    [Fact]
    public void ExtractCustomerName_PrefersFullname_OverName()
    {
        const string payload = """
        {
          "fullname": "Preferred Name",
          "name": "Fallback Name"
        }
        """;

        var result = BusinessKeyExtractor.ExtractCustomerName(payload);

        result.Should().Be("Preferred Name");
    }

    [Fact]
    public void ExtractCustomerEmail_WhenPayloadIsNull_ReturnsNull()
    {
        BusinessKeyExtractor.ExtractCustomerEmail(null).Should().BeNull();
        BusinessKeyExtractor.ExtractCustomerPhone(null).Should().BeNull();
        BusinessKeyExtractor.ExtractCustomerName(null).Should().BeNull();
    }

    [Fact]
    public void ExtractCustomerEmail_WhenPayloadIsEmpty_ReturnsNull()
    {
        BusinessKeyExtractor.ExtractCustomerEmail("").Should().BeNull();
        BusinessKeyExtractor.ExtractCustomerPhone("").Should().BeNull();
        BusinessKeyExtractor.ExtractCustomerName("").Should().BeNull();
    }

    [Fact]
    public void ExtractCustomerEmail_WhenPayloadIsNotJson_ReturnsNull()
    {
        BusinessKeyExtractor.ExtractCustomerEmail("not json").Should().BeNull();
        BusinessKeyExtractor.ExtractCustomerPhone("not json").Should().BeNull();
        BusinessKeyExtractor.ExtractCustomerName("not json").Should().BeNull();
    }

    [Fact]
    public void ExtractCustomerEmail_FromNestedObject_ReturnsValue()
    {
        const string payload = """
        {
          "data": {
            "customer": {
              "email": "nested@test.com",
              "phone": "0909123456"
            }
          }
        }
        """;

        BusinessKeyExtractor.ExtractCustomerEmail(payload).Should().Be("nested@test.com");
        BusinessKeyExtractor.ExtractCustomerPhone(payload).Should().Be("0909123456");
    }
}

public class EnrichCustomerInfoTests
{
    [Fact]
    public void EnrichCustomerInfo_FillsMissingCustomerEmailAndPhone_FromRequestBody()
    {
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-1",
            FlowType = "CHECKOUT_ESIM",
            ServiceName = "partner-service",
            ActionType = "HTTP_REQUEST",
            Status = "SUCCESS",
            CreatedAt = DateTime.UtcNow,
            RequestBody = """
            {
              "fullname": "1",
              "email": "minzdapoet@gmail.com",
              "phoneNumber": "0867687325",
              "voucherCode": null
            }
            """
        };

        EnrichCustomerInfo(message);

        message.CustomerEmail.Should().Be("minzdapoet@gmail.com");
        message.CustomerPhone.Should().Be("0867687325");
    }

    [Fact]
    public void EnrichCustomerInfo_DoesNotOverwriteExistingCustomerEmailOrPhone()
    {
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-1",
            FlowType = "CHECKOUT_ESIM",
            ServiceName = "partner-service",
            ActionType = "HTTP_REQUEST",
            Status = "SUCCESS",
            CreatedAt = DateTime.UtcNow,
            CustomerEmail = "existing@test.com",
            CustomerPhone = "0909000000",
            RequestBody = """
            {
              "email": "new-customer@test.com",
              "phoneNumber": "0999999999"
            }
            """
        };

        EnrichCustomerInfo(message);

        message.CustomerEmail.Should().Be("existing@test.com");
        message.CustomerPhone.Should().Be("0909000000");
    }

    [Fact]
    public void EnrichCustomerInfo_DoesNotOverwriteUserEmail_WithCustomerEmail()
    {
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-1",
            FlowType = "CHECKOUT_ESIM",
            ServiceName = "partner-service",
            ActionType = "HTTP_REQUEST",
            Status = "SUCCESS",
            CreatedAt = DateTime.UtcNow,
            UserEmail = "lasoapp@gmail.com",
            UserId = "user-123",
            RequestBody = """
            {
              "email": "minzdapoet@gmail.com",
              "phoneNumber": "0867687325"
            }
            """
        };

        EnrichCustomerInfo(message);

        // CustomerEmail extracted from payload
        message.CustomerEmail.Should().Be("minzdapoet@gmail.com");
        // UserEmail preserved from JWT/auth context
        message.UserEmail.Should().Be("lasoapp@gmail.com");
        message.UserId.Should().Be("user-123");
    }

    [Fact]
    public void EnrichCustomerInfo_ParsesWrapperBodyWithJsonString()
    {
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-1",
            FlowType = "HTTP_ACTION",
            ServiceName = "partner-service",
            ActionType = "HTTP_REQUEST",
            Status = "SUCCESS",
            CreatedAt = DateTime.UtcNow,
            RequestBody = """
            {
              "body": "{\"fullname\":\"Test\",\"email\":\"wrapper@test.com\",\"phoneNumber\":\"0909123456\"}",
              "path": "/apis/partner/order/create"
            }
            """
        };

        EnrichCustomerInfo(message);

        message.CustomerEmail.Should().Be("wrapper@test.com");
        message.CustomerPhone.Should().Be("0909123456");
    }

    [Fact]
    public void EnrichCustomerInfo_WhenBodiesAreMissing_DoesNotThrow()
    {
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-1",
            FlowType = "HTTP_ACTION",
            ServiceName = "partner-service",
            ActionType = "HTTP_REQUEST",
            Status = "SUCCESS",
            CreatedAt = DateTime.UtcNow
        };

        var act = () => EnrichCustomerInfo(message);

        act.Should().NotThrow();
        message.CustomerEmail.Should().BeNull();
        message.CustomerPhone.Should().BeNull();
    }

    [Fact]
    public void EnrichCustomerInfo_DoesNotOverwriteWithNullOrEmpty()
    {
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-1",
            FlowType = "HTTP_ACTION",
            ServiceName = "partner-service",
            ActionType = "HTTP_REQUEST",
            Status = "SUCCESS",
            CreatedAt = DateTime.UtcNow,
            CustomerEmail = "preserved@test.com",
            CustomerPhone = "0909000000",
            RequestBody = """
            {
              "email": null,
              "phoneNumber": "",
              "fullname": ""
            }
            """
        };

        EnrichCustomerInfo(message);

        message.CustomerEmail.Should().Be("preserved@test.com");
        message.CustomerPhone.Should().Be("0909000000");
    }

    [Fact]
    public void EnrichCustomerInfo_FallsBackToResponseBody_WhenRequestBodyMissing()
    {
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-1",
            FlowType = "CHECKOUT_ESIM",
            ServiceName = "partner-service",
            ActionType = "ORDER_CREATED",
            Status = "SUCCESS",
            CreatedAt = DateTime.UtcNow,
            ResponseBody = """
            {
              "data": {
                "customerEmail": "from-response@test.com",
                "customerPhone": "0909000000"
              }
            }
            """
        };

        EnrichCustomerInfo(message);

        message.CustomerEmail.Should().Be("from-response@test.com");
        message.CustomerPhone.Should().Be("0909000000");
    }

    // Mirror of the production helper - keep in sync with KafkaLogConsumerService.EnrichCustomerInfo
    private static void EnrichCustomerInfo(LogEventMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.CustomerEmail) &&
            !string.IsNullOrWhiteSpace(message.CustomerPhone))
        {
            return;
        }

        var candidates = new[] { message.RequestBody, message.ResponseBody };

        string? customerEmail = message.CustomerEmail;
        string? customerPhone = message.CustomerPhone;

        foreach (var raw in candidates)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(customerEmail))
            {
                customerEmail = BusinessKeyExtractor.ExtractCustomerEmail(raw);
            }

            if (string.IsNullOrWhiteSpace(customerPhone))
            {
                customerPhone = BusinessKeyExtractor.ExtractCustomerPhone(raw);
            }

            if (!string.IsNullOrWhiteSpace(customerEmail) &&
                !string.IsNullOrWhiteSpace(customerPhone))
            {
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(customerEmail))
        {
            message.CustomerEmail = customerEmail;
        }

        if (!string.IsNullOrWhiteSpace(customerPhone))
        {
            message.CustomerPhone = customerPhone;
        }
    }
}