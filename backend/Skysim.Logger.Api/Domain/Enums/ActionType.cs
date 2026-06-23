using System.Text.Json.Serialization;

namespace Skysim.Logger.Api.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
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
    EmailFailed
}
