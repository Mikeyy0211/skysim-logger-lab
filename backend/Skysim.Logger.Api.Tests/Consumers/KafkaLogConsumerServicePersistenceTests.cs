using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Skysim.Logger.Api.Consumers;
using Skysim.Logger.Api.Kafka;
using Skysim.Logger.Client.Masking;
using Skysim.Logger.Infrastructure.Entities;
using Skysim.Logger.Infrastructure.Repositories;
using Xunit;
using LogEventMessage = Skysim.Logger.Contracts.Events.LogEventMessage;
using StatusTypes = Skysim.Logger.Contracts.Constants.StatusTypes;
using FlowTypes = Skysim.Logger.Contracts.Constants.FlowTypes;
using ActionTypes = Skysim.Logger.Contracts.Constants.ActionTypes;

namespace Skysim.Logger.Api.Tests.Consumers;

public class KafkaLogConsumerServicePersistenceTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    #region Test 1: HTTP_REQUEST with requestBody/responseBody → request_payload + response_payload

    [Fact]
    public async Task TryUpsertDetailAsync_HttpRequestWithBodies_PopulatesRequestAndResponsePayloads()
    {
        // Arrange
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-http-001",
            FlowType = FlowTypes.HttpAction,
            ServiceName = "TestService",
            ActionType = ActionTypes.HttpRequest,
            Status = StatusTypes.Success,
            CreatedAt = DateTime.UtcNow,
            Method = "POST",
            Path = "/api/checkout",
            QueryString = "plan=unlimited",
            FullUrl = "https://api.example.com/api/checkout?plan=unlimited",
            ClientIp = "192.168.1.100",
            SourceService = "Frontend",
            RequestHeaders = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
                ["Authorization"] = "Bearer ***MASKED***"
            },
            RequestBody = "{\"email\":\"test@example.com\"}",
            StatusCode = 200,
            ResponseHeaders = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json"
            },
            ResponseBody = "{\"orderId\":\"ORD-123\",\"status\":\"success\"}",
            DurationMs = 350
        };

        var detailRepoMock = new Mock<ILogActionDetailRepository>();
        LogActionDetail? capturedDetail = null;
        detailRepoMock
            .Setup(repo => repo.UpsertAsync(It.IsAny<LogActionDetail>(), It.IsAny<CancellationToken>()))
            .Callback<LogActionDetail, CancellationToken>((detail, _) => capturedDetail = detail)
            .Returns(Task.FromResult(new LogActionDetail()));

        var service = CreateServiceWithMocks(detailRepoMock.Object);

        // Act
        await InvokeTryUpsertDetailAsync(service, detailRepoMock.Object, Guid.NewGuid(), message);

        // Assert
        capturedDetail.Should().NotBeNull();
        capturedDetail!.RequestPayload.Should().NotBeNull();
        capturedDetail.ResponsePayload.Should().NotBeNull();
        capturedDetail.ErrorPayload.Should().BeNull();

        var requestPayload = JsonSerializer.Deserialize<JsonElement>(capturedDetail.RequestPayload!, JsonOpts);
        requestPayload.GetProperty("method").GetString().Should().Be("POST");
        requestPayload.GetProperty("path").GetString().Should().Be("/api/checkout");
        requestPayload.GetProperty("queryString").GetString().Should().Be("plan=unlimited");
        requestPayload.GetProperty("fullUrl").GetString().Should().Be("https://api.example.com/api/checkout?plan=unlimited");
        requestPayload.GetProperty("clientIp").GetString().Should().Be("192.168.1.100");
        requestPayload.GetProperty("sourceService").GetString().Should().Be("Frontend");
        requestPayload.GetProperty("body").GetString().Should().Contain("test@example.com");

        var responsePayload = JsonSerializer.Deserialize<JsonElement>(capturedDetail.ResponsePayload!, JsonOpts);
        responsePayload.GetProperty("statusCode").GetInt32().Should().Be(200);
        responsePayload.GetProperty("body").GetString().Should().Contain("ORD-123");
        responsePayload.GetProperty("durationMs").GetInt32().Should().Be(350);
    }

    #endregion

    #region Test 2: HTTP_REQUEST with auth fields → metadata has authResult/hasAuthorization/authScheme

    [Fact]
    public async Task TryUpsertDetailAsync_HttpRequestWithAuth_PopulatesMetadataWithAuthFields()
    {
        // Arrange
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-auth-001",
            FlowType = FlowTypes.HttpAction,
            ServiceName = "TestService",
            ActionType = ActionTypes.HttpRequest,
            Status = StatusTypes.Success,
            CreatedAt = DateTime.UtcNow,
            Method = "GET",
            Path = "/api/orders",
            HasAuthorization = true,
            AuthScheme = "Bearer",
            IsAuthenticated = true,
            UserId = "user-456",
            Username = "johndoe",
            UserEmail = "john@example.com",
            Roles = new List<string> { "admin", "user" },
            AuthResult = "SUCCESS",
            CorrelationId = "corr-789"
        };

        var detailRepoMock = new Mock<ILogActionDetailRepository>();
        LogActionDetail? capturedDetail = null;
        detailRepoMock
            .Setup(repo => repo.UpsertAsync(It.IsAny<LogActionDetail>(), It.IsAny<CancellationToken>()))
            .Callback<LogActionDetail, CancellationToken>((detail, _) => capturedDetail = detail)
            .Returns(Task.FromResult(new LogActionDetail()));

        var service = CreateServiceWithMocks(detailRepoMock.Object);

        // Act
        await InvokeTryUpsertDetailAsync(service, detailRepoMock.Object, Guid.NewGuid(), message);

        // Assert
        capturedDetail.Should().NotBeNull();
        capturedDetail!.Metadata.Should().NotBeNull();

        var metadata = JsonSerializer.Deserialize<JsonElement>(capturedDetail.Metadata!, JsonOpts);
        metadata.GetProperty("hasAuthorization").GetBoolean().Should().BeTrue();
        metadata.GetProperty("authScheme").GetString().Should().Be("Bearer");
        metadata.GetProperty("isAuthenticated").GetBoolean().Should().BeTrue();
        metadata.GetProperty("userId").GetString().Should().Be("user-456");
        metadata.GetProperty("username").GetString().Should().Be("johndoe");
        metadata.GetProperty("userEmail").GetString().Should().Be("john@example.com");
        metadata.GetProperty("roles")[0].GetString().Should().Be("admin");
        metadata.GetProperty("authResult").GetString().Should().Be("SUCCESS");
        metadata.GetProperty("correlationId").GetString().Should().Be("corr-789");
        metadata.GetProperty("flowType").GetString().Should().Be(FlowTypes.HttpAction);
        metadata.GetProperty("actionType").GetString().Should().Be(ActionTypes.HttpRequest);
        metadata.GetProperty("status").GetString().Should().Be(StatusTypes.Success);
    }

    [Fact]
    public async Task TryUpsertDetailAsync_GuestNoToken_CreatesMetadataWithNoTokenAuthResult()
    {
        // Arrange - guest/no-token case: all booleans false, but authResult signals the auth state
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-guest-001",
            FlowType = FlowTypes.CheckoutEsim,
            ServiceName = "TestService",
            ActionType = ActionTypes.HttpRequest,
            Status = StatusTypes.Success,
            CreatedAt = DateTime.UtcNow,
            Method = "GET",
            Path = "/api/checkout",
            HasAuthorization = false,
            IsAuthenticated = false,
            AuthResult = "NO_TOKEN"
        };

        var detailRepoMock = new Mock<ILogActionDetailRepository>();
        LogActionDetail? capturedDetail = null;
        detailRepoMock
            .Setup(repo => repo.UpsertAsync(It.IsAny<LogActionDetail>(), It.IsAny<CancellationToken>()))
            .Callback<LogActionDetail, CancellationToken>((detail, _) => capturedDetail = detail)
            .Returns(Task.FromResult(new LogActionDetail()));

        var service = CreateServiceWithMocks(detailRepoMock.Object);

        // Act
        await InvokeTryUpsertDetailAsync(service, detailRepoMock.Object, Guid.NewGuid(), message);

        // Assert
        capturedDetail.Should().NotBeNull();
        capturedDetail!.Metadata.Should().NotBeNull();

        var metadata = JsonSerializer.Deserialize<JsonElement>(capturedDetail.Metadata!, JsonOpts);
        metadata.GetProperty("hasAuthorization").GetBoolean().Should().BeFalse();
        metadata.GetProperty("isAuthenticated").GetBoolean().Should().BeFalse();
        metadata.GetProperty("authResult").GetString().Should().Be("NO_TOKEN");
    }

    #endregion

    #region Test 3: Error event with errorMessage → error_payload has data

    [Fact]
    public async Task TryUpsertDetailAsync_ErrorEvent_PopulatesErrorPayload()
    {
        // Arrange
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-error-001",
            FlowType = FlowTypes.CheckoutEsim,
            ServiceName = "PaymentService",
            ActionType = ActionTypes.PaymentFailed,
            Status = StatusTypes.Failed,
            CreatedAt = DateTime.UtcNow,
            ErrorCode = "PAYMENT_DECLINED",
            ErrorMessage = "Card declined by issuer",
            Exception = "PaymentException: Card declined"
        };

        var detailRepoMock = new Mock<ILogActionDetailRepository>();
        LogActionDetail? capturedDetail = null;
        detailRepoMock
            .Setup(repo => repo.UpsertAsync(It.IsAny<LogActionDetail>(), It.IsAny<CancellationToken>()))
            .Callback<LogActionDetail, CancellationToken>((detail, _) => capturedDetail = detail)
            .Returns(Task.FromResult(new LogActionDetail()));

        var service = CreateServiceWithMocks(detailRepoMock.Object);

        // Act
        await InvokeTryUpsertDetailAsync(service, detailRepoMock.Object, Guid.NewGuid(), message);

        // Assert
        capturedDetail.Should().NotBeNull();
        capturedDetail!.ErrorPayload.Should().NotBeNull();

        var errorPayload = JsonSerializer.Deserialize<JsonElement>(capturedDetail.ErrorPayload!, JsonOpts);
        errorPayload.GetProperty("errorCode").GetString().Should().Be("PAYMENT_DECLINED");
        errorPayload.GetProperty("errorMessage").GetString().Should().Be("Card declined by issuer");
        errorPayload.GetProperty("exception").GetString().Should().Contain("PaymentException");
    }

    #endregion

    #region Test 4: No duplicate detail if action_id already exists (upsert)

    [Fact]
    public async Task TryUpsertDetailAsync_DuplicateActionId_ProducesOneDetailRecordPerActionId()
    {
        // Arrange
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-upsert-001",
            FlowType = FlowTypes.HttpAction,
            ServiceName = "TestService",
            ActionType = ActionTypes.HttpRequest,
            Status = StatusTypes.Success,
            CreatedAt = DateTime.UtcNow,
            Method = "POST",
            Path = "/api/orders",
            StatusCode = 201,
            DurationMs = 100
        };

        var detailRepoMock = new Mock<ILogActionDetailRepository>();
        var capturedActionIds = new List<Guid>();

        // First call: upsert creates a new record (returns detail with Id set)
        // Second call: upsert updates existing record (returns detail with same Id)
        var existingDetail = new LogActionDetail { Id = Guid.NewGuid(), ActionId = Guid.Empty };
        bool firstCall = true;

        detailRepoMock
            .Setup(repo => repo.UpsertAsync(It.IsAny<LogActionDetail>(), It.IsAny<CancellationToken>()))
            .Callback<LogActionDetail, CancellationToken>((detail, _) => capturedActionIds.Add(detail.ActionId))
            .Returns(() =>
            {
                if (firstCall)
                {
                    firstCall = false;
                    return Task.FromResult(new LogActionDetail { Id = existingDetail.Id, ActionId = capturedActionIds.Last() });
                }
                return Task.FromResult(existingDetail);
            });

        var service = CreateServiceWithMocks(detailRepoMock.Object);

        // Act
        var actionId = Guid.NewGuid();
        await InvokeTryUpsertDetailAsync(service, detailRepoMock.Object, actionId, message);
        await InvokeTryUpsertDetailAsync(service, detailRepoMock.Object, actionId, message);

        // Assert
        capturedActionIds.Should().HaveCount(2);
        capturedActionIds.Should().AllBeEquivalentTo(actionId, "each upsert must use the same actionId to produce one detail record per action_id");
    }

    #endregion

    #region Test: Sensitive fields are masked

    [Theory]
    [MemberData(nameof(GetSensitiveFields))]
    public async Task TryUpsertDetailAsync_SensitiveFieldsInRequestBody_AreMasked(string fieldName, string rawValue)
    {
        // Arrange
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-sensitive-001",
            FlowType = FlowTypes.HttpAction,
            ServiceName = "TestService",
            ActionType = ActionTypes.HttpRequest,
            Status = StatusTypes.Success,
            CreatedAt = DateTime.UtcNow,
            Method = "POST",
            Path = "/api/login",
            RequestBody = $"{{\"{fieldName}\":\"{rawValue}\"}}"
        };

        var detailRepoMock = new Mock<ILogActionDetailRepository>();
        LogActionDetail? capturedDetail = null;
        detailRepoMock
            .Setup(repo => repo.UpsertAsync(It.IsAny<LogActionDetail>(), It.IsAny<CancellationToken>()))
            .Callback<LogActionDetail, CancellationToken>((detail, _) => capturedDetail = detail)
            .Returns(Task.FromResult(new LogActionDetail()));

        var service = CreateServiceWithMocks(detailRepoMock.Object);

        // Act
        await InvokeTryUpsertDetailAsync(service, detailRepoMock.Object, Guid.NewGuid(), message);

        // Assert
        capturedDetail.Should().NotBeNull();
        capturedDetail!.RequestPayload.Should().Contain("***");
        capturedDetail.RequestPayload.Should().NotContain(rawValue);
    }

    [Theory]
    [MemberData(nameof(GetSensitiveFields))]
    public async Task TryUpsertDetailAsync_SensitiveFieldsInResponseBody_AreMasked(string fieldName, string rawValue)
    {
        // Arrange
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-sensitive-002",
            FlowType = FlowTypes.HttpAction,
            ServiceName = "TestService",
            ActionType = ActionTypes.HttpRequest,
            Status = StatusTypes.Success,
            CreatedAt = DateTime.UtcNow,
            ResponseBody = $"{{\"{fieldName}\":\"{rawValue}\"}}"
        };

        var detailRepoMock = new Mock<ILogActionDetailRepository>();
        LogActionDetail? capturedDetail = null;
        detailRepoMock
            .Setup(repo => repo.UpsertAsync(It.IsAny<LogActionDetail>(), It.IsAny<CancellationToken>()))
            .Callback<LogActionDetail, CancellationToken>((detail, _) => capturedDetail = detail)
            .Returns(Task.FromResult(new LogActionDetail()));

        var service = CreateServiceWithMocks(detailRepoMock.Object);

        // Act
        await InvokeTryUpsertDetailAsync(service, detailRepoMock.Object, Guid.NewGuid(), message);

        // Assert
        capturedDetail.Should().NotBeNull();
        capturedDetail!.ResponsePayload.Should().Contain("***");
        capturedDetail.ResponsePayload.Should().NotContain(rawValue);
    }

    public static IEnumerable<object[]> GetSensitiveFields()
    {
        var fields = new[]
        {
            ("password", "secret123"),
            ("token", "secret-token"),
            ("authorization", "Bearer secret"),
            ("access_token", "access-secret"),
            ("refresh_token", "refresh-secret"),
            ("secret", "my-secret"),
            ("cardNumber", "4111111111111111"),
            ("cvv", "123"),
            ("paymentSecret", "pay-secret"),
            ("otp", "123456")
        };

        foreach (var field in fields)
        {
            yield return new object[] { field.Item1, field.Item2 };
        }
    }

    #endregion

    #region Test: No detail created when nothing to save

    [Fact]
    public async Task TryUpsertDetailAsync_NoHttpData_DoesNotCallRepository()
    {
        // Arrange - business event with no HTTP data, no error, no auth fields
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-business-001",
            FlowType = FlowTypes.CheckoutEsim,
            ServiceName = "OrderService",
            ActionType = ActionTypes.OrderCreated,
            Status = StatusTypes.Success,
            CreatedAt = DateTime.UtcNow,
            CheckoutType = "GUEST",
            CustomerEmail = "guest@example.com",
            OrderId = "ORD-001"
            // No HTTP fields, no auth fields, no error fields
        };

        var detailRepoMock = new Mock<ILogActionDetailRepository>();
        var service = CreateServiceWithMocks(detailRepoMock.Object);

        // Act
        await InvokeTryUpsertDetailAsync(service, detailRepoMock.Object, Guid.NewGuid(), message);

        // Assert - repository should NOT be called since there's nothing to save
        detailRepoMock.Verify(
            repo => repo.UpsertAsync(It.IsAny<LogActionDetail>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Test: All four payloads can coexist

    [Fact]
    public async Task TryUpsertDetailAsync_FullHttpResponseWithAuthAndError_PopulatesAllFourPayloads()
    {
        // Arrange
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "flow-full-001",
            FlowType = FlowTypes.HttpAction,
            ServiceName = "TestService",
            ActionType = ActionTypes.HttpRequest,
            Status = StatusTypes.Failed,
            CreatedAt = DateTime.UtcNow,
            Method = "POST",
            Path = "/api/orders",
            RequestBody = "{\"amount\":100}",
            StatusCode = 500,
            ResponseBody = "{\"error\":\"Internal error\"}",
            DurationMs = 500,
            ErrorCode = "INTERNAL_ERROR",
            ErrorMessage = "Unexpected error",
            HasAuthorization = true,
            AuthScheme = "Bearer",
            IsAuthenticated = true,
            UserId = "user-001",
            AuthResult = "SUCCESS",
            CorrelationId = "corr-001"
        };

        var detailRepoMock = new Mock<ILogActionDetailRepository>();
        LogActionDetail? capturedDetail = null;
        detailRepoMock
            .Setup(repo => repo.UpsertAsync(It.IsAny<LogActionDetail>(), It.IsAny<CancellationToken>()))
            .Callback<LogActionDetail, CancellationToken>((detail, _) => capturedDetail = detail)
            .Returns(Task.FromResult(new LogActionDetail()));

        var service = CreateServiceWithMocks(detailRepoMock.Object);

        // Act
        await InvokeTryUpsertDetailAsync(service, detailRepoMock.Object, Guid.NewGuid(), message);

        // Assert
        capturedDetail.Should().NotBeNull();
        capturedDetail!.RequestPayload.Should().NotBeNull();
        capturedDetail.ResponsePayload.Should().NotBeNull();
        capturedDetail.ErrorPayload.Should().NotBeNull();
        capturedDetail.Metadata.Should().NotBeNull();

        // Verify each payload is valid JSON
        JsonSerializer.Deserialize<JsonElement>(capturedDetail.RequestPayload!, JsonOpts).Should().NotBeNull();
        JsonSerializer.Deserialize<JsonElement>(capturedDetail.ResponsePayload!, JsonOpts).Should().NotBeNull();
        JsonSerializer.Deserialize<JsonElement>(capturedDetail.ErrorPayload!, JsonOpts).Should().NotBeNull();
        JsonSerializer.Deserialize<JsonElement>(capturedDetail.Metadata!, JsonOpts).Should().NotBeNull();
    }

    #endregion

    #region Test helpers

    private static KafkaLogConsumerService CreateServiceWithMocks(ILogActionDetailRepository detailRepo)
    {
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var scopeMock = new Mock<IServiceScope>();
        var serviceProviderMock = new Mock<IServiceProvider>();

        serviceProviderMock
            .Setup(provider => provider.GetService(typeof(ILogActionDetailRepository)))
            .Returns(detailRepo);

        scopeMock
            .Setup(scope => scope.ServiceProvider)
            .Returns(serviceProviderMock.Object);

        scopeFactoryMock
            .Setup(factory => factory.CreateScope())
            .Returns(scopeMock.Object);

        var options = new KafkaConsumerOptions();
        var loggerMock = new Mock<ILogger<KafkaLogConsumerService>>();
        var masker = new SensitiveDataMasker();

        return new KafkaLogConsumerService(
            scopeFactoryMock.Object,
            Mock.Of<Microsoft.Extensions.Options.IOptions<KafkaConsumerOptions>>(o => o.Value == options),
            Mock.Of<IDlqPublisher>(),
            loggerMock.Object,
            masker);
    }

    private static Task InvokeTryUpsertDetailAsync(
        KafkaLogConsumerService service,
        ILogActionDetailRepository detailRepo,
        Guid actionId,
        LogEventMessage message)
    {
        var method = typeof(KafkaLogConsumerService)
            .GetMethod("TryUpsertDetailAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        return (Task)method!.Invoke(service, new object[] { detailRepo, actionId, message, CancellationToken.None })!;
    }

    #endregion
}
