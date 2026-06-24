namespace Skysim.Logger.Api.Domain.Enums;

public enum ActionType
{
    OrderCreated,
    PaymentRequested,
    PaymentSuccess,
    ProviderRequested,
    EsimActivated,
    EmailSent,
    OrderFailed,
    PaymentFailed,
    ProviderFailed,
    EsimActivationFailed,
    EmailFailed,
    HttpRequest
}
