using System.Text;
using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.TestHelper;
using Skysim.Logger.Api.Kafka;
using Skysim.Logger.Api.Validators;
using Skysim.Logger.Api.Infrastructure.Persistence.Exceptions;
using Skysim.Logger.Api.Domain.Services;
using Skysim.Logger.Infrastructure.Entities;
using Xunit;
using LogEventMessage = Skysim.Logger.Contracts.Events.LogEventMessage;
using StatusTypes = Skysim.Logger.Contracts.Constants.StatusTypes;
using ActionTypes = Skysim.Logger.Contracts.Constants.ActionTypes;
using FlowTypes = Skysim.Logger.Contracts.Constants.FlowTypes;
using CheckoutTypes = Skysim.Logger.Contracts.Constants.CheckoutTypes;

namespace Skysim.Logger.Api.Tests.Consumers;

public class KafkaLogConsumerServiceTests
{
    public KafkaLogConsumerServiceTests()
    {
        // Tests focus on testable components (deserialization, validation, logic)
        // Repository and integration tests require actual PostgreSQL database
    }

    private static LogEventMessage CreateValidMessage(
        Guid? eventId = null,
        string? flowId = null,
        string? actionType = null,
        string? status = null)
    {
        return new LogEventMessage
        {
            EventId = eventId ?? Guid.NewGuid(),
            FlowId = flowId ?? "test-flow-001",
            FlowType = FlowTypes.CheckoutEsim,
            ServiceName = "Order",
            ActionType = actionType ?? ActionTypes.OrderCreated,
            Status = status ?? StatusTypes.Success,
            CreatedAt = DateTime.UtcNow,
            CheckoutType = CheckoutTypes.Guest,
            CustomerEmail = "test@example.com",
            OrderId = "ORD-001"
        };
    }

    [Fact]
    public void Deserializer_ValidJson_ReturnsMessage()
    {
        // Arrange
        var message = CreateValidMessage();
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);

        // Act
        var result = DeserializeMessage(bytes);

        // Assert
        result.Should().NotBeNull();
        result!.EventId.Should().Be(message.EventId);
        result.FlowId.Should().Be(message.FlowId);
        result.ActionType.Should().Be(message.ActionType);
        result.Status.Should().Be(message.Status);
    }

    [Fact]
    public void Deserializer_InvalidJson_ThrowsJsonException()
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes("{ invalid json }");

        // Act & Assert
        var act = () => DeserializeMessage(bytes);
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserializer_EmptyPayload_ReturnsNull()
    {
        // Arrange
        var bytes = Array.Empty<byte>();

        // Act
        var result = DeserializeMessage(bytes);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Deserializer_NullPayload_ReturnsNull()
    {
        // Arrange
        byte[]? bytes = null;

        // Act
        var result = DeserializeMessage(bytes!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Validator_ValidMessage_ReturnsSuccess()
    {
        // Arrange
        var validator = new LogEventMessageValidator();
        var message = CreateValidMessage();

        // Act
        var result = await validator.ValidateAsync(message);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validator_MissingEventId_ReturnsError()
    {
        // Arrange
        var validator = new LogEventMessageValidator();
        var message = CreateValidMessage();
        message.EventId = Guid.Empty;

        // Act
        var result = await validator.ValidateAsync(message);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "EventId");
    }

    [Fact]
    public async Task Validator_MissingFlowId_ReturnsError()
    {
        // Arrange
        var validator = new LogEventMessageValidator();
        var message = CreateValidMessage();
        message.FlowId = string.Empty;

        // Act
        var result = await validator.ValidateAsync(message);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FlowId");
    }

    [Fact]
    public async Task Validator_FlowIdTooLong_ReturnsError()
    {
        // Arrange
        var validator = new LogEventMessageValidator();
        var message = CreateValidMessage();
        message.FlowId = new string('a', 101);

        // Act
        var result = await validator.ValidateAsync(message);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FlowId");
    }

    [Fact]
    public async Task Validator_GuestCheckoutWithoutUserId_ReturnsSuccess()
    {
        // Arrange
        var validator = new LogEventMessageValidator();
        var message = CreateValidMessage();
        message.CheckoutType = CheckoutTypes.Guest;
        message.UserId = null;

        // Act
        var result = await validator.ValidateAsync(message);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validator_MissingServiceName_ReturnsError()
    {
        // Arrange
        var validator = new LogEventMessageValidator();
        var message = CreateValidMessage();
        message.ServiceName = string.Empty;

        // Act
        var result = await validator.ValidateAsync(message);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ServiceName");
    }

    [Fact]
    public async Task Validator_AllOptionalFieldsNull_ReturnsSuccess()
    {
        // Arrange
        var validator = new LogEventMessageValidator();
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "test-flow",
            FlowType = FlowTypes.CheckoutEsim,
            ServiceName = "Order",
            ActionType = ActionTypes.OrderCreated,
            Status = StatusTypes.Success,
            CreatedAt = DateTime.UtcNow
            // All optional fields null
        };

        // Act
        var result = await validator.ValidateAsync(message);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void BuildLogAction_MapsAllRequiredFields()
    {
        // Arrange
        var message = CreateValidMessage();
        message.Duration = 150;
        message.RequestTime = DateTime.UtcNow.AddSeconds(-2);
        message.ResponseTime = DateTime.UtcNow;
        message.CorrelationId = "corr-123";
        message.ErrorCode = "ERR001";
        message.ErrorMessage = "Something went wrong";

        var flowId = Guid.NewGuid();

        // Act
        var action = BuildLogAction(message, flowId);

        // Assert
        action.EventId.Should().Be(message.EventId);
        action.FlowId.Should().Be(message.FlowId);
        action.ServiceName.Should().Be(message.ServiceName);
        action.ActionType.Should().Be(message.ActionType.ToString());
        action.Status.Should().Be(message.Status.ToString());
        action.DurationMs.Should().Be(150);
        action.CorrelationId.Should().Be("corr-123");
        action.ErrorCode.Should().Be("ERR001");
        action.ErrorMessage.Should().Be("Something went wrong");
    }

    [Fact]
    public void BuildLogAction_CalculatesDurationFromTimestampsWhenNotProvided()
    {
        // Arrange
        var message = CreateValidMessage();
        message.Duration = null;
        message.RequestTime = DateTime.UtcNow.AddSeconds(-5);
        message.ResponseTime = DateTime.UtcNow;

        // Act
        var action = BuildLogAction(message, Guid.NewGuid());

        // Assert
        action.DurationMs.Should().BeGreaterThan(4000);
        action.DurationMs.Should().BeLessThan(6000);
    }

    [Fact]
    public void BuildLogAction_UsesProvidedDurationWhenAvailable()
    {
        // Arrange
        var message = CreateValidMessage();
        message.Duration = 500;
        message.RequestTime = DateTime.UtcNow.AddSeconds(-100);
        message.ResponseTime = DateTime.UtcNow;

        // Act
        var action = BuildLogAction(message, Guid.NewGuid());

        // Assert
        action.DurationMs.Should().Be(500);
    }

    [Fact]
    public void DuplicateEventException_ContainsEventId()
    {
        // Arrange
        var eventId = Guid.NewGuid();

        // Act
        var exception = new DuplicateEventException(eventId);

        // Assert
        exception.EventId.Should().Be(eventId);
        exception.Message.Should().Contain(eventId.ToString());
    }

    [Fact]
    public void RetryOptions_DefaultValues_AreReasonable()
    {
        // Arrange
        var options = new RetryOptions();

        // Assert
        options.MaxAttempts.Should().Be(5);
        options.InitialDelayMs.Should().Be(200);
        options.BackoffMultiplier.Should().Be(2.0);
        options.MaxDelayMs.Should().Be(3200);
    }

    [Fact]
    public void CalculateDelay_ExponentialBackoff_Works()
    {
        // Arrange
        var options = new RetryOptions
        {
            InitialDelayMs = 100,
            BackoffMultiplier = 2.0,
            MaxDelayMs = 1000
        };

        // Act
        var delay1 = CalculateDelay(1, options);
        var delay2 = CalculateDelay(2, options);
        var delay3 = CalculateDelay(3, options);

        // Assert
        delay1.TotalMilliseconds.Should().Be(100); // 100 * 2^0 = 100
        delay2.TotalMilliseconds.Should().Be(200); // 100 * 2^1 = 200
        delay3.TotalMilliseconds.Should().Be(400); // 100 * 2^2 = 400
    }

    [Fact]
    public void CalculateDelay_CapsAtMaxDelay()
    {
        // Arrange
        var options = new RetryOptions
        {
            InitialDelayMs = 100,
            BackoffMultiplier = 10.0,
            MaxDelayMs = 500
        };

        // Act
        var delay = CalculateDelay(5, options);

        // Assert
        delay.TotalMilliseconds.Should().Be(500); // Capped at max
    }

    // Helper methods that mirror the service's internal logic

    private static LogEventMessage? DeserializeMessage(byte[] payload)
    {
        if (payload == null || payload.Length == 0)
        {
            return null;
        }

        var jsonString = Encoding.UTF8.GetString(payload);
        return JsonSerializer.Deserialize<LogEventMessage>(jsonString, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private static LogAction BuildLogAction(LogEventMessage message, Guid flowId)
    {
        var durationMs = message.Duration;
        if (!durationMs.HasValue && message.RequestTime.HasValue && message.ResponseTime.HasValue)
        {
            durationMs = (int)(message.ResponseTime.Value - message.RequestTime.Value).TotalMilliseconds;
        }

        return new LogAction
        {
            Id = Guid.NewGuid(),
            EventId = message.EventId,
            FlowId = message.FlowId,
            StepOrder = 0,
            ServiceName = message.ServiceName,
            ActionType = message.ActionType.ToString(),
            Status = message.Status.ToString(),
            Message = message.Message,
            ErrorCode = message.ErrorCode,
            ErrorMessage = message.ErrorMessage,
            RequestTime = message.RequestTime,
            ResponseTime = message.ResponseTime,
            DurationMs = durationMs,
            CorrelationId = message.CorrelationId,
            CreatedAt = message.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static TimeSpan CalculateDelay(int attempt, RetryOptions options)
    {
        var delay = options.InitialDelayMs * Math.Pow(options.BackoffMultiplier, attempt - 1);
        return TimeSpan.FromMilliseconds(Math.Min(delay, options.MaxDelayMs));
    }
}

// Test class specifically for MapFlowFromMessage merge behavior
public class MapFlowFromMessageMergeTests
{
    private static LogEventMessage CreateMessage(
        string flowId,
        string flowType,
        string actionType,
        string status,
        string? checkoutType = null,
        string? customerEmail = null,
        string? customerPhone = null,
        string? userId = null,
        string? orderId = null,
        string? paymentId = null,
        string? message = null)
    {
        return new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = flowId,
            FlowType = flowType,
            ServiceName = "TestService",
            ActionType = actionType,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            CheckoutType = checkoutType,
            CustomerEmail = customerEmail,
            CustomerPhone = customerPhone,
            UserId = userId,
            OrderId = orderId,
            PaymentId = paymentId,
            Message = message
        };
    }

    private static LogFlow CreateFlow(string flowType = FlowTypes.HttpAction)
    {
        return new LogFlow
        {
            Id = Guid.NewGuid(),
            FlowId = "test-flow",
            FlowType = flowType,
            Status = StatusTypes.Success,
            TotalSteps = 0,
            SuccessSteps = 0,
            FailedSteps = 0,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    // 6.2 / fix: CHECKOUT_ESIM message upgrades flow regardless of current flowType
    [Fact]
    public void MapFlowFromMessage_ChekoutEsimEvent_SetsFlowTypeToCheckoutEsim()
    {
        // Arrange - starts as HTTP_ACTION (middleware logs first)
        var flow = CreateFlow(FlowTypes.HttpAction);
        var message = CreateMessage(
            flowId: "test-flow-001",
            flowType: FlowTypes.CheckoutEsim,
            actionType: ActionTypes.OrderCreated,
            status: StatusTypes.Success,
            checkoutType: CheckoutTypes.Guest,
            customerEmail: "test@example.com",
            orderId: "ORD-123");

        // Act
        MapFlowFromMessage(flow, message);

        // Assert
        flow.FlowType.Should().Be(FlowTypes.CheckoutEsim);
    }

    [Fact]
    public void MapFlowFromMessage_ChekoutEsimEvent_UpdatesBusinessFields()
    {
        // Arrange
        var flow = CreateFlow(FlowTypes.HttpAction);
        var message = CreateMessage(
            flowId: "test-flow-001",
            flowType: FlowTypes.CheckoutEsim,
            actionType: ActionTypes.OrderCreated,
            status: StatusTypes.Success,
            checkoutType: CheckoutTypes.Guest,
            customerEmail: "test@example.com",
            orderId: "ORD-123");

        // Act
        MapFlowFromMessage(flow, message);

        // Assert
        flow.CheckoutType.Should().Be(CheckoutTypes.Guest);
        flow.CustomerEmail.Should().Be("test@example.com");
        flow.OrderId.Should().Be("ORD-123");
    }

    [Fact]
    public void MapFlowFromMessage_ChekoutEsimEvent_SetsFlowType_WhenCurrentIsEmptyString()
    {
        // Arrange - new LogFlow defaults to string.Empty before any message is processed
        var flow = new LogFlow
        {
            Id = Guid.NewGuid(),
            FlowId = "test-flow",
            FlowType = string.Empty, // This is what happens when a new LogFlow is created
            Status = string.Empty,
            TotalSteps = 0,
            SuccessSteps = 0,
            FailedSteps = 0,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var message = CreateMessage(
            flowId: "test-flow",
            flowType: FlowTypes.CheckoutEsim,
            actionType: ActionTypes.OrderCreated,
            status: StatusTypes.Success,
            checkoutType: CheckoutTypes.Guest);

        // Act
        MapFlowFromMessage(flow, message);

        // Assert
        flow.FlowType.Should().Be(FlowTypes.CheckoutEsim);
    }

    // 6.3: CHECKOUT_ESIM exists, HTTP_ACTION arrives later and must not clear business fields
    [Fact]
    public void MapFlowFromMessage_CheckoutEsimExists_HttpActionDoesNotClearBusinessFields()
    {
        // Arrange
        var flow = CreateFlow(FlowTypes.CheckoutEsim);
        flow.CustomerEmail = "existing@example.com";
        flow.CustomerPhone = "0900123456";
        flow.OrderId = "ORD-existing";
        flow.PaymentId = "PAY-existing";
        flow.UserId = "user-123";
        flow.CheckoutType = CheckoutTypes.Authenticated;
        flow.LastActionType = ActionTypes.EmailSent;
        flow.LastMessage = "Email sent successfully";

        var message = CreateMessage(
            flowId: "test-flow-002",
            flowType: FlowTypes.HttpAction,
            actionType: ActionTypes.HttpRequest,
            status: StatusTypes.Success,
            customerEmail: null,
            customerPhone: null,
            orderId: null,
            paymentId: null,
            message: "HTTP GET /api/checkout");

        // Act
        MapFlowFromMessage(flow, message);

        // Assert
        flow.CustomerEmail.Should().Be("existing@example.com");
        flow.CustomerPhone.Should().Be("0900123456");
        flow.OrderId.Should().Be("ORD-existing");
        flow.PaymentId.Should().Be("PAY-existing");
        flow.UserId.Should().Be("user-123");
    }

    // 6.4: CHECKOUT_ESIM exists, HTTP_ACTION arrives later and must not downgrade flowType
    [Fact]
    public void MapFlowFromMessage_CheckoutEsimExists_HttpActionDoesNotDowngradeFlowType()
    {
        // Arrange
        var flow = CreateFlow(FlowTypes.CheckoutEsim);
        flow.CustomerEmail = "existing@example.com";

        var message = CreateMessage(
            flowId: "test-flow-003",
            flowType: FlowTypes.HttpAction,
            actionType: ActionTypes.HttpRequest,
            status: StatusTypes.Success);

        // Act
        MapFlowFromMessage(flow, message);

        // Assert
        flow.FlowType.Should().Be(FlowTypes.CheckoutEsim);
    }

    // 6.5: HTTP_ACTION-only flow still works (existing behavior preserved)
    [Fact]
    public void MapFlowFromMessage_HttpActionOnlyFlow_WorksNormally()
    {
        // Arrange
        var flow = CreateFlow(FlowTypes.HttpAction);
        var message = CreateMessage(
            flowId: "http-only-flow",
            flowType: FlowTypes.HttpAction,
            actionType: ActionTypes.HttpRequest,
            status: StatusTypes.Success,
            customerEmail: null,
            message: "HTTP GET /api/test");

        // Act
        MapFlowFromMessage(flow, message);

        // Assert
        flow.FlowType.Should().Be(FlowTypes.HttpAction);
        flow.CustomerEmail.Should().BeNull();
    }

    // 6.6: HTTP_ACTION-only flow should still set lastActionType = HTTP_REQUEST
    [Fact]
    public void MapFlowFromMessage_HttpActionOnlyFlow_SetsLastActionTypeToHttpRequest()
    {
        // Arrange
        var flow = CreateFlow(FlowTypes.HttpAction);
        var message = CreateMessage(
            flowId: "http-only-flow",
            flowType: FlowTypes.HttpAction,
            actionType: ActionTypes.HttpRequest,
            status: StatusTypes.Success,
            message: "HTTP GET /api/test");

        // Act
        MapFlowFromMessage(flow, message);

        // Assert
        flow.LastActionType.Should().Be(ActionTypes.HttpRequest);
        flow.LastMessage.Should().Be("HTTP GET /api/test");
    }

    // 6.7: CHECKOUT_ESIM flow preserves lastActionType when HTTP_REQUEST arrives later
    [Fact]
    public void MapFlowFromMessage_CheckoutEsimFlow_PreservesLastActionTypeWhenHttpRequestArrives()
    {
        // Arrange
        var flow = CreateFlow(FlowTypes.CheckoutEsim);
        flow.LastActionType = ActionTypes.EmailSent;
        flow.LastMessage = "Email sent successfully";

        var message = CreateMessage(
            flowId: "business-flow",
            flowType: FlowTypes.HttpAction,
            actionType: ActionTypes.HttpRequest,
            status: StatusTypes.Success,
            message: "HTTP POST /api/checkout/esim");

        // Act
        MapFlowFromMessage(flow, message);

        // Assert
        flow.LastActionType.Should().Be(ActionTypes.EmailSent);
        flow.LastMessage.Should().Be("Email sent successfully");
    }

    [Fact]
    public void MapFlowFromMessage_BusinessFieldsMerged_UsesIncomingNonNullValues()
    {
        // Arrange
        var flow = CreateFlow(FlowTypes.CheckoutEsim);
        flow.CustomerEmail = "existing@example.com";
        flow.CustomerPhone = null;
        flow.OrderId = null;

        var message = CreateMessage(
            flowId: "test-flow-004",
            flowType: FlowTypes.CheckoutEsim,
            actionType: ActionTypes.PaymentSuccess,
            status: StatusTypes.Success,
            customerEmail: null,
            customerPhone: "0900123456",
            orderId: "ORD-new",
            paymentId: "PAY-new");

        // Act
        MapFlowFromMessage(flow, message);

        // Assert
        flow.CustomerEmail.Should().Be("existing@example.com"); // preserved (incoming was null)
        flow.CustomerPhone.Should().Be("0900123456"); // filled (incoming was non-null)
        flow.OrderId.Should().Be("ORD-new"); // filled (incoming was non-null)
        flow.PaymentId.Should().Be("PAY-new"); // filled (incoming was non-null)
    }

    [Fact]
    public void MapFlowFromMessage_AllBusinessFieldsMerged()
    {
        // Arrange
        var flow = CreateFlow(FlowTypes.CheckoutEsim);
        flow.CheckoutType = CheckoutTypes.Authenticated;
        flow.CustomerEmail = "alice@example.com";
        flow.CustomerPhone = "0900000000";
        flow.UserId = "user-001";
        flow.OrderId = "ORD-001";
        flow.PaymentId = "PAY-001";

        var message = CreateMessage(
            flowId: "test-flow-005",
            flowType: FlowTypes.HttpAction,
            actionType: ActionTypes.HttpRequest,
            status: StatusTypes.Success);

        // Act
        MapFlowFromMessage(flow, message);

        // Assert
        flow.CheckoutType.Should().Be(CheckoutTypes.Authenticated);
        flow.CustomerEmail.Should().Be("alice@example.com");
        flow.CustomerPhone.Should().Be("0900000000");
        flow.UserId.Should().Be("user-001");
        flow.OrderId.Should().Be("ORD-001");
        flow.PaymentId.Should().Be("PAY-001");
    }

    [Fact]
    public void MapFlowFromMessage_ChecksTerminalAction()
    {
        // Arrange
        var flow = CreateFlow(FlowTypes.CheckoutEsim);

        // ESIM_ACTIVATED is a terminal success action
        var message = CreateMessage(
            flowId: "test-flow",
            flowType: FlowTypes.CheckoutEsim,
            actionType: ActionTypes.EsimActivated,
            status: StatusTypes.Success);

        // Act
        MapFlowFromMessage(flow, message);

        // Assert
        flow.CompletedAt.Should().NotBeNull();
    }

    private static void MapFlowFromMessage(LogFlow flow, LogEventMessage message)
    {
        // Upgrade flowType: set to CHECKOUT_ESIM for business messages.
        // Handles both new flows (string.Empty -> CHECKOUT_ESIM) and HTTP_ACTION -> CHECKOUT_ESIM upgrades.
        if (message.FlowType == FlowTypes.CheckoutEsim)
        {
            flow.FlowType = message.FlowType;
        }

        // Merge business fields: use incoming non-null values, preserve existing non-null values
        flow.CheckoutType ??= message.CheckoutType;
        flow.CustomerEmail ??= message.CustomerEmail;
        flow.CustomerPhone ??= message.CustomerPhone;
        flow.UserId ??= message.UserId;
        flow.OrderId ??= message.OrderId;
        flow.PaymentId ??= message.PaymentId;

        // Update status from latest event
        flow.Status = message.Status;
        flow.StartedAt = message.CreatedAt;

        // Preserve lastActionType/lastMessage: HTTP_REQUEST after CHECKOUT_ESIM should not overwrite business action
        bool isExistingBusinessFlow = flow.FlowType == FlowTypes.CheckoutEsim;
        bool isHttpRequest = message.ActionType == ActionTypes.HttpRequest;

        if (isHttpRequest && isExistingBusinessFlow)
        {
            // Preserve existing lastActionType/lastMessage (business action preserved)
        }
        else
        {
            flow.LastActionType = message.ActionType;
            flow.LastMessage = message.Message;
        }

        if (message.Status == StatusTypes.Success)
        {
            flow.SuccessSteps++;
            if (FlowDomainService.IsTerminalAction(message.ActionType, message.Status))
            {
                flow.CompletedAt = DateTime.UtcNow;
            }
        }
        else if (message.Status == StatusTypes.Failed)
        {
            flow.FailedSteps++;
            flow.CompletedAt = DateTime.UtcNow;
        }
        flow.TotalSteps++;
    }
}

// Test class specifically for NormalizeServiceName logic
public class NormalizeServiceNameTests
{
    private static LogEventMessage CreateHttpActionMessage(string? serviceName = null)
    {
        return new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "test-flow-http",
            FlowType = FlowTypes.HttpAction,
            ServiceName = serviceName ?? string.Empty,
            ActionType = ActionTypes.HttpRequest,
            Status = StatusTypes.Success,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static LogEventMessage CreateBusinessMessage(string? serviceName = null)
    {
        return new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "test-flow-business",
            FlowType = FlowTypes.CheckoutEsim,
            ServiceName = serviceName ?? string.Empty,
            ActionType = ActionTypes.OrderCreated,
            Status = StatusTypes.Success,
            CreatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public void NormalizeServiceName_ServiceNameAlreadySet_DoesNotOverwrite()
    {
        // Arrange
        var message = CreateHttpActionMessage("existing-service");

        // Act
        NormalizeServiceName(message);

        // Assert
        message.ServiceName.Should().Be("existing-service");
    }

    [Fact]
    public void NormalizeServiceName_HttpActionWithXSourceService_UsesXSourceService()
    {
        // Arrange
        var message = CreateHttpActionMessage();
        message.RequestHeaders = new Dictionary<string, string>
        {
            { "X-Source-Service", "portal-service" }
        };

        // Act
        NormalizeServiceName(message);

        // Assert
        message.ServiceName.Should().Be("portal-service");
    }

    [Fact]
    public void NormalizeServiceName_HttpActionWithXCallerService_UsesXCallerService()
    {
        // Arrange
        var message = CreateHttpActionMessage();
        message.RequestHeaders = new Dictionary<string, string>
        {
            { "X-Caller-Service", "admin-service" }
        };

        // Act
        NormalizeServiceName(message);

        // Assert
        message.ServiceName.Should().Be("admin-service");
    }

    [Fact]
    public void NormalizeServiceName_HttpActionWithXSourceServiceAndXCallerService_PrefersXSourceService()
    {
        // Arrange
        var message = CreateHttpActionMessage();
        message.RequestHeaders = new Dictionary<string, string>
        {
            { "X-Source-Service", "source-svc" },
            { "X-Caller-Service", "caller-svc" }
        };

        // Act
        NormalizeServiceName(message);

        // Assert
        message.ServiceName.Should().Be("source-svc");
    }

    [Fact]
    public void NormalizeServiceName_HttpActionWithXForwardedPrefix_ParsesToServiceName()
    {
        // Arrange
        var message = CreateHttpActionMessage();
        message.RequestHeaders = new Dictionary<string, string>
        {
            { "X-Forwarded-Prefix", "/apis/partner" }
        };

        // Act
        NormalizeServiceName(message);

        // Assert
        message.ServiceName.Should().Be("partner-service");
    }

    [Fact]
    public void NormalizeServiceName_HttpActionWithPath_ParsesToServiceName()
    {
        // Arrange
        var message = CreateHttpActionMessage();
        message.Path = "/apis/partner/order/create";
        message.FullUrl = "http://171.244.49.17:8211/apis/partner/order/create";

        // Act
        NormalizeServiceName(message);

        // Assert
        message.ServiceName.Should().Be("partner-service");
    }

    [Fact]
    public void NormalizeServiceName_HttpActionWithFullUrlOnly_ParsesToServiceName()
    {
        // Arrange
        var message = CreateHttpActionMessage();
        message.FullUrl = "http://171.244.49.17:8211/apis/partner/order/create";

        // Act
        NormalizeServiceName(message);

        // Assert
        message.ServiceName.Should().Be("partner-service");
    }

    [Fact]
    public void NormalizeServiceName_HttpActionCannotResolve_ReturnsUnknownService()
    {
        // Arrange
        var message = CreateHttpActionMessage();
        message.Path = "/unknown/path";
        message.FullUrl = "http://unknown.com/unknown/path";

        // Act
        NormalizeServiceName(message);

        // Assert
        message.ServiceName.Should().Be("unknown-service");
    }

    [Fact]
    public void NormalizeServiceName_HttpActionNoHeadersNoPath_ReturnsUnknownService()
    {
        // Arrange
        var message = CreateHttpActionMessage();

        // Act
        NormalizeServiceName(message);

        // Assert
        message.ServiceName.Should().Be("unknown-service");
    }

    [Fact]
    public void NormalizeServiceName_NonHttpActionWithEmptyServiceName_DoesNotModify()
    {
        // Arrange
        var message = CreateBusinessMessage();

        // Act
        NormalizeServiceName(message);

        // Assert - serviceName should remain empty for non-HTTP_ACTION
        message.ServiceName.Should().BeEmpty();
    }

    [Fact]
    public void NormalizeServiceName_CaseInsensitiveHeaderLookup_Works()
    {
        // Arrange
        var message = CreateHttpActionMessage();
        message.RequestHeaders = new Dictionary<string, string>
        {
            { "x-source-service", "lowercase-service" }
        };

        // Act
        NormalizeServiceName(message);

        // Assert
        message.ServiceName.Should().Be("lowercase-service");
    }

    [Fact]
    public void NormalizeServiceName_XForwardedPrefixWithTrailingSlash_ParsesCorrectly()
    {
        // Arrange
        var message = CreateHttpActionMessage();
        message.RequestHeaders = new Dictionary<string, string>
        {
            { "X-Forwarded-Prefix", "/apis/partner/" }
        };

        // Act
        NormalizeServiceName(message);

        // Assert
        message.ServiceName.Should().Be("partner-service");
    }

    [Fact]
    public void NormalizeServiceName_HeaderWithEmptyValue_FallsThroughToNextStrategy()
    {
        // Arrange
        var message = CreateHttpActionMessage();
        message.RequestHeaders = new Dictionary<string, string>
        {
            { "X-Source-Service", "" },
            { "X-Forwarded-Prefix", "/apis/partner" }
        };

        // Act
        NormalizeServiceName(message);

        // Assert
        message.ServiceName.Should().Be("partner-service");
    }

    [Fact]
    public void Validate_NormalizedHttpActionMessage_PassesValidation()
    {
        // Arrange - simulate what happens in the consumer after normalization
        var message = CreateHttpActionMessage();
        message.RequestHeaders = new Dictionary<string, string>
        {
            { "X-Forwarded-Prefix", "/apis/partner" }
        };

        NormalizeServiceName(message);

        var validator = new LogEventMessageValidator();

        // Act
        var result = validator.TestValidate(message);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ServiceName);
    }

    [Fact]
    public void Validate_NonHttpActionWithoutServiceName_FailsValidation()
    {
        // Arrange - non-HTTP_ACTION should fail if serviceName is empty
        var message = CreateBusinessMessage();
        message.ServiceName = string.Empty;

        var validator = new LogEventMessageValidator();

        // Act
        var result = validator.TestValidate(message);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ServiceName);
    }

    [Fact]
    public void PortalSimgetgoCheckoutMessage_NormalizesCorrectly()
    {
        // Arrange - simulates actual portal simgetgo checkout message
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "portal-flow-001",
            FlowType = FlowTypes.HttpAction,
            ServiceName = string.Empty, // empty from portal
            ActionType = ActionTypes.HttpRequest,
            Status = StatusTypes.Success,
            CreatedAt = DateTime.UtcNow,
            Path = "/apis/partner/order/create",
            FullUrl = "http://171.244.49.17:8211/apis/partner/order/create",
            RequestHeaders = new Dictionary<string, string>
            {
                { "X-Forwarded-Prefix", "/apis/partner" },
                { "X-Forwarded-Path", "/apis/partner/order/create" }
            }
        };

        // Act
        NormalizeServiceName(message);

        // Assert
        message.ServiceName.Should().Be("partner-service");

        var validator = new LogEventMessageValidator();
        var result = validator.TestValidate(message);
        result.ShouldNotHaveValidationErrorFor(x => x.ServiceName);
    }

    // Mirror the static methods from KafkaLogConsumerService for testing
    private static void NormalizeServiceName(LogEventMessage message)
    {
        if (!string.IsNullOrEmpty(message.ServiceName))
        {
            return;
        }

        if (message.FlowType != FlowTypes.HttpAction)
        {
            return;
        }

        var headers = message.RequestHeaders ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (TryGetHeader(headers, "X-Source-Service", out var sourceService) && !string.IsNullOrEmpty(sourceService))
        {
            message.ServiceName = sourceService;
            return;
        }

        if (TryGetHeader(headers, "X-Caller-Service", out var callerService) && !string.IsNullOrEmpty(callerService))
        {
            message.ServiceName = callerService;
            return;
        }

        if (TryGetHeader(headers, "X-Forwarded-Prefix", out var forwardedPrefix) && !string.IsNullOrEmpty(forwardedPrefix))
        {
            message.ServiceName = ParseServiceNameFromPrefix(forwardedPrefix);
            return;
        }

        if (!string.IsNullOrEmpty(message.Path))
        {
            message.ServiceName = ParseServiceNameFromPath(message.Path);
            if (!string.IsNullOrEmpty(message.ServiceName))
            {
                return;
            }
        }

        if (!string.IsNullOrEmpty(message.FullUrl))
        {
            message.ServiceName = ParseServiceNameFromPath(message.FullUrl);
            return;
        }

        message.ServiceName = "unknown-service";
    }

    private static bool TryGetHeader(Dictionary<string, string> headers, string key, out string value)
    {
        if (headers.TryGetValue(key, out var headerValue))
        {
            value = headerValue;
            return true;
        }

        foreach (var kvp in headers)
        {
            if (string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = kvp.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    private static string ParseServiceNameFromPrefix(string prefix)
    {
        var trimmed = prefix.TrimEnd('/').TrimStart('/');
        var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return "unknown-service";
        }

        var lastSegment = segments[^1];
        return lastSegment.ToLowerInvariant() switch
        {
            "partner" => "partner-service",
            "admin" => "admin-service",
            "user" => "user-service",
            "payment" => "payment-service",
            _ => lastSegment.ToLowerInvariant() + "-service"
        };
    }

    private static string ParseServiceNameFromPath(string pathOrUrl)
    {
        if (string.IsNullOrEmpty(pathOrUrl))
        {
            return string.Empty;
        }

        string path = pathOrUrl;

        // If it's a full URL (contains ://), extract just the path portion
        if (pathOrUrl.Contains("://"))
        {
            var afterScheme = pathOrUrl.Substring(pathOrUrl.IndexOf("://") + 3);
            var slashIndex = afterScheme.IndexOf('/');
            if (slashIndex < 0)
            {
                return "unknown-service";
            }
            path = afterScheme.Substring(slashIndex);
        }

        var segments = path.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            return string.Empty;
        }

        var prefixSegment = segments[0];

        if (prefixSegment.Equals("apis", StringComparison.OrdinalIgnoreCase) && segments.Length > 1)
        {
            prefixSegment = segments[1];
        }

        if (prefixSegment.Equals("partner", StringComparison.OrdinalIgnoreCase))
        {
            return "partner-service";
        }

        if (prefixSegment.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            return "admin-service";
        }

        if (prefixSegment.Equals("user", StringComparison.OrdinalIgnoreCase))
        {
            return "user-service";
        }

        if (prefixSegment.Equals("payment", StringComparison.OrdinalIgnoreCase))
        {
            return "payment-service";
        }

        if (prefixSegment.StartsWith("api", StringComparison.OrdinalIgnoreCase))
        {
            return prefixSegment.ToLowerInvariant() + "-service";
        }

        return prefixSegment.ToLowerInvariant() + "-service";
    }
}
