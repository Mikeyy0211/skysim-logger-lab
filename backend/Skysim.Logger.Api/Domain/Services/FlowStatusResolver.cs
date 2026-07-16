using StatusTypes = Skysim.Logger.Contracts.Constants.StatusTypes;

namespace Skysim.Logger.Api.Domain.Services;

/// <summary>
/// Resolves the aggregate status of a flow from its accumulated action counts
/// and the terminal state of the current action.
/// </summary>
public static class FlowStatusResolver
{
    public static string ResolveFlowStatus(
        int successSteps,
        int failedSteps,
        bool isTerminalSuccess)
    {
        if (failedSteps > 0)
        {
            return successSteps > 0
                ? StatusTypes.PartialFailed
                : StatusTypes.Failed;
        }

        return isTerminalSuccess
            ? StatusTypes.Success
            : StatusTypes.Running;
    }
}
