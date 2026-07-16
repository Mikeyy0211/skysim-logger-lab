using FluentAssertions;
using Skysim.Logger.Api.Consumers;
using Skysim.Logger.Contracts.Events;
using Skysim.Logger.Infrastructure.Entities;
using Xunit;
using ActionTypes = Skysim.Logger.Contracts.Constants.ActionTypes;
using FlowTypes = Skysim.Logger.Contracts.Constants.FlowTypes;
using StatusTypes = Skysim.Logger.Contracts.Constants.StatusTypes;

namespace Skysim.Logger.Api.Tests.Consumers;

public class KafkaLogConsumerFlowStatusTests
{
    [Fact]
    public void NonTerminalSuccess_SetsRunning()
    {
        var flow = CreateFlow();

        KafkaLogConsumerService.MapFlowFromMessage(
            flow,
            CreateMessage(ActionTypes.OrderCreated, StatusTypes.Success));

        flow.Status.Should().Be(StatusTypes.Running);
        flow.TotalSteps.Should().Be(1);
        flow.SuccessSteps.Should().Be(1);
        flow.CompletedAt.Should().BeNull();
    }

    [Fact]
    public void TerminalSuccess_SetsSuccessAndCompletedAt()
    {
        var flow = CreateFlow();

        KafkaLogConsumerService.MapFlowFromMessage(
            flow,
            CreateMessage(ActionTypes.EsimActivated, StatusTypes.Success));

        flow.Status.Should().Be(StatusTypes.Success);
        flow.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void FailedAction_SetsFailedAndCompletedAt()
    {
        var flow = CreateFlow();

        KafkaLogConsumerService.MapFlowFromMessage(
            flow,
            CreateMessage(ActionTypes.PaymentFailed, StatusTypes.Failed));

        flow.Status.Should().Be(StatusTypes.Failed);
        flow.FailedSteps.Should().Be(1);
        flow.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void SuccessThenFailure_SetsPartialFailed()
    {
        var flow = CreateFlow();

        KafkaLogConsumerService.MapFlowFromMessage(
            flow,
            CreateMessage(ActionTypes.OrderCreated, StatusTypes.Success));
        KafkaLogConsumerService.MapFlowFromMessage(
            flow,
            CreateMessage(ActionTypes.PaymentFailed, StatusTypes.Failed));

        flow.Status.Should().Be(StatusTypes.PartialFailed);
        flow.SuccessSteps.Should().Be(1);
        flow.FailedSteps.Should().Be(1);
        flow.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void FailureThenSuccess_DoesNotReturnToSuccess()
    {
        var flow = CreateFlow();

        KafkaLogConsumerService.MapFlowFromMessage(
            flow,
            CreateMessage(ActionTypes.PaymentFailed, StatusTypes.Failed));
        KafkaLogConsumerService.MapFlowFromMessage(
            flow,
            CreateMessage(ActionTypes.EsimActivated, StatusTypes.Success));

        flow.Status.Should().Be(StatusTypes.PartialFailed);
        flow.SuccessSteps.Should().Be(1);
        flow.FailedSteps.Should().Be(1);
    }

    [Fact]
    public void NullBusinessFields_DoNotOverwriteExistingValues()
    {
        var flow = CreateFlow();
        flow.CustomerEmail = "customer@example.com";
        flow.UserEmail = "user@example.com";

        KafkaLogConsumerService.MapFlowFromMessage(
            flow,
            CreateMessage(ActionTypes.PaymentSuccess, StatusTypes.Success));

        flow.CustomerEmail.Should().Be("customer@example.com");
        flow.UserEmail.Should().Be("user@example.com");
    }

    [Fact]
    public void UserEmailAndCustomerEmail_AreMergedIndependently()
    {
        var flow = CreateFlow();
        flow.CustomerEmail = "customer@example.com";

        var message = CreateMessage(ActionTypes.PaymentSuccess, StatusTypes.Success);
        message.UserEmail = "user@example.com";
        message.CustomerEmail = null;

        KafkaLogConsumerService.MapFlowFromMessage(flow, message);

        flow.CustomerEmail.Should().Be("customer@example.com");
        flow.UserEmail.Should().Be("user@example.com");
    }

    private static LogFlow CreateFlow()
    {
        return new LogFlow
        {
            Id = Guid.NewGuid(),
            FlowId = "test-flow",
            FlowType = FlowTypes.CheckoutEsim,
            Status = StatusTypes.Running,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static LogEventMessage CreateMessage(string actionType, string status)
    {
        return new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "test-flow",
            FlowType = FlowTypes.CheckoutEsim,
            ServiceName = "TestService",
            ActionType = actionType,
            Status = status,
            CreatedAt = DateTime.UtcNow
        };
    }
}
