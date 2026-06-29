using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Skysim.Logger.Api.Common;
using Skysim.Logger.Api.Infrastructure.Kafka;
using Skysim.Logger.Api.Middlewares;
using Xunit;
using LogEventMessage = Skysim.Logger.Contracts.Events.LogEventMessage;
using StatusTypes = Skysim.Logger.Contracts.Constants.StatusTypes;
using FlowTypes = Skysim.Logger.Contracts.Constants.FlowTypes;
using ActionTypes = Skysim.Logger.Contracts.Constants.ActionTypes;

namespace Skysim.Logger.Api.Tests.MiddlewareTests;

public class LoggerMiddlewareTests
{
    private readonly Mock<IKafkaLogProducer> _producerMock = new();
    private readonly SensitiveDataMasker _masker = new();
    private readonly Mock<ILogger<LoggerMiddleware>> _loggerMock = new();

    [Fact]
    public async Task InvokeAsync_SuccessfulRequest_PublishesLogEventMessage()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: _ => Task.CompletedTask,
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", "/api/test");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _producerMock.Verify(
            p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);

        capturedMessage.Should().NotBeNull();
        capturedMessage!.FlowType.Should().Be(FlowTypes.HttpAction);
        capturedMessage.ActionType.Should().Be(ActionTypes.HttpRequest);
        capturedMessage.Status.Should().Be(StatusTypes.Success);
        capturedMessage.CorrelationId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_SetsCorrelationIdFromHeader()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: _ => Task.CompletedTask,
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["X-Correlation-Id"] = "my-correlation-id";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.CorrelationId.Should().Be("my-correlation-id");
    }

    [Fact]
    public async Task InvokeAsync_GeneratesCorrelationIdWhenMissing()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: _ => Task.CompletedTask,
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", "/api/test");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.CorrelationId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_AddsCorrelationIdToResponseHeader()
    {
        // Arrange
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: _ => Task.CompletedTask,
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", "/api/test");
        context.TraceIdentifier = string.Empty; // Force middleware to generate new FlowId

        // Act
        await middleware.InvokeAsync(context);

        // Assert — header is set when middleware generates a new FlowId
        context.Response.Headers["X-Correlation-Id"].ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_5xxStatus_IsLoggedAsFailed()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: ctx =>
            {
                ctx.Response.StatusCode = 500;
                return Task.CompletedTask;
            },
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", "/api/test");

        // Act
        await middleware.InvokeAsync(context);

        // Assert — ErrorCode comes from exception.Message, not status code
        // Status is Failed because statusCode >= 500 without an exception
        capturedMessage.Should().NotBeNull();
        capturedMessage!.Status.Should().Be(StatusTypes.Failed);
        capturedMessage.ErrorCode.Should().BeNull();
        capturedMessage.Message.Should().Contain("500");
    }

    [Fact]
    public async Task InvokeAsync_ExceptionInNext_SetsErrorCodeAndErrorMessage()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: _ => throw new InvalidOperationException("Something went wrong"),
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", "/api/test");

        // Act — the middleware catches the exception in the finally block and re-throws it
        await middleware.Invoking(m => m.InvokeAsync(context))
            .Should().ThrowAsync<InvalidOperationException>();

        // Assert — ErrorCode and ErrorMessage come from the caught exception
        capturedMessage.Should().NotBeNull();
        capturedMessage!.Status.Should().Be(StatusTypes.Failed);
        capturedMessage.ErrorCode.Should().Be("500");
        capturedMessage.ErrorMessage.Should().Be("Something went wrong");
    }

    [Fact]
    public async Task InvokeAsync_RequestWithBody_CapturesRequestData()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: _ => Task.CompletedTask,
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("POST", "/api/orders");
        context.Request.Body = CreateRequestBodyStream("""{"orderId":"ORD-001","password":"secret123"}""");
        context.Request.ContentType = "application/json";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.RequestData.HasValue.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_MasksSensitiveFieldsInRequestData()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: _ => Task.CompletedTask,
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("POST", "/api/orders");
        context.Request.Body = CreateRequestBodyStream("""{"email":"user@test.com","password":"secret123"}""");
        context.Request.ContentType = "application/json";

        // Act
        await middleware.InvokeAsync(context);

        // Assert — the masker runs in the middleware, masking the message in-place
        capturedMessage.Should().NotBeNull();
        capturedMessage!.RequestData.HasValue.Should().BeTrue();
        var json = capturedMessage!.RequestData!.Value.GetRawText();
        json.Should().Contain("***");
        json.Should().NotContain("secret123");
    }

    [Fact]
    public async Task InvokeAsync_GetWithQueryString_CapturesRequestData()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: _ => Task.CompletedTask,
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.QueryString = new QueryString("?status=active&token=abc-token&authorization=Bearer-secret");
        context.Request.Headers["X-Flow-Id"] = "flow-from-query";
        context.Request.Headers["X-Correlation-Id"] = "correlation-from-query";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.RequestData.HasValue.Should().BeTrue();
        var requestJson = capturedMessage!.RequestData!.Value.GetRawText();
        requestJson.Should().Contain(@"""status"":""active""");
        requestJson.Should().Contain(@"""method"":""GET""");
        requestJson.Should().Contain(@"""path"":""/api/test""");
        requestJson.Should().Contain(@"""token"":""***""");
        requestJson.Should().Contain(@"""authorization"":""***""");
        requestJson.Should().NotContain("abc-token");
        requestJson.Should().NotContain("Bearer-secret");
        requestJson.Should().Contain("\"selectedHeaders\"");
    }

    [Fact]
    public async Task InvokeAsync_PublishFailure_DoesNotThrow()
    {
        // Arrange
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Kafka unavailable"));

        var middleware = new LoggerMiddleware(
            next: _ => Task.CompletedTask,
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", "/api/test");

        // Act
        Func<Task> act = () => middleware.InvokeAsync(context);

        // Assert — should not throw despite producer failure
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InvokeAsync_PublishFailure_LogsWarning()
    {
        // Arrange
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Kafka unavailable"));

        var middleware = new LoggerMiddleware(
            next: _ => Task.CompletedTask,
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", "/api/test");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Kafka publish failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task InvokeAsync_CapturesDuration()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: async _ => await Task.Delay(50),
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", "/api/test");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.Duration.Should().BeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public async Task InvokeAsync_SetsMessageField()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: _ => Task.CompletedTask,
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", "/api/test");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.Message.Should().Contain("GET");
        capturedMessage.Message.Should().Contain("/api/test");
    }

    [Fact]
    public async Task InvokeAsync_WithXFlowIdHeader_UsesItAsFlowId()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: _ => Task.CompletedTask,
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["X-Flow-Id"] = "my-flow-id-from-header";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.FlowId.Should().Be("my-flow-id-from-header");
        capturedMessage.CorrelationId.Should().Be("my-flow-id-from-header");
    }

    [Fact]
    public async Task InvokeAsync_WithoutHeaders_UsesTraceIdentifier()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: _ => Task.CompletedTask,
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", "/api/test");
        context.TraceIdentifier = "trace-12345";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.FlowId.Should().Be("trace-12345");
    }

    [Fact]
    public async Task InvokeAsync_WithoutHeadersAndNoTraceId_GeneratesGuid()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: _ => Task.CompletedTask,
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", "/api/test");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.FlowId.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
    [InlineData("/api/log-flows")]
    [InlineData("/api/log-flows?page=1")]
    [InlineData("/api/log-actions")]
    [InlineData("/api/log-actions/123")]
    [InlineData("/favicon.ico")]
    [InlineData("/health")]
    [InlineData("/HEALTH")]
    [InlineData("/Swagger/Index")]
    public async Task InvokeAsync_ExcludedPaths_DoesNotPublish(string path)
    {
        // Arrange
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: _ => Task.CompletedTask,
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", path);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _producerMock.Verify(
            p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("/api/orders")]
    [InlineData("/api/customers")]
    [InlineData("/api/payments/123")]
    public async Task InvokeAsync_NonExcludedPaths_PublishesLogEventMessage(string path)
    {
        // Arrange
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: _ => Task.CompletedTask,
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", path);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _producerMock.Verify(
            p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_404Response_IncludesStatusCodeOnly()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                ctx.Response.ContentType = "text/plain";
                return ctx.Response.WriteAsync("not found");
            },
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", "/not-exist-test");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.ResponseData.HasValue.Should().BeTrue();
        capturedMessage!.ResponseData!.Value.GetRawText().Should().Contain(@"""statusCode"":404");
    }

    [Fact]
    public async Task InvokeAsync_ExceptionResponse_IncludesExceptionDetails()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: _ => throw new InvalidOperationException("handled exception"),
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", "/api/error");

        // Act
        Func<Task> act = () => middleware.InvokeAsync(context);
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.Status.Should().Be(StatusTypes.Failed);
        capturedMessage.ErrorMessage.Should().Be("handled exception");
        capturedMessage.Exception.Should().Contain("InvalidOperationException");
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_ExtractsUserIdFromSubClaim()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: _ => Task.CompletedTask,
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", "/api/test");
        SetupAuthenticatedUser(context, "sub", "user-123-sub");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.UserId.Should().Be("user-123-sub");
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_ExtractsUserIdFromUserIdClaim()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: _ => Task.CompletedTask,
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", "/api/test");
        SetupAuthenticatedUser(context, "userId", "user-456");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.UserId.Should().Be("user-456");
    }

    [Fact]
    public async Task InvokeAsync_AnonymousUser_SetsUserIdToNull()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = new LoggerMiddleware(
            next: _ => Task.CompletedTask,
            _producerMock.Object,
            _masker,
            _loggerMock.Object);

        var context = CreateHttpContext("GET", "/api/test");
        // Default context is anonymous (no authentication set up)

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.UserId.Should().BeNull();
    }

    private static DefaultHttpContext CreateHttpContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Body = new MemoryStream();
        context.Response.Body = new MemoryStream();
        context.Request.QueryString = new QueryString(string.Empty);
        return context;
    }

    private static Stream CreateRequestBodyStream(string body)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(body);
        return new MemoryStream(bytes);
    }

    private static void SetupAuthenticatedUser(DefaultHttpContext context, string claimType, string claimValue)
    {
        var claims = new[] { new Claim(claimType, claimValue) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        context.User = new ClaimsPrincipal(identity);
    }
}
