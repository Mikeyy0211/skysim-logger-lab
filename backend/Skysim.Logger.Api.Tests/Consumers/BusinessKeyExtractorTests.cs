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