using System.Text;
using System.Text.Json;
using System.Security.Claims;
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
using AuthResultTypes = Skysim.Logger.Contracts.Constants.AuthResultTypes;

namespace Skysim.Logger.Api.Tests.Client.Middlewares;

public class LoggerMiddlewareTests
{
    private readonly Mock<IKafkaLogProducer> _producerMock = new();
    private readonly Mock<ILogger<LoggerMiddleware>> _loggerMock = new();

    [Fact]
    public async Task InvokeAsync_SuccessfulRequest_PublishesLogEventMessage()
    {
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
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
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["X-Flow-Id"] = "my-flow-id";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.FlowId.Should().Be("my-flow-id");
    }

    [Fact]
    public async Task InvokeAsync_SetsFlowIdFromXCorrelationIdHeader()
    {
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["X-Correlation-Id"] = "my-correlation-id";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.FlowId.Should().Be("my-correlation-id");
    }

    [Fact]
    public async Task InvokeAsync_GeneratesFlowIdWhenMissing()
    {
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.TraceIdentifier = string.Empty;

        // Act
        await middleware.InvokeAsync(context);

        // Assert
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
        // Arrange
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
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

        // Act
        await middleware.InvokeAsync(context);

        // Assert
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
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("Something went wrong"));

        var context = CreateHttpContext("GET", "/api/test");

        // Act & Assert
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
        // Arrange
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

        // Act
        await middleware.InvokeAsync(context);

        // Assert
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
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.QueryString = new QueryString("?status=active&q=search");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.QueryString.Should().Contain("status=active");
    }

    [Fact]
    public async Task InvokeAsync_PublishFailure_DoesNotThrow()
    {
        // Arrange
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
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
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
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
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware(async _ => await Task.Delay(50));

        var context = CreateHttpContext("GET", "/api/test");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.DurationMs.Should().BeGreaterThanOrEqualTo(50);
    }

    [Fact]
    public async Task InvokeAsync_SetsMessageField()
    {
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
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
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["X-Flow-Id"] = "my-flow-id-from-header";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
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
        // Arrange
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", path);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
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
        // Arrange
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", path);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        _producerMock.Verify(
            p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_404Response_CapturesStatusCode()
    {
        // Arrange
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

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task InvokeAsync_LargeRequestBody_SkipsCapture()
    {
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
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
        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.RequestBody.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_ServiceNameFromConstructor()
    {
        // Arrange
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

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.ServiceName.Should().Be("my-custom-service");
    }

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

    // ==== Auth context tests ====

    [Fact]
    public async Task InvokeAsync_GuestRequest_SetsAuthFieldsCorrectly()
    {
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.HasAuthorization.Should().BeFalse();
        message.AuthScheme.Should().BeNull();
        message.IsAuthenticated.Should().BeFalse();
        message.UserId.Should().BeNull();
        message.Username.Should().BeNull();
        message.UserEmail.Should().BeNull();
        message.Roles.Should().BeNull();
        message.AuthResult.Should().Be("NO_TOKEN");
    }

    [Fact]
    public async Task InvokeAsync_TokenPresentButNotAuthenticated_SetsAuthResultTokenPresentNotAuthenticated()
    {
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["Authorization"] = "Bearer some-invalid-token";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.HasAuthorization.Should().BeTrue();
        message.IsAuthenticated.Should().BeFalse();
        message.AuthResult.Should().Be("TOKEN_PRESENT_NOT_AUTHENTICATED");
    }

    [Fact]
    public async Task InvokeAsync_RequestWithAuthorizationHeader_SetsHasAuthorizationTrue()
    {
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["Authorization"] = "Bearer some-token-value";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.HasAuthorization.Should().BeTrue();
        message.AuthScheme.Should().Be("Bearer");
    }

    [Fact]
    public async Task InvokeAsync_RequestWithBasicAuth_SetsAuthSchemeCorrectly()
    {
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["Authorization"] = "Basic dXNlcm5hbWU6cGFzc3dvcmQ=";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.HasAuthorization.Should().BeTrue();
        message.AuthScheme.Should().Be("Basic");
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_SetsUserFieldsCorrectly()
    {
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["Authorization"] = "Bearer valid-token";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-123"),
            new Claim("sub", "user-123"),
            new Claim("preferred_username", "johndoe"),
            new Claim(ClaimTypes.Email, "john@example.com"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "User")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        context.User = new ClaimsPrincipal(identity);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.IsAuthenticated.Should().BeTrue();
        message.UserId.Should().Be("user-123");
        message.Username.Should().Be("johndoe");
        message.UserEmail.Should().Be("john@example.com");
        message.Roles.Should().Contain("Admin");
        message.Roles.Should().Contain("User");
        message.AuthResult.Should().Be("AUTHENTICATED");
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUserWithSubClaim_SetsUserIdFromSub()
    {
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["Authorization"] = "Bearer valid-token";

        var claims = new[]
        {
            new Claim("sub", "user-456"),
            new Claim("preferred_username", "janedoe")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        context.User = new ClaimsPrincipal(identity);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.UserId.Should().Be("user-456");
        message.Username.Should().Be("janedoe");
    }

    [Fact]
    public async Task InvokeAsync_401Response_SetsAuthResultUnauthenticated()
    {
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        });

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["Authorization"] = "Bearer some-token";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.AuthResult.Should().Be("UNAUTHENTICATED");
        message.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task InvokeAsync_403Response_SetsAuthResultForbidden()
    {
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        });

        var context = CreateHttpContext("GET", "/api/test");

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.AuthResult.Should().Be("FORBIDDEN");
        message.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task InvokeAsync_AuthorizationHeaderInRequestHeaders_IsMasked()
    {
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["Authorization"] = "Bearer super-secret-token-12345";
        context.Request.Headers["Content-Type"] = "application/json";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.RequestHeaders.Should().ContainKey("Authorization");
        message.RequestHeaders!["Authorization"].Should().Be("Bearer ***");
        message.RequestHeaders!["Content-Type"].Should().Be("application/json");
    }

    [Fact]
    public async Task InvokeAsync_BasicAuthHeaderInRequestHeaders_IsMasked()
    {
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["Authorization"] = "Basic dXNlcm5hbWU6cGFzc3dvcmQ=";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.RequestHeaders.Should().ContainKey("Authorization");
        message.RequestHeaders!["Authorization"].Should().Be("***");
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedWithRoleClaim_SetsRolesCorrectly()
    {
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["Authorization"] = "Bearer valid-token";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-789"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("role", "PowerUser")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        context.User = new ClaimsPrincipal(identity);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.Roles.Should().NotBeNull();
        message.Roles.Should().HaveCount(2);
        message.Roles.Should().Contain("Admin");
        message.Roles.Should().Contain("PowerUser");
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedWithoutRoles_RolesIsNull()
    {
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["Authorization"] = "Bearer valid-token";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-no-roles")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        context.User = new ClaimsPrincipal(identity);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        message!.Roles.Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedWithUserIdClaim_SetsUserIdFromUserIdClaim()
    {
        // Arrange
        object? capturedPayload = null;
        _producerMock
            .Setup(p => p.PublishAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((payload, _) => capturedPayload = payload)
            .Returns(Task.CompletedTask);

        var middleware = CreateMiddleware();

        var context = CreateHttpContext("GET", "/api/test");
        context.Request.Headers["Authorization"] = "Bearer valid-token";

        var claims = new[]
        {
            new Claim("user_id", "uid-123"),
            new Claim(ClaimTypes.NameIdentifier, "cid-456")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        context.User = new ClaimsPrincipal(identity);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        capturedPayload.Should().NotBeNull();
        var json = JsonSerializer.Serialize(capturedPayload!);
        var message = JsonSerializer.Deserialize<LogEventMessage>(json, LogEventMessage.JsonOptions);
        message.Should().NotBeNull();
        // ClaimTypes.NameIdentifier is checked first, so it should take precedence
        message!.UserId.Should().Be("cid-456");
    }
}
