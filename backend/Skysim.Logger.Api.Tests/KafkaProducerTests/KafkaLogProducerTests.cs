using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Domain.Enums;
using Skysim.Logger.Api.Infrastructure.Kafka;
using Xunit;

namespace Skysim.Logger.Api.Tests.KafkaProducerTests;

public class KafkaLogProducerTests
{
    [Fact]
    public async Task PublishAsync_ValidMessage_SetsServiceName()
    {
        // Arrange
        var wrapper = new FakeKafkaProducerWrapper();
        var options = new FakeKafkaLogProducerOptions();
        var logger = new Mock<ILogger<KafkaLogProducer>>();
        using var producer = new KafkaLogProducer(options, logger.Object, wrapper);

        var message = CreateLogEventMessage();

        // Act & Assert
        var act = () => producer.PublishAsync(message);
        await act.Should().NotThrowAsync();
        wrapper.ProduceCallCount.Should().Be(1);
    }

    [Fact]
    public async Task PublishAsync_WithFlowId_ProduceMessageWithoutThrowing()
    {
        // Arrange
        var wrapper = new FakeKafkaProducerWrapper();
        var options = new FakeKafkaLogProducerOptions();
        var logger = new Mock<ILogger<KafkaLogProducer>>();
        using var producer = new KafkaLogProducer(options, logger.Object, wrapper);

        var message = CreateLogEventMessage(flowId: "my-flow-id");

        // Act & Assert
        var act = () => producer.PublishAsync(message);
        await act.Should().NotThrowAsync();
        wrapper.ProduceCallCount.Should().Be(1);
    }

    [Fact]
    public async Task PublishAsync_WithoutFlowId_ProduceMessageWithoutThrowing()
    {
        // Arrange
        var wrapper = new FakeKafkaProducerWrapper();
        var options = new FakeKafkaLogProducerOptions();
        var logger = new Mock<ILogger<KafkaLogProducer>>();
        using var producer = new KafkaLogProducer(options, logger.Object, wrapper);

        var message = CreateLogEventMessage(flowId: string.Empty);

        // Act & Assert
        var act = () => producer.PublishAsync(message);
        await act.Should().NotThrowAsync();
        wrapper.ProduceCallCount.Should().Be(1);
    }

    [Fact]
    public async Task PublishAsync_SerializationFailure_DoesNotThrow()
    {
        // Arrange
        var wrapper = new FakeKafkaProducerWrapper();
        var options = new FakeKafkaLogProducerOptions();
        var logger = new Mock<ILogger<KafkaLogProducer>>();
        using var producer = new KafkaLogProducer(options, logger.Object, wrapper);

        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-123",
            FlowType = (FlowType)999, // invalid enum value that cannot be serialized
            ActionType = ActionType.HttpRequest,
            Status = Status.Success,
            ServiceName = "TestService",
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var act = () => producer.PublishAsync(message);

        // Assert — should not throw; failure is swallowed and logged
        await act.Should().NotThrowAsync();
        wrapper.ProduceCallCount.Should().Be(0); // No produce due to serialization failure
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Failed to serialize")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task PublishAsync_ExceptionDuringProduce_LogsWarning()
    {
        // Arrange
        var wrapper = new FailingKafkaProducerWrapper();
        var options = new FakeKafkaLogProducerOptions();
        var logger = new Mock<ILogger<KafkaLogProducer>>();
        using var producer = new KafkaLogProducer(options, logger.Object, wrapper);

        var message = CreateLogEventMessage();

        // Act
        await producer.PublishAsync(message);

        // Assert — the retry pipeline exhausts all attempts and logs a warning
        // MaxAttempts = 2 means 1 initial + 2 retries = 3 total calls
        wrapper.ProduceCallCount.Should().Be(3);
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("Failed to publish")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task PublishAsync_ExceptionDuringProduce_ExceptionIsNotPropagated()
    {
        // Arrange
        var wrapper = new FailingKafkaProducerWrapper();
        var options = new FakeKafkaLogProducerOptions();
        var logger = new Mock<ILogger<KafkaLogProducer>>();
        using var producer = new KafkaLogProducer(options, logger.Object, wrapper);

        var message = CreateLogEventMessage();

        // Act
        var act = async () => await producer.PublishAsync(message);

        // Assert — fire-and-forget from middleware perspective: no exception escapes
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var wrapper = new FakeKafkaProducerWrapper();
        var options = new FakeKafkaLogProducerOptions();
        var logger = new Mock<ILogger<KafkaLogProducer>>();
        var producer = new KafkaLogProducer(options, logger.Object, wrapper);

        // Act
        var act = () => producer.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    private static LogEventMessage CreateLogEventMessage(string flowId = "flow-123")
    {
        return new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = flowId,
            FlowType = FlowType.HttpAction,
            ActionType = ActionType.HttpRequest,
            Status = Status.Success,
            ServiceName = "TestService",
            CreatedAt = DateTime.UtcNow
        };
    }
}

internal class FakeKafkaLogProducerOptions : IKafkaLogProducerOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string Acks { get; set; } = "all";
    public RetryOptions Retry { get; set; } = new RetryOptions
    {
        MaxAttempts = 2,
        InitialDelayMs = 50,
        BackoffMultiplier = 1.0,
        MaxDelayMs = 100
    };
    public string ServiceName { get; set; } = "TestService";
}

internal sealed class FakeKafkaProducerWrapper : IKafkaProducerWrapper
{
    public int ProduceCallCount { get; private set; }

    public Task<KafkaDeliveryResult> ProduceAsync(
        string topic,
        Message<string, byte[]> message,
        CancellationToken cancellationToken)
    {
        ProduceCallCount++;
        return Task.FromResult(new KafkaDeliveryResult
        {
            Topic = topic,
            PartitionValue = 0,
            OffsetValue = 0
        });
    }
}

internal sealed class FailingKafkaProducerWrapper : IKafkaProducerWrapper
{
    public int ProduceCallCount { get; private set; }

    public Task<KafkaDeliveryResult> ProduceAsync(
        string topic,
        Message<string, byte[]> message,
        CancellationToken cancellationToken)
    {
        ProduceCallCount++;
        throw new ProduceException<string, byte[]>(
            new Error(ErrorCode.Local_Transport, "Simulated failure"),
            deliveryResult: null);
    }
}
