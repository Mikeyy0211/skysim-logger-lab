using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Skysim.Logger.Client.Producers;
using Skysim.Logger.Contracts.Constants;
using Skysim.Logger.SampleService.DTOs;
using Skysim.Logger.SampleService.Services;
using Xunit;
using LogEventMessage = Skysim.Logger.Contracts.Events.LogEventMessage;

namespace Skysim.Logger.SampleService.Tests.Services;

public class BusinessActionLoggerTests
{
    private readonly Mock<IKafkaLogProducer> _producerMock;
    private readonly Mock<ILogger<BusinessActionLogger>> _loggerMock;
    private readonly BusinessActionLogger _sut;
    private readonly List<LogEventMessage> _publishedMessages;

    public BusinessActionLoggerTests()
    {
        _producerMock = new Mock<IKafkaLogProducer>();
        _loggerMock = new Mock<ILogger<BusinessActionLogger>>();
        _publishedMessages = new List<LogEventMessage>();

        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => _publishedMessages.Add(msg))
            .Returns(Task.CompletedTask);

        _sut = new BusinessActionLogger(_producerMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task PublishCheckoutFlowAsync_PublishesAllSixEvents()
    {
        // Arrange
        var request = CreateRequest("test@example.com", "0900123456");
        var flowId = "test-flow-001";
        var checkoutType = CheckoutTypes.Guest;
        var orderId = "ORD-abc123";
        var paymentId = "PAY-xyz789";

        // Act
        await _sut.PublishCheckoutFlowAsync(request, flowId, checkoutType, orderId, paymentId);

        // Assert
        _publishedMessages.Should().HaveCount(6);
    }

    [Fact]
    public async Task PublishCheckoutFlowAsync_UsesSameFlowIdForAllEvents()
    {
        // Arrange
        var request = CreateRequest("test@example.com", "0900123456");
        var flowId = "my-flow-001";
        var checkoutType = CheckoutTypes.Guest;
        var orderId = "ORD-abc123";
        var paymentId = "PAY-xyz789";

        // Act
        await _sut.PublishCheckoutFlowAsync(request, flowId, checkoutType, orderId, paymentId);

        // Assert
        _publishedMessages.Should().OnlyContain(m => m.FlowId == flowId);
    }

    [Fact]
    public async Task PublishCheckoutFlowAsync_SetsFlowTypeToCheckoutEsim()
    {
        // Arrange
        var request = CreateRequest("test@example.com", "0900123456");
        var flowId = "test-flow";
        var checkoutType = CheckoutTypes.Guest;
        var orderId = "ORD-abc123";
        var paymentId = "PAY-xyz789";

        // Act
        await _sut.PublishCheckoutFlowAsync(request, flowId, checkoutType, orderId, paymentId);

        // Assert
        _publishedMessages.Should().OnlyContain(m => m.FlowType == FlowTypes.CheckoutEsim);
    }

    [Fact]
    public async Task PublishCheckoutFlowAsync_SetsCorrectActionTypes()
    {
        // Arrange
        var request = CreateRequest("test@example.com", "0900123456");
        var flowId = "test-flow";
        var checkoutType = CheckoutTypes.Guest;
        var orderId = "ORD-abc123";
        var paymentId = "PAY-xyz789";

        // Act
        await _sut.PublishCheckoutFlowAsync(request, flowId, checkoutType, orderId, paymentId);

        // Assert
        var actionTypes = _publishedMessages.Select(m => m.ActionType).ToList();
        actionTypes.Should().ContainInOrder(
            ActionTypes.OrderCreated,
            ActionTypes.PaymentRequested,
            ActionTypes.PaymentSuccess,
            ActionTypes.ProviderRequested,
            ActionTypes.EsimActivated,
            ActionTypes.EmailSent);
    }

    [Fact]
    public async Task PublishCheckoutFlowAsync_SetsCorrectServiceNames()
    {
        // Arrange
        var request = CreateRequest("test@example.com", "0900123456");
        var flowId = "test-flow";
        var checkoutType = CheckoutTypes.Guest;
        var orderId = "ORD-abc123";
        var paymentId = "PAY-xyz789";

        // Act
        await _sut.PublishCheckoutFlowAsync(request, flowId, checkoutType, orderId, paymentId);

        // Assert
        var serviceNames = _publishedMessages.Select(m => m.ServiceName).ToList();
        serviceNames.Should().ContainInOrder(
            "OrderService",
            "PaymentService",
            "PaymentService",
            "CoreService",
            "ProviderService",
            "NotificationService");
    }

    [Fact]
    public async Task PublishCheckoutFlowAsync_SetsCustomerEmailOnAllEvents()
    {
        // Arrange
        var request = CreateRequest("alice@example.com", "0900123456");
        var flowId = "test-flow";
        var checkoutType = CheckoutTypes.Guest;
        var orderId = "ORD-abc123";
        var paymentId = "PAY-xyz789";

        // Act
        await _sut.PublishCheckoutFlowAsync(request, flowId, checkoutType, orderId, paymentId);

        // Assert
        _publishedMessages.Should().OnlyContain(m => m.CustomerEmail == "alice@example.com");
    }

    [Fact]
    public async Task PublishCheckoutFlowAsync_SetsCustomerPhoneOnAllEvents()
    {
        // Arrange
        var request = CreateRequest("test@example.com", "0912345678");
        var flowId = "test-flow";
        var checkoutType = CheckoutTypes.Guest;
        var orderId = "ORD-abc123";
        var paymentId = "PAY-xyz789";

        // Act
        await _sut.PublishCheckoutFlowAsync(request, flowId, checkoutType, orderId, paymentId);

        // Assert
        _publishedMessages.Should().OnlyContain(m => m.CustomerPhone == "0912345678");
    }

    [Fact]
    public async Task PublishCheckoutFlowAsync_SetsOrderIdOnAllEvents()
    {
        // Arrange
        var request = CreateRequest("test@example.com", "0900123456");
        var flowId = "test-flow";
        var checkoutType = CheckoutTypes.Guest;
        var orderId = "ORD-abc123";
        var paymentId = "PAY-xyz789";

        // Act
        await _sut.PublishCheckoutFlowAsync(request, flowId, checkoutType, orderId, paymentId);

        // Assert
        _publishedMessages.Should().OnlyContain(m => m.OrderId == orderId);
    }

    [Fact]
    public async Task PublishCheckoutFlowAsync_SetsPaymentIdOnlyOnPaymentEvents()
    {
        // Arrange
        var request = CreateRequest("test@example.com", "0900123456");
        var flowId = "test-flow";
        var checkoutType = CheckoutTypes.Guest;
        var orderId = "ORD-abc123";
        var paymentId = "PAY-xyz789";

        // Act
        await _sut.PublishCheckoutFlowAsync(request, flowId, checkoutType, orderId, paymentId);

        // Assert
        var paymentRequested = _publishedMessages.First(m => m.ActionType == ActionTypes.PaymentRequested);
        var paymentSuccess = _publishedMessages.First(m => m.ActionType == ActionTypes.PaymentSuccess);
        var orderCreated = _publishedMessages.First(m => m.ActionType == ActionTypes.OrderCreated);
        var providerRequested = _publishedMessages.First(m => m.ActionType == ActionTypes.ProviderRequested);
        var esimActivated = _publishedMessages.First(m => m.ActionType == ActionTypes.EsimActivated);
        var emailSent = _publishedMessages.First(m => m.ActionType == ActionTypes.EmailSent);

        paymentRequested.PaymentId.Should().Be(paymentId);
        paymentSuccess.PaymentId.Should().Be(paymentId);
        orderCreated.PaymentId.Should().BeNull();
        providerRequested.PaymentId.Should().BeNull();
        esimActivated.PaymentId.Should().BeNull();
        emailSent.PaymentId.Should().BeNull();
    }

    [Theory]
    [InlineData(CheckoutTypes.Guest)]
    [InlineData(CheckoutTypes.Authenticated)]
    public async Task PublishCheckoutFlowAsync_SetsCorrectCheckoutType(string checkoutType)
    {
        // Arrange
        var request = CreateRequest("test@example.com", "0900123456");
        var flowId = "test-flow";
        var orderId = "ORD-abc123";
        var paymentId = "PAY-xyz789";

        // Act
        await _sut.PublishCheckoutFlowAsync(request, flowId, checkoutType, orderId, paymentId);

        // Assert
        _publishedMessages.Should().OnlyContain(m => m.CheckoutType == checkoutType);
    }

    [Fact]
    public async Task PublishCheckoutFlowAsync_SetsUserIdToNull()
    {
        // Arrange
        var request = CreateRequest("test@example.com", "0900123456");
        var flowId = "test-flow";
        var checkoutType = CheckoutTypes.Guest;
        var orderId = "ORD-abc123";
        var paymentId = "PAY-xyz789";

        // Act
        await _sut.PublishCheckoutFlowAsync(request, flowId, checkoutType, orderId, paymentId);

        // Assert
        _publishedMessages.Should().OnlyContain(m => m.UserId == null);
    }

    [Fact]
    public async Task PublishCheckoutFlowAsync_SetsStatusToSuccessOnAllEvents()
    {
        // Arrange
        var request = CreateRequest("test@example.com", "0900123456");
        var flowId = "test-flow";
        var checkoutType = CheckoutTypes.Guest;
        var orderId = "ORD-abc123";
        var paymentId = "PAY-xyz789";

        // Act
        await _sut.PublishCheckoutFlowAsync(request, flowId, checkoutType, orderId, paymentId);

        // Assert
        _publishedMessages.Should().OnlyContain(m => m.Status == StatusTypes.Success);
    }

    [Fact]
    public async Task PublishCheckoutFlowAsync_GeneratesUniqueEventIds()
    {
        // Arrange
        var request = CreateRequest("test@example.com", "0900123456");
        var flowId = "test-flow";
        var checkoutType = CheckoutTypes.Guest;
        var orderId = "ORD-abc123";
        var paymentId = "PAY-xyz789";

        // Act
        await _sut.PublishCheckoutFlowAsync(request, flowId, checkoutType, orderId, paymentId);

        // Assert
        var eventIds = _publishedMessages.Select(m => m.EventId).ToList();
        eventIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task PublishCheckoutFlowAsync_DoesNotThrowWhenPublishFails()
    {
        // Arrange
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Kafka unavailable"));

        var request = CreateRequest("test@example.com", "0900123456");
        var flowId = "test-flow";
        var checkoutType = CheckoutTypes.Guest;
        var orderId = "ORD-abc123";
        var paymentId = "PAY-xyz789";

        // Act
        var act = async () => await _sut.PublishCheckoutFlowAsync(request, flowId, checkoutType, orderId, paymentId);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishCheckoutFlowAsync_LogsErrorWhenPublishFails()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Kafka unavailable");
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        var request = CreateRequest("test@example.com", "0900123456");
        var flowId = "test-flow";
        var checkoutType = CheckoutTypes.Guest;
        var orderId = "ORD-abc123";
        var paymentId = "PAY-xyz789";

        // Act
        await _sut.PublishCheckoutFlowAsync(request, flowId, checkoutType, orderId, paymentId);

        // Assert - verify error was logged
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task PublishCheckoutFlowAsync_ContinuesPublishingAfterFirstFailure()
    {
        // Arrange
        var callCount = 0;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback(() => callCount++)
            .ThrowsAsync(new InvalidOperationException("Kafka unavailable"));

        var request = CreateRequest("test@example.com", "0900123456");
        var flowId = "test-flow";
        var checkoutType = CheckoutTypes.Guest;
        var orderId = "ORD-abc123";
        var paymentId = "PAY-xyz789";

        // Act
        await _sut.PublishCheckoutFlowAsync(request, flowId, checkoutType, orderId, paymentId);

        // Assert - all 6 events should be attempted (each one calls PublishEventSafelyAsync)
        callCount.Should().Be(6);
    }

    private static CheckoutEsimRequest CreateRequest(string email, string phone)
    {
        return new CheckoutEsimRequest
        {
            CustomerEmail = email,
            CustomerPhone = phone,
            PackageCode = "ESIM-GLOBAL-30",
            Quantity = 1
        };
    }
}
