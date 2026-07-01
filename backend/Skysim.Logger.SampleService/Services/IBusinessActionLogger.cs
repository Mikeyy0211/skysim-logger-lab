using Skysim.Logger.SampleService.DTOs;

namespace Skysim.Logger.SampleService.Services;

/// <summary>
/// Defines the contract for publishing business action logs to Kafka.
/// </summary>
public interface IBusinessActionLogger
{
    /// <summary>
    /// Publishes a complete eSIM checkout flow with all business action events to Kafka.
    /// </summary>
    /// <param name="request">The checkout request containing customer and package details.</param>
    /// <param name="flowId">The unique flow identifier for correlation.</param>
    /// <param name="checkoutType">The checkout type (Guest or Authenticated).</param>
    /// <param name="orderId">The generated order identifier.</param>
    /// <param name="paymentId">The generated payment identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task PublishCheckoutFlowAsync(
        CheckoutEsimRequest request,
        string flowId,
        string checkoutType,
        string orderId,
        string? paymentId,
        CancellationToken ct = default);
}
