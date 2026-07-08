using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
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
    private readonly Mock<ILogger<LoggerMiddleware>> _loggerMock = new();

    [Fact]
    public async Task InvokeAsync_SuccessfulRequest_PublishesLogEventMessage()
    {
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");

        await middleware.InvokeAsync(context);

        _producerMock.Verify(
            p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);

        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.FlowType.Should().Be(FlowTypes.HttpAction);
        message.ActionType.Should().Be(ActionTypes.HttpRequest);
        message.Status.Should().Be(StatusTypes.Success);
        message.FlowId.Should().NotBeNullOrEmpty();
        message.Method.Should().Be("GET");
        message.Path.Should().Be("/api/test");
        message.DurationMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task InvokeAsync_SetsFlowIdFromXFlowIdHeader()
    {
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["X-Flow-Id"] = "my-flow-id";

        await middleware.InvokeAsync(context);

        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.FlowId.Should().Be("my-flow-id");
    }

    [Fact]
    public async Task InvokeAsync_SetsFlowIdFromXCorrelationIdHeader()
    {
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["X-Correlation-Id"] = "my-correlation-id";

        await middleware.InvokeAsync(context);

        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.FlowId.Should().Be("my-correlation-id");
    }

    [Fact]
    public async Task InvokeAsync_GeneratesFlowIdWhenMissing()
    {
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");

        await middleware.InvokeAsync(context);

        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.FlowId.Should().NotBeNullOrEmpty();
        Guid.TryParse(message.FlowId, out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_AddsXFlowIdToResponseHeaderWhenGenerated()
    {
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");

        await middleware.InvokeAsync(context);

        context.Response.Headers["X-Flow-Id"].ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_5xxStatus_IsLoggedAsFailed()
    {
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = 500;
            return Task.CompletedTask;
        });
        var context = CreateHttpContext("GET", "/api/test");

        await middleware.InvokeAsync(context);

        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.Status.Should().Be(StatusTypes.Failed);
        message.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task InvokeAsync_ExceptionInNext_SetsErrorCodeAndErrorMessage()
    {
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("Something went wrong"));
        var context = CreateHttpContext("GET", "/api/test");

        await middleware.Invoking(m => m.InvokeAsync(context))
            .Should().ThrowAsync<InvalidOperationException>();

        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.Status.Should().Be(StatusTypes.Failed);
        message.ErrorCode.Should().Be("InvalidOperationException");
        message.ErrorMessage.Should().Be("Something went wrong");
    }

    [Fact]
    public async Task InvokeAsync_RequestWithBody_CapturesRequestBody()
    {
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var bodyContent = """{"orderId":"ORD-001"}""";
        var context = CreateHttpContext("POST", "/api/orders");
        context.Request.Body = CreateRequestBodyStream(bodyContent);
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = bodyContent.Length;

        await middleware.InvokeAsync(context);

        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.RequestBody.Should().NotBeNullOrEmpty();
        message.RequestBody.Should().Contain("ORD-001");
    }

    [Fact]
    public async Task InvokeAsync_CapturesQueryString()
    {
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");
        context.Request.QueryString = new QueryString("?status=active&q=search");

        await middleware.InvokeAsync(context);

        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.QueryString.Should().Contain("status=active");
    }

    [Fact]
    public async Task InvokeAsync_PublishFailure_DoesNotThrow()
    {
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Kafka unavailable"));

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");

        Func<Task> act = () => middleware.InvokeAsync(context);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InvokeAsync_PublishFailure_LogsError()
    {
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Kafka unavailable"));

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");

        await middleware.InvokeAsync(context);

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
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware(async _ => await Task.Delay(50));
        var context = CreateHttpContext("GET", "/api/test");

        await middleware.InvokeAsync(context);

        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.DurationMs.Should().BeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public async Task InvokeAsync_SetsMessageField()
    {
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");

        await middleware.InvokeAsync(context);

        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.Message.Should().Contain("GET");
        message.Message.Should().Contain("/api/test");
    }

    [Fact]
    public async Task InvokeAsync_WithXFlowIdHeader_UsesItAsFlowId()
    {
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["X-Flow-Id"] = "my-flow-id-from-header";

        await middleware.InvokeAsync(context);

        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.FlowId.Should().Be("my-flow-id-from-header");
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
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", path);

        await middleware.InvokeAsync(context);

        _producerMock.Verify(
            p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("/api/orders")]
    [InlineData("/api/customers")]
    [InlineData("/api/payments/123")]
    public async Task InvokeAsync_NonExcludedPaths_PublishesLogEventMessage(string path)
    {
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", path);

        await middleware.InvokeAsync(context);

        _producerMock.Verify(
            p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_404Response_CapturesStatusCode()
    {
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            ctx.Response.ContentType = "text/plain";
            return ctx.Response.WriteAsync("not found");
        });
        var context = CreateHttpContext("GET", "/not-exist-test");

        await middleware.InvokeAsync(context);

        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task InvokeAsync_LargeRequestBody_SkipsCapture()
    {
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var largeBody = new string('x', 50 * 1024);
        var context = CreateHttpContext("POST", "/api/upload");
        context.Request.Body = CreateRequestBodyStream(largeBody);
        context.Request.ContentType = "application/json";
        context.Request.ContentLength = largeBody.Length;

        await middleware.InvokeAsync(context);

        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.RequestBody.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_ServiceNameFromConstructor()
    {
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middlewareOptions = new LoggerMiddlewareOptions { ServiceName = "my-custom-service" };
        var options = Microsoft.Extensions.Options.Options.Create(middlewareOptions);
        var middleware = new LoggerMiddleware(
            next: _ => Task.CompletedTask,
            _producerMock.Object,
            _loggerMock.Object,
            options);
        var context = CreateHttpContext("GET", "/api/test");

        await middleware.InvokeAsync(context);

        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.ServiceName.Should().Be("my-custom-service");
    }

    [Fact]
    public async Task InvokeAsync_AuthorizationHeaderInRequestHeaders_IsRaw_ForConsumerToMask()
    {
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["Authorization"] = "Bearer super-secret-token-12345";
        context.Request.Headers["Content-Type"] = "application/json";

        await middleware.InvokeAsync(context);

        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.RequestHeaders.Should().ContainKey("Authorization");
        message.RequestHeaders!["Authorization"].Should().Be("Bearer super-secret-token-12345");
        message.RequestHeaders!["Content-Type"].Should().Be("application/json");
    }

    [Fact]
    public async Task InvokeAsync_BasicAuthHeaderInRequestHeaders_IsRaw_ForConsumerToMask()
    {
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["Authorization"] = "Basic dXNlcm5hbWU6cGFzc3dvcmQ=";

        await middleware.InvokeAsync(context);

        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.RequestHeaders.Should().ContainKey("Authorization");
        message.RequestHeaders!["Authorization"].Should().Be("Basic dXNlcm5hbWU6cGFzc3dvcmQ=");
    }

    [Fact]
    public async Task InvokeAsync_AllRequestHeaders_AreCaptured()
    {
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["X-Custom-Header"] = "custom-value";
        context.Request.Headers["X-Another"] = "another-value";

        await middleware.InvokeAsync(context);

        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.RequestHeaders.Should().NotBeNull();
        var headers = message.RequestHeaders!;
        headers.Should().ContainKey("X-Custom-Header");
        headers["X-Custom-Header"].Should().Be("custom-value");
        headers.Should().ContainKey("X-Another");
        headers["X-Another"].Should().Be("another-value");
    }

    [Fact]
    public async Task InvokeAsync_DoesNotPublishAuthDerivedFields()
    {
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["Authorization"] = "Bearer some-token";

        await middleware.InvokeAsync(context);

        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        root.TryGetProperty("userId", out _).Should().BeFalse();
        root.TryGetProperty("userEmail", out _).Should().BeFalse();
        root.TryGetProperty("username", out _).Should().BeFalse();
        root.TryGetProperty("roles", out _).Should().BeFalse();
        root.TryGetProperty("authResult", out _).Should().BeFalse();
        root.TryGetProperty("isAuthenticated", out _).Should().BeFalse();
        root.TryGetProperty("authScheme", out _).Should().BeFalse();
        root.TryGetProperty("hasAuthorization", out _).Should().BeFalse();
    }

    #region Flow context propagation tests

    [Fact]
    public async Task InvokeAsync_SetsXCorrelationIdInResponseHeader_WhenGenerated()
    {
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");

        await middleware.InvokeAsync(context);

        var corrIdHeader = context.Response.Headers["X-Correlation-Id"].ToString();
        corrIdHeader.Should().NotBeNullOrEmpty();
        Guid.TryParse(corrIdHeader, out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_SetsXCorrelationIdToXFlowIdValue_WhenXFlowIdProvided()
    {
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["X-Flow-Id"] = "my-flow-id";

        await middleware.InvokeAsync(context);

        context.Response.Headers["X-Flow-Id"].ToString().Should().Be("my-flow-id");
        context.Response.Headers["X-Correlation-Id"].ToString().Should().Be("my-flow-id");
    }

    [Fact]
    public async Task InvokeAsync_PublishesCorrelationIdInPayload_WhenXCorrelationIdProvided()
    {
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["X-Correlation-Id"] = "my-corr-id";

        await middleware.InvokeAsync(context);

        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("correlationId").GetString().Should().Be("my-corr-id");
        doc.RootElement.GetProperty("flowId").GetString().Should().Be("my-corr-id");
    }

    [Fact]
    public async Task InvokeAsync_ExposesFlowContextInHttpContextItems()
    {
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["X-Flow-Id"] = "context-test-flow";

        await middleware.InvokeAsync(context);

        context.Items.Should().ContainKey("FlowContext");
        var flowCtx = context.Items["FlowContext"] as Skysim.Logger.Client.Middlewares.FlowContext;
        flowCtx.Should().NotBeNull();
        flowCtx!.FlowId.Should().Be("context-test-flow");
        flowCtx.CorrelationId.Should().Be("context-test-flow");
    }

    [Fact]
    public async Task InvokeAsync_ExposesGeneratedFlowContextInHttpContextItems()
    {
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");

        await middleware.InvokeAsync(context);

        context.Items.Should().ContainKey("FlowContext");
        var flowCtx = context.Items["FlowContext"] as Skysim.Logger.Client.Middlewares.FlowContext;
        flowCtx.Should().NotBeNull();
        flowCtx!.FlowId.Should().NotBeNullOrEmpty();
        flowCtx.CorrelationId.Should().Be(flowCtx.FlowId);
        Guid.TryParse(flowCtx.FlowId, out _).Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_XCorrelationIdInRequest_UsesItForBothFlowIdAndCorrelationId()
    {
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["X-Correlation-Id"] = "external-corr-id";

        await middleware.InvokeAsync(context);

        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("flowId").GetString().Should().Be("external-corr-id");
        doc.RootElement.GetProperty("correlationId").GetString().Should().Be("external-corr-id");
        context.Response.Headers["X-Flow-Id"].ToString().Should().Be("external-corr-id");
        context.Response.Headers["X-Correlation-Id"].ToString().Should().Be("external-corr-id");
    }

    #endregion

    #region Request header propagation (so FlowContextForwardingHandler can read flowId even when caller did not send X-Flow-Id)

    [Fact]
    public async Task InvokeAsync_GeneratesFlowId_WhenInboundMissing_AlsoSetsRequestHeader()
    {
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");

        await middleware.InvokeAsync(context);

        var flowIdResponse = context.Response.Headers["X-Flow-Id"].ToString();
        var flowIdRequest = context.Request.Headers["X-Flow-Id"].ToString();
        flowIdRequest.Should().NotBeNullOrEmpty();
        flowIdRequest.Should().Be(flowIdResponse);
    }

    [Fact]
    public async Task InvokeAsync_GeneratesCorrelationId_WhenInboundMissing_AlsoSetsRequestHeader()
    {
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");

        await middleware.InvokeAsync(context);

        var corrIdResponse = context.Response.Headers["X-Correlation-Id"].ToString();
        var corrIdRequest = context.Request.Headers["X-Correlation-Id"].ToString();
        corrIdRequest.Should().NotBeNullOrEmpty();
        corrIdRequest.Should().Be(corrIdResponse);
    }

    [Fact]
    public async Task InvokeAsync_PreservesCallerXFlowId_OnRequestAndResponseHeaders()
    {
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();
        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["X-Flow-Id"] = "caller-supplied-flow";

        await middleware.InvokeAsync(context);

        context.Request.Headers["X-Flow-Id"].ToString().Should().Be("caller-supplied-flow");
        context.Response.Headers["X-Flow-Id"].ToString().Should().Be("caller-supplied-flow");
        context.Request.Headers["X-Correlation-Id"].ToString().Should().Be("caller-supplied-flow");
        context.Response.Headers["X-Correlation-Id"].ToString().Should().Be("caller-supplied-flow");
    }

    #endregion

    private LoggerMiddleware CreateMiddleware(RequestDelegate? next = null)
    {
        var middlewareOptions = new LoggerMiddlewareOptions { ServiceName = "test-service" };
        var options = Microsoft.Extensions.Options.Options.Create(middlewareOptions);
        return new LoggerMiddleware(
            next: next ?? (_ => Task.CompletedTask),
            _producerMock.Object,
            _loggerMock.Object,
            options);
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
