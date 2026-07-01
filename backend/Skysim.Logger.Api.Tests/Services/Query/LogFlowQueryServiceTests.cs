using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Skysim.Logger.Api.Contracts.Queries;
using Skysim.Logger.Api.Services.Query;
using Skysim.Logger.Infrastructure.Data;
using Skysim.Logger.Infrastructure.Entities;
using Xunit;
using StatusTypes = Skysim.Logger.Contracts.Constants.StatusTypes;
using CheckoutTypes = Skysim.Logger.Contracts.Constants.CheckoutTypes;
using FlowTypes = Skysim.Logger.Contracts.Constants.FlowTypes;
using ActionTypes = Skysim.Logger.Contracts.Constants.ActionTypes;

namespace Skysim.Logger.Api.Tests.Services.Query;

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
    public async Task GetListAsync_SortByUpdatedAtDesc_IsDefault()
    {
        var old = CreateFlow("flow-old");
        var recent = CreateFlow("flow-recent");
        old.UpdatedAt = DateTime.UtcNow.AddDays(-10);
        recent.UpdatedAt = DateTime.UtcNow;
        await _db.LogFlows.AddRangeAsync(old, recent);
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery();
        var result = await _service.GetListAsync(query);

        result.Items[0].FlowId.Should().Be("flow-recent");
        result.Items[1].FlowId.Should().Be("flow-old");
    }

    [Fact]
    public async Task GetListAsync_SearchMatchesFlowId_CaseInsensitive()
    {
        await _db.LogFlows.AddRangeAsync(
            CreateFlow("demo-business-flow"),
            CreateFlow("other-flow"));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery { Search = "demo-business-flow" };
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].FlowId.Should().Be("demo-business-flow");
    }

    [Fact]
    public async Task GetListAsync_SearchMatchesCustomerEmail_CaseInsensitive()
    {
        await _db.LogFlows.AddRangeAsync(
            CreateFlow("flow-email-1", customerEmail: "detail.demo@example.com"),
            CreateFlow("flow-email-2", customerEmail: "other@test.com"));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery { Search = "detail.demo@example.com" };
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].FlowId.Should().Be("flow-email-1");
    }

    [Fact]
    public async Task GetListAsync_SearchMatchesCustomerPhone_CaseInsensitive()
    {
        await _db.LogFlows.AddRangeAsync(
            CreateFlow("flow-phone-1", customerPhone: "0900000003"),
            CreateFlow("flow-phone-2", customerPhone: "0123456789"));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery { Search = "0900000003" };
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].FlowId.Should().Be("flow-phone-1");
    }

    [Fact]
    public async Task GetListAsync_SearchMatchesOrderId_CaseInsensitive()
    {
        await _db.LogFlows.AddRangeAsync(
            CreateFlow("flow-0001-order", orderId: "ORDER-XYZ-001"),
            CreateFlow("flow-0002-other", paymentId: "PAYMENT-XYZ-002"));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery { Search = "ORDER-XYZ-001" };
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].FlowId.Should().Be("flow-0001-order");
    }

    [Fact]
    public async Task GetListAsync_SearchMatchesPaymentId_CaseInsensitive()
    {
        await _db.LogFlows.AddRangeAsync(
            CreateFlow("flow-0003-payment", paymentId: "PAYMENT-ABC-001"),
            CreateFlow("flow-0004-other", orderId: "ORDER-DEF-002"));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery { Search = "PAYMENT-ABC" };
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].FlowId.Should().Be("flow-0003-payment");
    }

    [Fact]
    public async Task GetListAsync_SearchMatchesUserId_CaseInsensitive()
    {
        await _db.LogFlows.AddRangeAsync(
            CreateFlow("flow-user-1", userId: "user-42"),
            CreateFlow("flow-user-2", userId: "guest-99"));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery { Search = "user-42" };
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].FlowId.Should().Be("flow-user-1");
    }

    [Fact]
    public async Task GetListAsync_SearchMatchesLastMessage_CaseInsensitive()
    {
        await _db.LogFlows.AddRangeAsync(
            CreateFlow("flow-msg-1", lastMessage: "payment timeout error"),
            CreateFlow("flow-msg-2", lastMessage: "order created successfully"));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery { Search = "payment timeout" };
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].FlowId.Should().Be("flow-msg-1");
    }

    [Fact]
    public async Task GetListAsync_SearchCaseInsensitive_MatchesUppercaseSearch()
    {
        await _db.LogFlows.AddRangeAsync(
            CreateFlow("flow-lower", customerEmail: "demo@example.com"),
            CreateFlow("flow-upper", customerEmail: "DEMO@EXAMPLE.COM"));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery { Search = "DEMO" };
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetListAsync_SearchCombinedWithFlowTypeFilter()
    {
        await _db.LogFlows.AddRangeAsync(
            CreateFlow("flow-checkout-1", flowType: FlowTypes.CheckoutEsim, customerEmail: "detail.demo@example.com"),
            CreateFlow("flow-http-1", flowType: FlowTypes.HttpAction, customerEmail: "detail.demo@example.com"),
            CreateFlow("flow-checkout-2", flowType: FlowTypes.CheckoutEsim, customerEmail: "other@example.com"));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery { Search = "detail.demo@example.com", FlowType = FlowTypes.CheckoutEsim };
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].FlowId.Should().Be("flow-checkout-1");
    }

    [Fact]
    public async Task GetListAsync_SearchCombinedWithStatusFilter()
    {
        await _db.LogFlows.AddRangeAsync(
            CreateFlow("flow-success-1", status: StatusTypes.Success, customerEmail: "alice@example.com"),
            CreateFlow("flow-failed-1", status: StatusTypes.Failed, customerEmail: "alice@example.com"));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery { Search = "alice@example.com", Status = StatusTypes.Success };
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].FlowId.Should().Be("flow-success-1");
    }

    [Fact]
    public async Task GetListAsync_SearchCombinedWithCheckoutTypeFilter()
    {
        await _db.LogFlows.AddRangeAsync(
            CreateFlow("flow-guest", checkoutType: CheckoutTypes.Guest, customerEmail: "bob@example.com"),
            CreateFlow("flow-auth", checkoutType: CheckoutTypes.Authenticated, customerEmail: "bob@example.com"));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery { Search = "bob@example.com", CheckoutType = CheckoutTypes.Guest };
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].FlowId.Should().Be("flow-guest");
    }

    [Fact]
    public async Task GetListAsync_SearchCombinedWithMultipleFilters()
    {
        await _db.LogFlows.AddRangeAsync(
            CreateFlow("flow-1", customerEmail: "carol@example.com", flowType: FlowTypes.CheckoutEsim, status: StatusTypes.Success),
            CreateFlow("flow-2", customerEmail: "carol@example.com", flowType: FlowTypes.CheckoutEsim, status: StatusTypes.Failed),
            CreateFlow("flow-3", customerEmail: "carol@example.com", flowType: FlowTypes.HttpAction, status: StatusTypes.Success));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery
        {
            Search = "carol@example.com",
            FlowType = FlowTypes.CheckoutEsim,
            Status = StatusTypes.Success
        };
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].FlowId.Should().Be("flow-1");
    }

    [Fact]
    public async Task GetListAsync_PaginationWithSearch_ReturnsCorrectPage()
    {
        foreach (var i in Enumerable.Range(1, 15))
        {
            await _db.LogFlows.AddAsync(CreateFlow($"search-flow-{i:D2}", customerEmail: "paged@example.com"));
        }
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery { Search = "paged@example.com", Page = 2, PageSize = 5 };
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(5);
        result.Page.Should().Be(2);
        result.TotalItems.Should().Be(15);
        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task GetListAsync_LastServiceName_ReflectsLatestBusinessActionByStepOrder()
    {
        var flow = CreateFlow("flow-with-actions");
        await _db.LogFlows.AddAsync(flow);
        await _db.SaveChangesAsync();

        await _db.LogActions.AddRangeAsync(
            CreateAction(flow.FlowId, "OrderService", ActionTypes.OrderCreated, stepOrder: 1),
            CreateAction(flow.FlowId, "PaymentService", ActionTypes.PaymentSuccess, stepOrder: 2),
            CreateAction(flow.FlowId, "ProviderService", ActionTypes.EsimActivated, stepOrder: 3));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery();
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].LastServiceName.Should().Be("ProviderService");
    }

    [Fact]
    public async Task GetListAsync_LastServiceName_IsNullWhenNoActions()
    {
        var flow = CreateFlow("flow-no-actions");
        await _db.LogFlows.AddAsync(flow);
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery();
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].LastServiceName.Should().BeNull();
    }

    [Fact]
    public async Task GetListAsync_LastServiceName_ReturnsNotificationServiceForCheckoutWithEmailSent()
    {
        var flow = CreateFlow("flow-checkout-email", flowType: FlowTypes.CheckoutEsim);
        await _db.LogFlows.AddAsync(flow);
        await _db.SaveChangesAsync();

        await _db.LogActions.AddRangeAsync(
            CreateAction(flow.FlowId, "OrderService", ActionTypes.OrderCreated, stepOrder: 1),
            CreateAction(flow.FlowId, "PaymentService", ActionTypes.PaymentSuccess, stepOrder: 2),
            CreateAction(flow.FlowId, "NotificationService", ActionTypes.EmailSent, stepOrder: 3));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery();
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].LastServiceName.Should().Be("NotificationService");
    }

    [Fact]
    public async Task GetListAsync_LastServiceName_CheckoutIgnoresHttpRequestWhenItIsLatestStep()
    {
        var flow = CreateFlow("flow-checkout-with-http", flowType: FlowTypes.CheckoutEsim);
        await _db.LogFlows.AddAsync(flow);
        await _db.SaveChangesAsync();

        await _db.LogActions.AddRangeAsync(
            CreateAction(flow.FlowId, "OrderService", ActionTypes.OrderCreated, stepOrder: 1),
            CreateAction(flow.FlowId, "PaymentService", ActionTypes.PaymentSuccess, stepOrder: 2),
            CreateAction(flow.FlowId, "NotificationService", ActionTypes.EmailSent, stepOrder: 3),
            CreateAction(flow.FlowId, "sample-checkout-service", ActionTypes.HttpRequest, stepOrder: 4));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery();
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].LastServiceName.Should().Be("NotificationService");
    }

    [Fact]
    public async Task GetListAsync_LastServiceName_HttpActionStillUsesLatestStepOrder()
    {
        var flow = CreateFlow("flow-http-action", flowType: FlowTypes.HttpAction);
        await _db.LogFlows.AddAsync(flow);
        await _db.SaveChangesAsync();

        await _db.LogActions.AddAsync(CreateAction(flow.FlowId, "sample-checkout-service", ActionTypes.HttpRequest, stepOrder: 1));
        await _db.SaveChangesAsync();

        var query = new LogFlowListQuery();
        var result = await _service.GetListAsync(query);

        result.Items.Should().HaveCount(1);
        result.Items[0].LastServiceName.Should().Be("sample-checkout-service");
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
        string? customerPhone = null,
        string? userId = null,
        string? orderId = null,
        string? paymentId = null,
        string? lastMessage = null,
        string flowType = "CHECKOUT_ESIM",
        string checkoutType = "GUEST",
        string status = "IN_PROGRESS")
    {
        return new LogFlow
        {
            Id = Guid.NewGuid(),
            FlowId = flowId,
            FlowType = flowType,
            CheckoutType = checkoutType,
            Status = status,
            CustomerEmail = customerEmail,
            CustomerPhone = customerPhone,
            UserId = userId,
            OrderId = orderId,
            PaymentId = paymentId,
            TotalSteps = 1,
            SuccessSteps = 0,
            FailedSteps = 0,
            LastActionType = ActionTypes.OrderCreated,
            LastMessage = lastMessage,
            StartedAt = DateTime.UtcNow,
            CompletedAt = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static LogAction CreateAction(
        string flowId,
        string serviceName,
        string actionType,
        int stepOrder = 1)
    {
        return new LogAction
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            FlowId = flowId,
            StepOrder = stepOrder,
            ServiceName = serviceName,
            ActionType = actionType,
            Status = StatusTypes.Success,
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
