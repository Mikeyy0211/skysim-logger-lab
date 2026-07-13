using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Skysim.Logger.Client.Producers;
using Xunit;
using LogEventMessage = Skysim.Logger.Contracts.Events.LogEventMessage;
using StatusTypes = Skysim.Logger.Contracts.Constants.StatusTypes;
using FlowTypes = Skysim.Logger.Contracts.Constants.FlowTypes;
using ActionTypes = Skysim.Logger.Contracts.Constants.ActionTypes;

namespace Skysim.Logger.Api.Tests.Client.Producers;

public class KafkaLogProducerTests
{
    private static readonly DateTime BaseTime = new(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc);

    private static LogEventMessage CreateMessage(string flowId = "flow-123", string flowType = FlowTypes.CheckoutEsim)
    {
        return new LogEventMessage
        {
            EventId = Guid.Parse("550e8400-e29b-41d4-a716-446655440000"),
            FlowId = flowId,
            FlowType = flowType,
            ActionType = ActionTypes.OrderCreated,
            Status = StatusTypes.Success,
            ServiceName = "TestService",
            CreatedAt = BaseTime
        };
    }

    [Fact]
    public async Task PublishAsync_ValidMessage_ShouldPreserveExistingServiceName()
    {
        var producer = CreateMockProducer();
        var logger = new Mock<ILogger<KafkaLogProducer>>();

        using var sut = new KafkaLogProducer(
            "localhost:9092",
            "skysim.action.logs",
            "all",
            5,
            200,
            "ConfiguredServiceName",
            logger.Object,
            producer.Object);

        var message = CreateMessage();
        await sut.PublishAsync(message);

        // ServiceName should be preserved from the message, not overwritten by config
        message.ServiceName.Should().Be("TestService");
    }

    [Fact]
    public async Task PublishAsync_NullServiceName_ShouldSetFromConfig()
    {
        var producer = CreateMockProducer();
        var logger = new Mock<ILogger<KafkaLogProducer>>();

        using var sut = new KafkaLogProducer(
            "localhost:9092",
            "skysim.action.logs",
            "all",
            5,
            200,
            "ConfiguredServiceName",
            logger.Object,
            producer.Object);

        var message = CreateMessage();
        message.ServiceName = null!;
        await sut.PublishAsync(message);

        message.ServiceName.Should().Be("ConfiguredServiceName");
    }

    [Fact]
    public async Task PublishAsync_EmptyServiceName_ShouldSetFromConfig()
    {
        var producer = CreateMockProducer();
        var logger = new Mock<ILogger<KafkaLogProducer>>();

        using var sut = new KafkaLogProducer(
            "localhost:9092",
            "skysim.action.logs",
            "all",
            5,
            200,
            "ConfiguredServiceName",
            logger.Object,
            producer.Object);

        var message = CreateMessage();
        message.ServiceName = string.Empty;
        await sut.PublishAsync(message);

        message.ServiceName.Should().Be("ConfiguredServiceName");
    }

    [Fact]
    public async Task PublishAsync_WhitespaceServiceName_ShouldSetFromConfig()
    {
        var producer = CreateMockProducer();
        var logger = new Mock<ILogger<KafkaLogProducer>>();

        using var sut = new KafkaLogProducer(
            "localhost:9092",
            "skysim.action.logs",
            "all",
            5,
            200,
            "ConfiguredServiceName",
            logger.Object,
            producer.Object);

        var message = CreateMessage();
        message.ServiceName = "   ";
        await sut.PublishAsync(message);

        message.ServiceName.Should().Be("ConfiguredServiceName");
    }

    [Fact]
    public async Task PublishAsync_WithFlowId_ShouldUseFlowIdAsKey()
    {
        var producer = CreateMockProducer();
        var logger = new Mock<ILogger<KafkaLogProducer>>();

        using var sut = new KafkaLogProducer(
            "localhost:9092",
            "skysim.action.logs",
            "all",
            5,
            200,
            "TestService",
            logger.Object,
            producer.Object);

        var message = CreateMessage("my-flow-id");
        await sut.PublishAsync(message);

        producer.Verify(p => p.ProduceAsync(
            "skysim.action.logs",
            It.Is<Message<string, byte[]>>(m => m.Key == "my-flow-id"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WithoutFlowId_ShouldUseEventIdAsKey()
    {
        var producer = CreateMockProducer();
        var logger = new Mock<ILogger<KafkaLogProducer>>();

        using var sut = new KafkaLogProducer(
            "localhost:9092",
            "skysim.action.logs",
            "all",
            5,
            200,
            "TestService",
            logger.Object,
            producer.Object);

        var message = CreateMessage(string.Empty);
        await sut.PublishAsync(message);

        producer.Verify(p => p.ProduceAsync(
            "skysim.action.logs",
            It.Is<Message<string, byte[]>>(m => m.Key == "550e8400-e29b-41d4-a716-446655440000"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ProduceFailure_ShouldRetryAndLogWarning()
    {
        var producer = CreateMockFailingProducer();
        var logger = new Mock<ILogger<KafkaLogProducer>>();

        using var sut = new KafkaLogProducer(
            "localhost:9092",
            "skysim.action.logs",
            "all",
            2,
            50,
            "TestService",
            logger.Object,
            producer.Object);

        var message = CreateMessage();
        await sut.PublishAsync(message);

        producer.Verify(p => p.ProduceAsync(
            It.IsAny<string>(),
            It.IsAny<Message<string, byte[]>>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
        logger.Verify(l => l.Log(
            LogLevel.Warning,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Kafka produce failed")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task PublishAsync_ProduceFailure_ShouldNotPropagateException()
    {
        var producer = CreateMockFailingProducer();
        var logger = new Mock<ILogger<KafkaLogProducer>>();

        using var sut = new KafkaLogProducer(
            "localhost:9092",
            "skysim.action.logs",
            "all",
            5,
            200,
            "TestService",
            logger.Object,
            producer.Object);

        var message = CreateMessage();
        var act = async () => await sut.PublishAsync(message);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Dispose_ShouldFlushAndNotThrow()
    {
        var producer = CreateMockProducer();
        var logger = new Mock<ILogger<KafkaLogProducer>>();

        using var sut = new KafkaLogProducer(
            "localhost:9092",
            "skysim.action.logs",
            "all",
            5,
            200,
            "TestService",
            logger.Object,
            producer.Object);

        var act = () => sut.Dispose();

        act.Should().NotThrow();
        producer.Verify(p => p.Flush(It.IsAny<TimeSpan>()), Times.Once);
        producer.Verify(p => p.Dispose(), Times.Once);
    }

    private static Mock<IProducer<string, byte[]>> CreateMockProducer()
    {
        var mock = new Mock<IProducer<string, byte[]>>();
        mock.Setup(p => p.ProduceAsync(
            It.IsAny<string>(),
            It.IsAny<Message<string, byte[]>>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, byte[]>
            {
                Topic = "skysim.action.logs",
                Partition = new Partition(0),
                Offset = new Offset(0)
            });
        return mock;
    }

    private static Mock<IProducer<string, byte[]>> CreateMockFailingProducer()
    {
        var mock = new Mock<IProducer<string, byte[]>>();
        mock.Setup(p => p.ProduceAsync(
            It.IsAny<string>(),
            It.IsAny<Message<string, byte[]>>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProduceException<string, byte[]>(
                new Error(ErrorCode.Local_Transport, "Simulated failure"),
                deliveryResult: null));
        return mock;
    }
}
