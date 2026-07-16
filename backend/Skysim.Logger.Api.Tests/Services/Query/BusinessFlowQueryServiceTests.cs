using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Skysim.Logger.Api.Contracts.Queries;
using Skysim.Logger.Api.Services.Query;
using Skysim.Logger.Contracts.Constants;
using Skysim.Logger.Infrastructure.Data;
using Skysim.Logger.Infrastructure.Entities;
using Xunit;

namespace Skysim.Logger.Api.Tests.Services.Query;

public class BusinessFlowQueryServiceTests : IDisposable
{
    private readonly LoggerDbContext _db;
    private readonly BusinessFlowQueryService _service;

    public BusinessFlowQueryServiceTests()
    {
        var options = new DbContextOptionsBuilder<LoggerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new LoggerDbContext(options);
        _service = new BusinessFlowQueryService(new TestDbContextFactory(options));
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetListAsync_GroupsMultipleTechnicalFlowsByOrderCode()
    {
        await SeedAsync(
            CreateFlow("flow-a", "ORDER-001", StatusTypes.Success),
            CreateFlow("flow-b", "ORDER-001", StatusTypes.Failed),
            CreateFlow("flow-c", "ORDER-002", StatusTypes.Success));
        await _db.LogActions.AddRangeAsync(
            CreateAction("flow-a", ActionTypes.OrderCreated, StatusTypes.Success, 1),
            CreateAction("flow-b", ActionTypes.HttpRequest, StatusTypes.Failed, 1),
            CreateAction("flow-c", ActionTypes.OrderCreated, StatusTypes.Success, 1));
        await _db.SaveChangesAsync();

        var result = await _service.GetListAsync(new BusinessFlowListQuery { PageSize = 20 });

        result.TotalItems.Should().Be(2);
        result.Items.Should().ContainSingle(item => item.OrderCode == "ORDER-001");
        var grouped = result.Items.Single(item => item.OrderCode == "ORDER-001");
        grouped.TechnicalFlowCount.Should().Be(2);
        grouped.ActionCount.Should().Be(2);
        grouped.OverallStatus.Should().Be(StatusTypes.PartialFailed);
    }

    [Fact]
    public async Task GetListAsync_FiltersBusinessGroupsBeforePagination()
    {
        await SeedAsync(
            CreateFlow("flow-1", "ORDER-001", StatusTypes.Success),
            CreateFlow("flow-2", "ORDER-002", StatusTypes.Failed),
            CreateFlow("flow-3", "ORDER-003", StatusTypes.Success));
        await _db.SaveChangesAsync();

        var result = await _service.GetListAsync(new BusinessFlowListQuery
        {
            Status = StatusTypes.Failed,
            Page = 1,
            PageSize = 1
        });

        result.TotalItems.Should().Be(1);
        result.TotalPages.Should().Be(1);
        result.Items.Single().OrderCode.Should().Be("ORDER-002");
    }

    [Fact]
    public async Task GetListAsync_SearchesStableBusinessFields()
    {
        await SeedAsync(
            CreateFlow("flow-email", "ORDER-EMAIL", StatusTypes.Success, customerEmail: "customer@example.com"),
            CreateFlow("flow-other", "ORDER-OTHER", StatusTypes.Success, paymentId: "PAY-OTHER"));
        await _db.SaveChangesAsync();

        var result = await _service.GetListAsync(new BusinessFlowListQuery { Keyword = "customer@example.com" });

        result.Items.Should().ContainSingle();
        result.Items[0].OrderCode.Should().Be("ORDER-EMAIL");
    }

    [Fact]
    public async Task GetListAsync_PreservesUserAndCustomerEmailSeparately()
    {
        await SeedAsync(CreateFlow(
            "flow-identities",
            "ORDER-IDENTITY",
            StatusTypes.Success,
            userId: "user-123",
            userEmail: "operator@example.com",
            customerEmail: "buyer@example.com"));
        await _db.SaveChangesAsync();

        var result = await _service.GetListAsync(new BusinessFlowListQuery());

        var item = result.Items.Single();
        item.UserId.Should().Be("user-123");
        item.UserEmail.Should().Be("operator@example.com");
        item.CustomerEmail.Should().Be("buyer@example.com");
    }

    [Fact]
    public async Task GetListAsync_ExcludesKnownPaymentStateFromOrderCode()
    {
        await SeedAsync(CreateFlow("flow-invalid", "WAITING_ONEPAY", StatusTypes.Success));

        var result = await _service.GetListAsync(new BusinessFlowListQuery());

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_GroupsByOrderCodeAndExcludesTechnicalNoise()
    {
        await SeedAsync(
            CreateFlow("flow-order-a-1", "ORDER-A", StatusTypes.Success),
            CreateFlow("flow-order-a-2", "ORDER-A", StatusTypes.Failed),
            CreateFlow("flow-order-b", "ORDER-B", StatusTypes.Success),
            CreateFlow("flow-partial", "ORDER-PARTIAL", StatusTypes.PartialFailed),
            CreateFlow("flow-running", "ORDER-RUNNING", StatusTypes.Running),
            CreateFlow("flow-processing", "ORDER-PROCESSING", "PROCESSING"),
            CreateFlow("flow-no-code", null, StatusTypes.Failed),
            CreateFlow("robots.txt-service", "robots.txt-service", StatusTypes.Failed),
            CreateFlow("unknown-service", "unknown-service", StatusTypes.Failed));
        await _db.LogActions.AddRangeAsync(
            CreateAction("flow-order-a-1", ActionTypes.OrderCreated, StatusTypes.Success, 1),
            CreateAction("flow-order-a-2", ActionTypes.HttpRequest, StatusTypes.Failed, 1),
            CreateAction("flow-order-b", ActionTypes.OrderCreated, StatusTypes.Success, 1),
            CreateAction("flow-partial", ActionTypes.HttpRequest, StatusTypes.Failed, 1));
        await _db.SaveChangesAsync();

        var result = await _service.GetDashboardSummaryAsync();

        result.TotalOrders.Should().Be(5);
        result.RunningOrders.Should().Be(2);
        result.RequiresAttentionOrders.Should().Be(2);
        result.CompletedOrders.Should().Be(1);
        result.CompletionRate.Should().BeApproximately(33.33, 0.01);
        result.RecentRequiresAttention.Select(item => item.OrderCode)
            .Should().NotContain("robots.txt-service")
            .And.NotContain("unknown-service");
        result.RecentCompleted.Should().ContainSingle(item => item.OrderCode == "ORDER-B");
        result.RecentRequiresAttention.Should().ContainSingle(item => item.OrderCode == "ORDER-A");
        result.RecentRequiresAttention.Single(item => item.OrderCode == "ORDER-A").TechnicalFlowCount.Should().Be(2);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_PrioritizesFailedAndKeepsIdentityFieldsIndependent()
    {
        await SeedAsync(
            CreateFlow("flow-failed", "ORDER-FAILED", StatusTypes.Failed, userEmail: "operator@example.com", customerEmail: "buyer@example.com"),
            CreateFlow("flow-partial", "ORDER-PARTIAL", StatusTypes.PartialFailed),
            CreateFlow("flow-success", "ORDER-SUCCESS", StatusTypes.Success));
        await _db.LogActions.AddRangeAsync(
            CreateAction("flow-failed", ActionTypes.HttpRequest, StatusTypes.Failed, 1),
            CreateAction("flow-partial", ActionTypes.HttpRequest, StatusTypes.Failed, 1),
            CreateAction("flow-success", ActionTypes.OrderCreated, StatusTypes.Success, 1));
        await _db.SaveChangesAsync();

        var beforeCount = await _db.LogFlows.CountAsync();
        var result = await _service.GetDashboardSummaryAsync();
        var afterCount = await _db.LogFlows.CountAsync();

        result.RecentRequiresAttention[0].OrderCode.Should().Be("ORDER-FAILED");
        result.RecentRequiresAttention[0].UserEmail.Should().Be("operator@example.com");
        result.RecentRequiresAttention[0].CustomerEmail.Should().Be("buyer@example.com");
        result.RecentCompleted.Should().ContainSingle(item => item.Status == StatusTypes.Success);
        afterCount.Should().Be(beforeCount);
    }

    private async Task SeedAsync(params LogFlow[] flows)
    {
        await _db.LogFlows.AddRangeAsync(flows);
        await _db.SaveChangesAsync();
    }

    private static LogFlow CreateFlow(
        string flowId,
        string? orderCode,
        string status,
        string? userId = null,
        string? userEmail = null,
        string? customerEmail = null,
        string? paymentId = null)
    {
        var now = DateTime.UtcNow;
        return new LogFlow
        {
            Id = Guid.NewGuid(),
            FlowId = flowId,
            FlowType = FlowTypes.CheckoutEsim,
            Status = status,
            UserId = userId,
            UserEmail = userEmail,
            CustomerEmail = customerEmail,
            OrderCode = orderCode,
            PaymentId = paymentId,
            TotalSteps = 1,
            SuccessSteps = status == StatusTypes.Success ? 1 : 0,
            FailedSteps = status == StatusTypes.Failed ? 1 : 0,
            StartedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private static LogAction CreateAction(string flowId, string actionType, string status, int stepOrder)
    {
        var now = DateTime.UtcNow;
        return new LogAction
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            FlowId = flowId,
            StepOrder = stepOrder,
            ServiceName = "test-service",
            ActionType = actionType,
            Status = status,
            Message = status == StatusTypes.Failed ? "POST /apis/payment/transaction/check -> 401 (12ms)" : "Action completed",
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private sealed class TestDbContextFactory : IDbContextFactory<LoggerDbContext>
    {
        private readonly DbContextOptions<LoggerDbContext> _options;

        public TestDbContextFactory(DbContextOptions<LoggerDbContext> options) => _options = options;

        public LoggerDbContext CreateDbContext() => new(_options);

        public Task<LoggerDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new LoggerDbContext(_options));
    }
}
