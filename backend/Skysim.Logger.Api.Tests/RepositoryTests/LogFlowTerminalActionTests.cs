using FluentAssertions;
using Skysim.Logger.Api.Domain.Services;
using Xunit;
using Status = Skysim.Logger.Contracts.Constants.Status;
using ActionType = Skysim.Logger.Contracts.Constants.ActionType;

namespace Skysim.Logger.Api.Tests.RepositoryTests;

public class LogFlowTerminalActionTests
{
    [Theory]
    [InlineData(ActionType.OrderFailed, Status.Failed, true)]
    [InlineData(ActionType.PaymentFailed, Status.Failed, true)]
    [InlineData(ActionType.ProviderFailed, Status.Failed, true)]
    [InlineData(ActionType.EsimActivationFailed, Status.Failed, true)]
    [InlineData(ActionType.EmailFailed, Status.Failed, true)]
    [InlineData(ActionType.EsimActivated, Status.Success, true)]
    [InlineData(ActionType.EsimActivated, Status.Failed, true)]
    [InlineData(ActionType.EsimActivated, Status.InProgress, true)]
    [InlineData(ActionType.OrderCreated, Status.InProgress, false)]
    [InlineData(ActionType.PaymentRequested, Status.InProgress, false)]
    [InlineData(ActionType.PaymentSuccess, Status.Success, false)]
    [InlineData(ActionType.ProviderRequested, Status.InProgress, false)]
    [InlineData(ActionType.EmailSent, Status.Success, false)]
    public void IsTerminalAction_ShouldReturnExpectedResult(ActionType actionType, Status status, bool expectedTerminal)
    {
        var isTerminal = FlowDomainService.IsTerminalAction(actionType, status);
        isTerminal.Should().Be(expectedTerminal);
    }
}
