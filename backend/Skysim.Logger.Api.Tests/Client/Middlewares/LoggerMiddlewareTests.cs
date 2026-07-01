using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Skysim.Logger.Client.Masking;
using Skysim.Logger.Client.Middlewares;
using Skysim.Logger.Client.Producers;
using Xunit;
using LogEventMessage = Skysim.Logger.Contracts.Events.LogEventMessage;
using StatusTypes = Skysim.Logger.Contracts.Constants.StatusTypes;
using FlowTypes = Skysim.Logger.Contracts.Constants.FlowTypes;
using ActionTypes = Skysim.Logger.Contracts.Constants.ActionTypes;

namespace Skysim.Logger.Api.Tests.Client.Middlewares;

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

        var middleware = CreateMiddleware();

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
        capturedMessage.FlowId.Should().NotBeNullOrEmpty();
        capturedMessage.Method.Should().Be("GET");
        capturedMessage.Path.Should().Be("/api/test");
        capturedMessage.DurationMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task InvokeAsync_SetsFlowIdFromXFlowIdHeader()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["X-Flow-Id"] = "my-flow-id";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.FlowId.Should().Be("my-flow-id");
    }

    [Fact]
    public async Task InvokeAsync_SetsFlowIdFromXCorrelationIdHeader()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["X-Correlation-Id"] = "my-correlation-id";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.FlowId.Should().Be("my-correlation-id");
    }

    [Fact]
    public async Task InvokeAsync_GeneratesFlowIdWhenMissing()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.TraceIdentifier = string.Empty;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.FlowId.Should().NotBeNullOrEmpty();
        Guid.TryParse(capturedMessage.FlowId, out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AddsXFlowIdToResponseHeaderWhenGenerated()
    {
        // Arrange
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.TraceIdentifier = string.Empty;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Flow-Id"].ToString().Should().NotBeNullOrEmpty();
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

        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 500;
            return Task.CompletedTask;
        });

        var context = CreateHttpContext("GET", "/api/test");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.Status.Should().Be(StatusTypes.Failed);
        capturedMessage.StatusCode.Should().Be(500);
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

        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("Something went wrong"));

        var context = CreateHttpContext("GET", "/api/test");

        // Act & Assert
        await middleware.Invoking(m => m.InvokeAsync(context))
            .Should().ThrowAsync<InvalidOperationException>();

        capturedMessage.Should().NotBeNull();
        capturedMessage!.Status.Should().Be(StatusTypes.Failed);
        capturedMessage.ErrorCode.Should().Be("InvalidOperationException");
        capturedMessage.ErrorMessage.Should().Be("Something went wrong");
    }

    [Fact]
    public async Task InvokeAsync_RequestWithBody_CapturesRequestBody()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var bodyContent = """{"orderId":"ORD-001"}""";
        var context = CreateHttpContext("POST", "/api/orders");
        context.Request.Body = CreateRequestBodyStream(bodyContent);
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = bodyContent.Length;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.RequestBody.Should().NotBeNullOrEmpty();
        capturedMessage.RequestBody.Should().Contain("ORD-001");
    }

    [Fact]
    public async Task InvokeAsync_MasksSensitiveFieldsInRequestBody()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var bodyContent = """{"email":"user@test.com","password":"secret123"}""";
        var context = CreateHttpContext("POST", "/api/orders");
        context.Request.Body = CreateRequestBodyStream(bodyContent);
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = bodyContent.Length;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.RequestBody.Should().Contain("***");
        capturedMessage!.RequestBody.Should().NotContain("secret123");
    }

    [Fact]
    public async Task InvokeAsync_CapturesQueryString()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.QueryString = new QueryString("?status=active&q=search");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.QueryString.Should().Contain("status=active");
    }

    [Fact]
    public async Task InvokeAsync_PublishFailure_DoesNotThrow()
    {
        // Arrange
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Kafka unavailable"));

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");

        // Act
        Func<Task> act = () => middleware.InvokeAsync(context);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InvokeAsync_PublishFailure_LogsError()
    {
        // Arrange
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Kafka unavailable"));

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Kafka publish failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_CapturesDurationMs()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware(async _ => await Task.Delay(50));

        var context = CreateHttpContext("GET", "/api/test");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.DurationMs.Should().BeGreaterThanOrEqualTo(50);
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

        var middleware = CreateMiddleware();

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

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["X-Flow-Id"] = "my-flow-id-from-header";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.FlowId.Should().Be("my-flow-id-from-header");
    }

    [Theory]
    [InlineData("/swagger")]
    [InlineData("/swagger/index.html")]
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

        var middleware = CreateMiddleware();

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

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", path);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _producerMock.Verify(
            p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_404Response_CapturesStatusCode()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            ctx.Response.ContentType = "text/plain";
            return ctx.Response.WriteAsync("not found");
        });

        var context = CreateHttpContext("GET", "/not-exist-test");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task InvokeAsync_LargeRequestBody_SkipsCapture()
    {
        // Arrange
        LogEventMessage? capturedMessage = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<LogEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<LogEventMessage, CancellationToken>((msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var largeBody = new string('x', 50 * 1024); // 50KB body
        var context = CreateHttpContext("POST", "/api/upload");
        context.Request.Body = CreateRequestBodyStream(largeBody);
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = largeBody.Length;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.RequestBody.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_ServiceNameFromConstructor()
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
            _loggerMock.Object,
            "my-custom-service");

        var context = CreateHttpContext("GET", "/api/test");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedMessage.Should().NotBeNull();
        capturedMessage!.ServiceName.Should().Be("my-custom-service");
    }

    private LoggerMiddleware CreateMiddleware(RequestDelegate? next = null)
    {
        return new LoggerMiddleware(
            next: next ?? (_ => Task.CompletedTask),
            _producerMock.Object,
            _masker,
            _loggerMock.Object,
            "test-service");
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
}
