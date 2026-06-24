using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Skysim.Logger.Api.Common;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Contracts.DTOs.Queries;
using Skysim.Logger.Api.Domain.Entities;
using Skysim.Logger.Api.Domain.Enums;
using Skysim.Logger.Api.Infrastructure.Persistence;
using Skysim.Logger.Api.Services.Query;
using Xunit;
using Status = Skysim.Logger.Api.Domain.Enums.Status;

namespace Skysim.Logger.Api.Tests.QueryServiceTests;

public class LogActionQueryServiceTests : IDisposable
{
    private readonly LoggerDbContext _db;
    private readonly IDbContextFactory<LoggerDbContext> _dbFactory;
    private readonly LogActionQueryService _service;

    public LogActionQueryServiceTests()
    {
        var options = new DbContextOptionsBuilder<LoggerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new LoggerDbContext(options);
        _dbFactory = new TestDbContextFactory(options);
        _service = new LogActionQueryService(_dbFactory, new SensitiveDataMasker());
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetByFlowIdAsync_PaginatedList_ScopedToFlowId()
    {
        var flow = CreateFlow("paginated-flow");
        await _db.LogFlows.AddAsync(flow);
        await _db.SaveChangesAsync();

        for (int i = 1; i <= 15; i++)
        {
            await _db.LogActions.AddAsync(CreateAction(flow.FlowId, "Order", ActionType.OrderCreated, i));
        }
        await _db.SaveChangesAsync();

        var query = new LogActionListQuery { FlowId = flow.FlowId, Page = 2, PageSize = 5 };
        var result = await _service.GetByFlowIdAsync(query);

        result.Items.Should().HaveCount(5);
        result.Page.Should().Be(2);
        result.TotalItems.Should().Be(15);
        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task GetByFlowIdAsync_FilterByServiceName()
    {
        var flow = CreateFlow("service-flow");
        await _db.LogFlows.AddAsync(flow);
        await _db.SaveChangesAsync();

        await _db.LogActions.AddRangeAsync(
            CreateAction(flow.FlowId, "Payment", ActionType.PaymentSuccess, 1),
            CreateAction(flow.FlowId, "Order", ActionType.OrderCreated, 2));
        await _db.SaveChangesAsync();

        var query = new LogActionListQuery { FlowId = flow.FlowId, ServiceName = "Payment" };
        var result = await _service.GetByFlowIdAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].ServiceName.Should().Be("Payment");
    }

    [Fact]
    public async Task GetByFlowIdAsync_UnknownFlowId_ReturnsEmptyPage()
    {
        var query = new LogActionListQuery { FlowId = "non-existent-flow" };

        var result = await _service.GetByFlowIdAsync(query);

        result.Items.Should().BeEmpty();
        result.TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task GetDetailsAsync_UnknownActionId_ReturnsNull()
    {
        var result = await _service.GetDetailsAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDetailsAsync_ExistingActionId_ReturnsDetailWithMaskedPayloads()
    {
        var flow = CreateFlow("payload-flow");
        var action = CreateAction(flow.FlowId, "Order", ActionType.OrderCreated);
        await _db.LogFlows.AddAsync(flow);
        await _db.LogActions.AddAsync(action);
        await _db.SaveChangesAsync();

        await _db.LogActionDetails.AddAsync(new LogActionDetail
        {
            Id = Guid.NewGuid(),
            ActionId = action.Id,
            RequestPayload = "{\"orderId\":\"ORD-123\",\"password\":\"secret123\"}",
            ResponsePayload = "{\"status\":\"ok\"}",
            ErrorPayload = null,
            Metadata = "{\"authorization\":\"Bearer token123\"}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await _service.GetDetailsAsync(action.Id);

        result.Should().NotBeNull();
        result!.Action.Id.Should().Be(action.Id);
        result.Action.ServiceName.Should().Be("Order");

        result.RequestPayload.Should().Contain("\"password\":\"***\"");
        result.RequestPayload.Should().Contain("\"orderId\":\"ORD-123\"");

        result.ResponsePayload.Should().Contain("\"status\":\"ok\"");

        result.Metadata.Should().Contain("\"authorization\":\"***\"");
    }

    [Fact]
    public async Task GetDetailsAsync_NullPayloads_StayNull()
    {
        var flow = CreateFlow("null-payload-flow");
        var action = CreateAction(flow.FlowId, "Order", ActionType.OrderCreated);
        await _db.LogFlows.AddAsync(flow);
        await _db.LogActions.AddAsync(action);
        await _db.SaveChangesAsync();

        await _db.LogActionDetails.AddAsync(new LogActionDetail
        {
            Id = Guid.NewGuid(),
            ActionId = action.Id,
            RequestPayload = null,
            ResponsePayload = null,
            ErrorPayload = null,
            Metadata = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var result = await _service.GetDetailsAsync(action.Id);

        result.Should().NotBeNull();
        result!.RequestPayload.Should().BeNull();
        result.ResponsePayload.Should().BeNull();
        result.ErrorPayload.Should().BeNull();
        result.Metadata.Should().BeNull();
    }

    private static LogFlow CreateFlow(string flowId)
    {
        return new LogFlow
        {
            Id = Guid.NewGuid(),
            FlowId = flowId,
            FlowType = FlowType.CheckoutEsim.ToString(),
            CheckoutType = CheckoutType.Guest.ToString(),
            Status = Status.InProgress.ToString(),
            CustomerEmail = "test@test.com",
            CustomerPhone = "+1234567890",
            UserId = null,
            OrderId = null,
            PaymentId = null,
            TotalSteps = 1,
            SuccessSteps = 0,
            FailedSteps = 0,
            LastActionType = ActionType.OrderCreated.ToString(),
            LastMessage = null,
            StartedAt = DateTime.UtcNow,
            CompletedAt = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static LogAction CreateAction(
        string flowId,
        string serviceName,
        ActionType actionType,
        int stepOrder = 1)
    {
        return new LogAction
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            FlowId = flowId,
            StepOrder = stepOrder,
            ServiceName = serviceName,
            ActionType = actionType.ToString(),
            Status = Status.Success.ToString(),
            Message = null,
            ErrorCode = null,
            ErrorMessage = null,
            RequestTime = null,
            ResponseTime = null,
            DurationMs = null,
            CorrelationId = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private class TestDbContextFactory : IDbContextFactory<LoggerDbContext>
    {
        private readonly DbContextOptions<LoggerDbContext> _options;
        public TestDbContextFactory(DbContextOptions<LoggerDbContext> options) => _options = options;
        public LoggerDbContext CreateDbContext() => new(_options);
        public Task<LoggerDbContext> CreateDbContextAsync(CancellationToken ct = default) =>
            Task.FromResult(new LoggerDbContext(_options));
    }
}
