using FluentAssertions;
using Skysim.Logger.Api.Domain.Services;
using Xunit;
using StatusTypes = Skysim.Logger.Contracts.Constants.StatusTypes;
using ActionTypes = Skysim.Logger.Contracts.Constants.ActionTypes;

namespace Skysim.Logger.Api.Tests.RepositoryTests;

public class LogFlowTerminalActionTests
{
    [Theory]
    [InlineData(ActionTypes.OrderFailed, StatusTypes.Failed, true)]
    [InlineData(ActionTypes.PaymentFailed, StatusTypes.Failed, true)]
    [InlineData(ActionTypes.ProviderFailed, StatusTypes.Failed, true)]
    [InlineData(ActionTypes.EsimActivationFailed, StatusTypes.Failed, true)]
    [InlineData(ActionTypes.EmailFailed, StatusTypes.Failed, true)]
    [InlineData(ActionTypes.EsimActivated, StatusTypes.Success, true)]
    [InlineData(ActionTypes.EsimActivated, StatusTypes.Failed, true)]
    [InlineData(ActionTypes.EsimActivated, StatusTypes.InProgress, true)]
    [InlineData(ActionTypes.OrderCreated, StatusTypes.InProgress, false)]
    [InlineData(ActionTypes.PaymentRequested, StatusTypes.InProgress, false)]
    [InlineData(ActionTypes.PaymentSuccess, StatusTypes.Success, false)]
    [InlineData(ActionTypes.ProviderRequested, StatusTypes.InProgress, false)]
    [InlineData(ActionTypes.EmailSent, StatusTypes.Success, false)]
    public void IsTerminalAction_ShouldReturnExpectedResult(string actionType, string status, bool expectedTerminal)
    {
        var isTerminal = FlowDomainService.IsTerminalAction(actionType, status);
        isTerminal.Should().Be(expectedTerminal);
    }
}
