using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Skysim.Logger.Api.Contracts.DTOs;
using Skysim.Logger.Api.Contracts.DTOs.Queries;
using Skysim.Logger.Api.Domain.Entities;
using Skysim.Logger.Api.Domain.Enums;
using Skysim.Logger.Api.Infrastructure.Persistence;
using Skysim.Logger.Api.Services.Query;
using Xunit;
using Status = Skysim.Logger.Api.Domain.Enums.Status;

namespace Skysim.Logger.Api.Tests.QueryServiceTests;

public class LogFlowQueryServiceTests : IDisposable
{
    private readonly LoggerDbContext _db;
    private readonly IDbContextFactory<LoggerDbContext> _dbFactory;
    private readonly LogFlowQueryService _service;

    public LogFlowQueryServiceTests()
    {
        var options = new DbContextOptionsBuilder<LoggerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new LoggerDbContext(options);
        _dbFactory = new TestDbContextFactory(options);
        _service = new LogFlowQueryService(_dbFactory);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetListAsync_EmptyDb_ReturnsEmptyPage()
    {
        var query = new LogFlowListQuery();

        var result = await _service.GetListAsync(query);

        result.Items.Should().BeEmpty();
        result.Page.Should().Be(1);
        result.TotalItems.Should().Be(0);
        result.TotalPages.Should().Be(0);
    }

    [Fact]
    public async Task GetListAsync_Pagination_ReturnsCorrectSubset()
    {
        await SeedFlowsAsync(25);
        var query = new LogFlowListQuery { Page = 2, PageSize = 10 };

        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(10);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.TotalItems.Should().Be(25);
        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task GetListAsync_SortByCreatedAtDesc_IsDefault()
    {
        var old = CreateFlow("flow-old");
        var recent = CreateFlow("flow-recent");
        old.CreatedAt = DateTime.UtcNow.AddDays(-10);
        recent.CreatedAt = DateTime.UtcNow;
        await _db.LogFlows.AddRangeAsync(old, recent);
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery();
        var result = await _service.GetListAsync(query);

        result.Items[0].FlowId.Should().Be("flow-recent");
        result.Items[1].FlowId.Should().Be("flow-old");
    }

    [Fact]
    public async Task GetListAsync_SortByStatus_HasSecondarySort()
    {
        await _db.LogFlows.AddRangeAsync(
            CreateFlow("flow-aaa", status: Status.Failed),
            CreateFlow("flow-bbb", status: Status.Failed));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery { SortBy = "status", SortDirection = "desc" };
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(2);
        result.Items[0].Status.Should().Be("Failed");
        result.Items[1].Status.Should().Be("Failed");
    }

    [Fact]
    public async Task GetListAsync_FilterByCustomerEmail_CaseInsensitive()
    {
        await SeedFlowsAsync(5);
        await _db.LogFlows.AddAsync(CreateFlow("flow-email-test", customerEmail: "Alice@Example.com"));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery { CustomerEmail = "alice@example.com" };
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].FlowId.Should().Be("flow-email-test");
    }

    [Fact]
    public async Task GetListAsync_FilterByStatus()
    {
        await _db.LogFlows.AddRangeAsync(
            CreateFlow("flow-success", status: Status.Success),
            CreateFlow("flow-failed", status: Status.Failed));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery { Status = "Failed" };
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].FlowId.Should().Be("flow-failed");
    }

    [Fact]
    public async Task GetListAsync_FilterByDateRange()
    {
        var old = CreateFlow("flow-old");
        var recent = CreateFlow("flow-recent");
        old.CreatedAt = DateTime.UtcNow.AddDays(-30);
        recent.CreatedAt = DateTime.UtcNow;
        await _db.LogFlows.AddRangeAsync(old, recent);
        await _db.SaveChangesAsync();

        var from = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd");
        var to = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var query = new LogFlowListQuery { FromDate = from, ToDate = to };

        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].FlowId.Should().Be("flow-recent");
    }

    [Fact]
    public async Task GetListAsync_FilterByServiceName_JoinsLogActions()
    {
        var flowPayment = CreateFlow("flow-payment");
        var flowOrder = CreateFlow("flow-order");
        await _db.LogFlows.AddRangeAsync(flowPayment, flowOrder);
        await _db.SaveChangesAsync();

        await _db.LogActions.AddRangeAsync(
            CreateAction(flowPayment.FlowId, "Payment", ActionType.PaymentSuccess),
            CreateAction(flowOrder.FlowId, "Order", ActionType.OrderCreated));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery { ServiceName = "Payment" };
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].FlowId.Should().Be("flow-payment");
    }

    [Fact]
    public async Task GetListAsync_FilterByMultipleFields_CombinesWithAnd()
    {
        await _db.LogFlows.AddRangeAsync(
            CreateFlow("flow-1", customerEmail: "alice@test.com", status: Status.Success),
            CreateFlow("flow-2", customerEmail: "bob@test.com", status: Status.Failed),
            CreateFlow("flow-3", customerEmail: "alice@test.com", status: Status.Failed));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery { CustomerEmail = "alice@test.com", Status = "Failed" };
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].FlowId.Should().Be("flow-3");
    }

    [Fact]
    public async Task GetByFlowIdAsync_UnknownFlowId_ReturnsNull()
    {
        var result = await _service.GetByFlowIdAsync("unknown-flow-id");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByFlowIdAsync_ExistingFlowId_ReturnsDetailWithTimelineOrderedByStepOrder()
    {
        var flow = CreateFlow("detail-flow");
        await _db.LogFlows.AddAsync(flow);
        await _db.SaveChangesAsync();

        await _db.LogActions.AddRangeAsync(
            CreateAction(flow.FlowId, "Order", ActionType.OrderCreated, stepOrder: 3),
            CreateAction(flow.FlowId, "Order", ActionType.OrderCreated, stepOrder: 1),
            CreateAction(flow.FlowId, "Payment", ActionType.PaymentSuccess, stepOrder: 2));
        await _db.SaveChangesAsync();

        var result = await _service.GetByFlowIdAsync("detail-flow");

        result.Should().NotBeNull();
        result!.Flow.FlowId.Should().Be("detail-flow");
        result.Timeline.Should().HaveCount(3);
        result.Timeline[0].StepOrder.Should().Be(1);
        result.Timeline[1].StepOrder.Should().Be(2);
        result.Timeline[2].StepOrder.Should().Be(3);
    }

    private async Task SeedFlowsAsync(int count)
    {
        var flows = Enumerable.Range(1, count)
            .Select(i => CreateFlow($"flow-{i:D3}"))
            .ToList();
        await _db.LogFlows.AddRangeAsync(flows);
        await _db.SaveChangesAsync();
    }

    private static LogFlow CreateFlow(
        string flowId,
        string? customerEmail = null,
        Status status = Status.InProgress)
    {
        return new LogFlow
        {
            Id = Guid.NewGuid(),
            FlowId = flowId,
            FlowType = FlowType.CheckoutEsim.ToString(),
            CheckoutType = CheckoutType.Guest.ToString(),
            Status = status.ToString(),
            CustomerEmail = customerEmail,
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
