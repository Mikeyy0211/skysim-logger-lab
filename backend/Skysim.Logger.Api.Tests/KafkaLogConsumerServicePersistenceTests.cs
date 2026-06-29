using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Skysim.Logger.Api.Common;
using Skysim.Logger.Api.Infrastructure.Kafka;
using Skysim.Logger.Infrastructure.Entities;
using Skysim.Logger.Infrastructure.Repositories;
using Xunit;
using LogEventMessage = Skysim.Logger.Contracts.Events.LogEventMessage;
using StatusTypes = Skysim.Logger.Contracts.Constants.StatusTypes;
using FlowTypes = Skysim.Logger.Contracts.Constants.FlowTypes;
using ActionTypes = Skysim.Logger.Contracts.Constants.ActionTypes;

namespace Skysim.Logger.Api.Tests;

public class KafkaLogConsumerServicePersistenceTests
{
    [Theory]
    [MemberData(nameof(GetSensitiveFields))]
    public async Task TryUpsertDetailAsync_MasksSensitiveFields_BeforePersisting(string fieldName, string rawValue)
    {
        // Arrange
        var message = new LogEventMessage
        {
            EventId = Guid.NewGuid(),
            FlowId = "test-flow",
            FlowType = FlowTypes.CheckoutEsim,
            ServiceName = "Test",
            ActionType = ActionTypes.OrderCreated,
            Status = StatusTypes.Success,
            CreatedAt = DateTime.UtcNow,
            RequestData = JsonDocument.Parse($"{{\"{fieldName}\":\"{rawValue}\"}}").RootElement.Clone(),
            ResponseData = JsonDocument.Parse($"{{\"{fieldName}\":\"{rawValue}\"}}").RootElement.Clone(),
            ErrorCode = "ERR",
            ErrorMessage = "failed",
            Exception = "System.Exception(\"failed\")"
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
        capturedDetail.ResponsePayload.Should().Contain("***");
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
}
