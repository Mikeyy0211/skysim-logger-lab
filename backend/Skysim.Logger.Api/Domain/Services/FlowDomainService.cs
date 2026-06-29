using ActionTypes = Skysim.Logger.Contracts.Constants.ActionTypes;
using StatusTypes = Skysim.Logger.Contracts.Constants.StatusTypes;

namespace Skysim.Logger.Api.Domain.Services;

/// <summary>
/// Domain service for flow-related business logic and validation rules.
/// </summary>
public static class FlowDomainService
{
    private static readonly HashSet<string> TerminalActionTypes =
    [
        ActionTypes.OrderFailed,
        ActionTypes.PaymentFailed,
        ActionTypes.ProviderFailed,
        ActionTypes.EsimActivationFailed,
        ActionTypes.EmailFailed,
        ActionTypes.EsimActivated
    ];

    /// <summary>
    /// Determines whether an action is considered a terminal action in a flow.
    /// A terminal action marks the end of a flow, either as a success or failure.
    /// </summary>
    /// <param name="actionType">The type of the action.</param>
    /// <param name="status">The status of the action.</param>
    /// <returns>True if the action is terminal; otherwise, false.</returns>
    public static bool IsTerminalAction(string actionType, string status)
    {
        if (TerminalActionTypes.Contains(actionType))
        {
            return true;
        }

        if (status == StatusTypes.Failed)
        {
            return true;
        }

        return false;
    }
}
