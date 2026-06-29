using ActionType = Skysim.Logger.Contracts.Constants.ActionType;
using Status = Skysim.Logger.Contracts.Constants.Status;

namespace Skysim.Logger.Api.Domain.Services;

/// <summary>
/// Domain service for flow-related business logic and validation rules.
/// </summary>
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

    /// <summary>
    /// Determines whether an action is considered a terminal action in a flow.
    /// A terminal action marks the end of a flow, either as a success or failure.
    /// </summary>
    /// <param name="actionType">The type of the action.</param>
    /// <param name="status">The status of the action.</param>
    /// <returns>True if the action is terminal; otherwise, false.</returns>
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
