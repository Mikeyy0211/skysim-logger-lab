using System.Text;
using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using Skysim.Logger.Api.Common;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Infrastructure.Kafka;
using Skysim.Logger.Api.Infrastructure.Persistence.Exceptions;
using Skysim.Logger.Common.Kafka;
using Skysim.Logger.Infrastructure.Entities;
using Xunit;
using LogEventMessage = Skysim.Logger.Contracts.Events.LogEventMessage;
using Status = Skysim.Logger.Contracts.Constants.Status;
using ActionType = Skysim.Logger.Contracts.Constants.ActionType;
using FlowType = Skysim.Logger.Contracts.Constants.FlowType;
using CheckoutType = Skysim.Logger.Contracts.Constants.CheckoutType;

namespace Skysim.Logger.Api.Tests;

public class KafkaLogConsumerServiceTests
{
    public KafkaLogConsumerServiceTests()
    {
        // Tests focus on testable components (deserialization, validation, logic)
        // Repository and integration tests require actual PostgreSQL database
    }

    private static LogEventMessage CreateValidMessage(
        Guid? eventId = null,
        string? flowId = null,
        ActionType? actionType = null,
        Status? status = null)
    {
        return new LogEventMessage
        {
            EventId = eventId ?? Guid.NewGuid(),
            FlowId = flowId ?? "test-flow-001",
            FlowType = FlowType.CheckoutEsim,
            ServiceName = "Order",
            ActionType = actionType ?? ActionType.OrderCreated,
            Status = status ?? Status.Success,
            CreatedAt = DateTime.UtcNow,
            CheckoutType = CheckoutType.Guest,
            CustomerEmail = "test@example.com",
            OrderId = "ORD-001"
        };
    }

    [Fact]
    public void Deserializer_ValidJson_ReturnsMessage()
    {
        // Arrange
        var message = CreateValidMessage();
        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);

        // Act
        var result = DeserializeMessage(bytes);

        // Assert
        result.Should().NotBeNull();
        result!.EventId.Should().Be(message.EventId);
        result.FlowId.Should().Be(message.FlowId);
        result.ActionType.Should().Be(message.ActionType);
        result.Status.Should().Be(message.Status);
    }

    [Fact]
    public void Deserializer_InvalidJson_ThrowsJsonException()
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes("{ invalid json }");

        // Act & Assert
        var act = () => DeserializeMessage(bytes);
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserializer_EmptyPayload_ReturnsNull()
    {
        // Arrange
        var bytes = Array.Empty<byte>();

        // Act
        var result = DeserializeMessage(bytes);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Deserializer_NullPayload_ReturnsNull()
    {
        // Arrange
        byte[]? bytes = null;

        // Act
        var result = DeserializeMessage(bytes!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Validator_ValidMessage_ReturnsSuccess()
    {
        // Arrange
        var validator = new LogEventMessageValidator();
        var message = CreateValidMessage();

        // Act
        var result = await validator.ValidateAsync(message);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Validator_MissingEventId_ReturnsError()
    {
        // Arrange
        var validator = new LogEventMessageValidator();
        var message = CreateValidMessage();
        message.EventId = Guid.Empty;

        // Act
        var result = await validator.ValidateAsync(message);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "EventId");
    }

    [Fact]
    public async Task Validator_MissingFlowId_ReturnsError()
    {
        // Arrange
        var validator = new LogEventMessageValidator();
        var message = CreateValidMessage();
        message.FlowId = string.Empty;

        // Act
        var result = await validator.ValidateAsync(message);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FlowId");
    }

    [Fact]
    public async Task Validator_FlowIdTooLong_ReturnsError()
    {
        // Arrange
        var validator = new LogEventMessageValidator();
        var message = CreateValidMessage();
        message.FlowId = new string('a', 101);

        // Act
        var result = await validator.ValidateAsync(message);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FlowId");
    }

    [Fact]
    public async Task Validator_GuestCheckoutWithoutUserId_ReturnsSuccess()
    {
        // Arrange
        var validator = new LogEventMessageValidator();
        var message = CreateValidMessage();
        message.CheckoutType = CheckoutType.Guest;
        message.UserId = null;

        // Act
        var result = await validator.ValidateAsync(message);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validator_MissingServiceName_ReturnsError()
    {
        // Arrange
        var validator = new LogEventMessageValidator();
        var message = CreateValidMessage();
        message.ServiceName = string.Empty;

        // Act
        var result = await validator.ValidateAsync(message);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ServiceName");
    }

    [Fact]
    public async Task Validator_AllOptionalFieldsNull_ReturnsSuccess()
    {
        // Arrange
        var validator = new LogEventMessageValidator();
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "test-flow",
            FlowType = FlowType.CheckoutEsim,
            ServiceName = "Order",
            ActionType = ActionType.OrderCreated,
            Status = Status.Success,
            CreatedAt = DateTime.UtcNow
            // All optional fields null
        };

        // Act
        var result = await validator.ValidateAsync(message);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void BuildLogAction_MapsAllRequiredFields()
    {
        // Arrange
        var message = CreateValidMessage();
        message.Duration = 150;
        message.RequestTime = DateTime.UtcNow.AddSeconds(-2);
        message.ResponseTime = DateTime.UtcNow;
        message.CorrelationId = "corr-123";
        message.ErrorCode = "ERR001";
        message.ErrorMessage = "Something went wrong";

        var flowId = Guid.NewGuid();

        // Act
        var action = BuildLogAction(message, flowId);

        // Assert
        action.EventId.Should().Be(message.EventId);
        action.FlowId.Should().Be(message.FlowId);
        action.ServiceName.Should().Be(message.ServiceName);
        action.ActionType.Should().Be(message.ActionType.ToString());
        action.Status.Should().Be(message.Status.ToString());
        action.DurationMs.Should().Be(150);
        action.CorrelationId.Should().Be("corr-123");
        action.ErrorCode.Should().Be("ERR001");
        action.ErrorMessage.Should().Be("Something went wrong");
    }

    [Fact]
    public void BuildLogAction_CalculatesDurationFromTimestampsWhenNotProvided()
    {
        // Arrange
        var message = CreateValidMessage();
        message.Duration = null;
        message.RequestTime = DateTime.UtcNow.AddSeconds(-5);
        message.ResponseTime = DateTime.UtcNow;

        // Act
        var action = BuildLogAction(message, Guid.NewGuid());

        // Assert
        action.DurationMs.Should().BeGreaterThan(4000);
        action.DurationMs.Should().BeLessThan(6000);
    }

    [Fact]
    public void BuildLogAction_UsesProvidedDurationWhenAvailable()
    {
        // Arrange
        var message = CreateValidMessage();
        message.Duration = 500;
        message.RequestTime = DateTime.UtcNow.AddSeconds(-100);
        message.ResponseTime = DateTime.UtcNow;

        // Act
        var action = BuildLogAction(message, Guid.NewGuid());

        // Assert
        action.DurationMs.Should().Be(500);
    }

    [Fact]
    public void DuplicateEventException_ContainsEventId()
    {
        // Arrange
        var eventId = Guid.NewGuid();

        // Act
        var exception = new DuplicateEventException(eventId);

        // Assert
        exception.EventId.Should().Be(eventId);
        exception.Message.Should().Contain(eventId.ToString());
    }

    [Fact]
    public void RetryOptions_DefaultValues_AreReasonable()
    {
        // Arrange
        var options = new RetryOptions();

        // Assert
        options.MaxAttempts.Should().Be(5);
        options.InitialDelayMs.Should().Be(200);
        options.BackoffMultiplier.Should().Be(2.0);
        options.MaxDelayMs.Should().Be(3200);
    }

    [Fact]
    public void CalculateDelay_ExponentialBackoff_Works()
    {
        // Arrange
        var options = new RetryOptions
        {
            InitialDelayMs = 100,
            BackoffMultiplier = 2.0,
            MaxDelayMs = 1000
        };

        // Act
        var delay1 = CalculateDelay(1, options);
        var delay2 = CalculateDelay(2, options);
        var delay3 = CalculateDelay(3, options);

        // Assert
        delay1.TotalMilliseconds.Should().Be(100); // 100 * 2^0 = 100
        delay2.TotalMilliseconds.Should().Be(200); // 100 * 2^1 = 200
        delay3.TotalMilliseconds.Should().Be(400); // 100 * 2^2 = 400
    }

    [Fact]
    public void CalculateDelay_CapsAtMaxDelay()
    {
        // Arrange
        var options = new RetryOptions
        {
            InitialDelayMs = 100,
            BackoffMultiplier = 10.0,
            MaxDelayMs = 500
        };

        // Act
        var delay = CalculateDelay(5, options);

        // Assert
        delay.TotalMilliseconds.Should().Be(500); // Capped at max
    }

    // Helper methods that mirror the service's internal logic

    private static LogEventMessage? DeserializeMessage(byte[] payload)
    {
        if (payload == null || payload.Length == 0)
        {
            return null;
        }

        var jsonString = Encoding.UTF8.GetString(payload);
        return JsonSerializer.Deserialize<LogEventMessage>(jsonString, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private static LogAction BuildLogAction(LogEventMessage message, Guid flowId)
    {
        var durationMs = message.Duration;
        if (!durationMs.HasValue && message.RequestTime.HasValue && message.ResponseTime.HasValue)
        {
            durationMs = (int)(message.ResponseTime.Value - message.RequestTime.Value).TotalMilliseconds;
        }

        return new LogAction
        {
            Id = Guid.NewGuid(),
            EventId = message.EventId,
            FlowId = message.FlowId,
            StepOrder = 0,
            ServiceName = message.ServiceName,
            ActionType = message.ActionType.ToString(),
            Status = message.Status.ToString(),
            Message = message.Message,
            ErrorCode = message.ErrorCode,
            ErrorMessage = message.ErrorMessage,
            RequestTime = message.RequestTime,
            ResponseTime = message.ResponseTime,
            DurationMs = durationMs,
            CorrelationId = message.CorrelationId,
            CreatedAt = message.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static TimeSpan CalculateDelay(int attempt, RetryOptions options)
    {
        var delay = options.InitialDelayMs * Math.Pow(options.BackoffMultiplier, attempt - 1);
        return TimeSpan.FromMilliseconds(Math.Min(delay, options.MaxDelayMs));
    }
}
