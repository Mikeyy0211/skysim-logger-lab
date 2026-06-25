using Skysim.Logger.Api.Domain.Enums;

namespace Skysim.Logger.Api.Domain.Services;

public static class FlowDomainService
{
    private static readonly HashSet<ActionType> TerminalActionTypes =
    [
        ActionType.OrderFailed,
        ActionType.PaymentFailed,
        ActionType.ProviderFailed,
        ActionType.EsimActivationFailed,
        ActionType.EmailFailed,
        ActionType.EsimActivated
    ];

    public static bool IsTerminalAction(ActionType actionType, Status status)
    {
        if (TerminalActionTypes.Contains(actionType))
        {
            return true;
        }

        if (status == Status.Failed)
        {
            return true;
        }

        return false;
    }
}
