using Skysim.Logger.Client.Producers;
using Skysim.Logger.Contracts.Constants;
using Skysim.Logger.Contracts.Events;
using Skysim.Logger.SampleService.DTOs;
using LogEventMessage = Skysim.Logger.Contracts.Events.LogEventMessage;

namespace Skysim.Logger.SampleService.Services;

/// <summary>
/// Service for publishing business action logs to Kafka.
/// Handles the complete eSIM checkout flow by publishing individual action events.
/// </summary>
public class BusinessActionLogger : IBusinessActionLogger
{
    private readonly IKafkaLogProducer _producer;
    private readonly ILogger<BusinessActionLogger> _logger;

    public BusinessActionLogger(IKafkaLogProducer producer, ILogger<BusinessActionLogger> logger)
    {
        _producer = producer;
        _logger = logger;
    }

    /// <summary>
    /// Publishes a complete eSIM checkout flow with all business action events to Kafka.
    /// Events are published in order: OrderCreated, PaymentRequested, PaymentSuccess,
    /// ProviderRequested, EsimActivated, EmailSent.
    /// </summary>
    /// <param name="request">The checkout request containing customer and package details.</param>
    /// <param name="flowId">The unique flow identifier for correlation.</param>
    /// <param name="checkoutType">The checkout type (Guest or Authenticated).</param>
    /// <param name="orderId">The generated order identifier.</param>
    /// <param name="paymentId">The generated payment identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task PublishCheckoutFlowAsync(
        CheckoutEsimRequest request,
        string flowId,
        string checkoutType,
        string orderId,
        string? paymentId,
        CancellationToken ct = default)
    {
        var createdAt = DateTime.UtcNow;

        await PublishEventSafelyAsync(CreateOrderCreatedEvent(flowId, checkoutType, request, orderId, createdAt), ct);
        await PublishEventSafelyAsync(CreatePaymentRequestedEvent(flowId, checkoutType, request, orderId, paymentId, createdAt), ct);
        await PublishEventSafelyAsync(CreatePaymentSuccessEvent(flowId, checkoutType, request, orderId, paymentId, createdAt), ct);
        await PublishEventSafelyAsync(CreateProviderRequestedEvent(flowId, checkoutType, request, orderId, createdAt), ct);
        await PublishEventSafelyAsync(CreateEsimActivatedEvent(flowId, checkoutType, request, orderId, createdAt), ct);
        await PublishEventSafelyAsync(CreateEmailSentEvent(flowId, checkoutType, request, orderId, createdAt), ct);
    }

    /// <summary>
    /// Safely publishes a single event to Kafka, catching and logging any exceptions.
    /// Ensures one failed event does not stop the entire flow from publishing.
    /// </summary>
    /// <param name="message">The log event message to publish.</param>
    /// <param name="ct">Cancellation token.</param>
    private async Task PublishEventSafelyAsync(LogEventMessage message, CancellationToken ct)
    {
        try
        {
            await _producer.PublishAsync(message, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish business action event. EventId={EventId}, FlowId={FlowId}, ActionType={ActionType}",
                message.EventId,
                message.FlowId,
                message.ActionType);
        }
    }

    private static LogEventMessage CreateOrderCreatedEvent(
        string flowId,
        string checkoutType,
        CheckoutEsimRequest request,
        string orderId,
        DateTime createdAt)
    {
        return new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = flowId,
            FlowType = FlowTypes.CheckoutEsim,
            ServiceName = "OrderService",
            ActionType = ActionTypes.OrderCreated,
            Status = StatusTypes.Success,
            CreatedAt = createdAt,
            CheckoutType = checkoutType,
            UserId = null,
            CustomerEmail = request.CustomerEmail,
            CustomerPhone = request.CustomerPhone,
            OrderId = orderId,
            PaymentId = null,
            Message = "Order created successfully"
        };
    }

    private static LogEventMessage CreatePaymentRequestedEvent(
        string flowId,
        string checkoutType,
        CheckoutEsimRequest request,
        string orderId,
        string? paymentId,
        DateTime createdAt)
    {
        return new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = flowId,
            FlowType = FlowTypes.CheckoutEsim,
            ServiceName = "PaymentService",
            ActionType = ActionTypes.PaymentRequested,
            Status = StatusTypes.Success,
            CreatedAt = createdAt.AddMilliseconds(10),
            CheckoutType = checkoutType,
            UserId = null,
            CustomerEmail = request.CustomerEmail,
            CustomerPhone = request.CustomerPhone,
            OrderId = orderId,
            PaymentId = paymentId,
            Message = "Payment requested successfully"
        };
    }

    private static LogEventMessage CreatePaymentSuccessEvent(
        string flowId,
        string checkoutType,
        CheckoutEsimRequest request,
        string orderId,
        string? paymentId,
        DateTime createdAt)
    {
        return new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = flowId,
            FlowType = FlowTypes.CheckoutEsim,
            ServiceName = "PaymentService",
            ActionType = ActionTypes.PaymentSuccess,
            Status = StatusTypes.Success,
            CreatedAt = createdAt.AddMilliseconds(50),
            CheckoutType = checkoutType,
            UserId = null,
            CustomerEmail = request.CustomerEmail,
            CustomerPhone = request.CustomerPhone,
            OrderId = orderId,
            PaymentId = paymentId,
            Message = "Payment successful"
        };
    }

    private static LogEventMessage CreateProviderRequestedEvent(
        string flowId,
        string checkoutType,
        CheckoutEsimRequest request,
        string orderId,
        DateTime createdAt)
    {
        return new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = flowId,
            FlowType = FlowTypes.CheckoutEsim,
            ServiceName = "CoreService",
            ActionType = ActionTypes.ProviderRequested,
            Status = StatusTypes.Success,
            CreatedAt = createdAt.AddMilliseconds(80),
            CheckoutType = checkoutType,
            UserId = null,
            CustomerEmail = request.CustomerEmail,
            CustomerPhone = request.CustomerPhone,
            OrderId = orderId,
            PaymentId = null,
            Message = "Provider requested successfully"
        };
    }

    private static LogEventMessage CreateEsimActivatedEvent(
        string flowId,
        string checkoutType,
        CheckoutEsimRequest request,
        string orderId,
        DateTime createdAt)
    {
        return new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = flowId,
            FlowType = FlowTypes.CheckoutEsim,
            ServiceName = "ProviderService",
            ActionType = ActionTypes.EsimActivated,
            Status = StatusTypes.Success,
            CreatedAt = createdAt.AddMilliseconds(90),
            CheckoutType = checkoutType,
            UserId = null,
            CustomerEmail = request.CustomerEmail,
            CustomerPhone = request.CustomerPhone,
            OrderId = orderId,
            PaymentId = null,
            Message = "eSIM activated successfully"
        };
    }

    private static LogEventMessage CreateEmailSentEvent(
        string flowId,
        string checkoutType,
        CheckoutEsimRequest request,
        string orderId,
        DateTime createdAt)
    {
        return new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = flowId,
            FlowType = FlowTypes.CheckoutEsim,
            ServiceName = "NotificationService",
            ActionType = ActionTypes.EmailSent,
            Status = StatusTypes.Success,
            CreatedAt = createdAt.AddMilliseconds(100),
            CheckoutType = checkoutType,
            UserId = null,
            CustomerEmail = request.CustomerEmail,
            CustomerPhone = request.CustomerPhone,
            OrderId = orderId,
            PaymentId = null,
            Message = "Confirmation email sent successfully"
        };
    }
}
