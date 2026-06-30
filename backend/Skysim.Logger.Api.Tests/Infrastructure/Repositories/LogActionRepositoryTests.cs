using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Skysim.Logger.Api.Infrastructure.Persistence.Exceptions;
using Skysim.Logger.Infrastructure.Data;
using Skysim.Logger.Infrastructure.Entities;
using Skysim.Logger.Infrastructure.Repositories;
using Xunit;
using StatusTypes = Skysim.Logger.Contracts.Constants.StatusTypes;
using ActionTypes = Skysim.Logger.Contracts.Constants.ActionTypes;

namespace Skysim.Logger.Api.Tests.Infrastructure.Repositories;

public class LogActionRepositoryTests : IDisposable
{
    private readonly LoggerDbContext _db;
    private readonly LogActionRepository _repo;

    public LogActionRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<LoggerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new LoggerDbContext(options);
        _repo = new LogActionRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public async Task InsertAsync_WithValidAction_ShouldPersistAndReturnAction()
    {
        var beforeInsert = DateTime.UtcNow;
        var action = CreateValidLogAction(
            Guid.NewGuid(),
            "test-flow-001",
            ActionTypes.OrderCreated,
            StatusTypes.Success);

        var result = await _repo.InsertAsync(action);

        result.Should().NotBeNull();
        result.StepOrder.Should().Be(1);
        result.CreatedAt.Should().BeOnOrAfter(beforeInsert);
        result.CreatedAt.Should().BeCloseTo(beforeInsert, TimeSpan.FromSeconds(2));
        result.UpdatedAt.Should().BeCloseTo(beforeInsert, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task InsertAsync_ShouldAssignStepOrderIncrementally()
    {
        var flowId = "test-flow-step-order";
        var action1 = CreateValidLogAction(Guid.NewGuid(), flowId, ActionTypes.OrderCreated, StatusTypes.Success);
        var action2 = CreateValidLogAction(Guid.NewGuid(), flowId, ActionTypes.PaymentRequested, StatusTypes.InProgress);
        var action3 = CreateValidLogAction(Guid.NewGuid(), flowId, ActionTypes.PaymentSuccess, StatusTypes.Success);

        var result1 = await _repo.InsertAsync(action1);
        var result2 = await _repo.InsertAsync(action2);
        var result3 = await _repo.InsertAsync(action3);

        result1.StepOrder.Should().Be(1);
        result2.StepOrder.Should().Be(2);
        result3.StepOrder.Should().Be(3);
    }

    [Fact]
    public async Task InsertAsync_ShouldStoreAllFields()
    {
        var now = DateTime.UtcNow;
        var action = new LogAction
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            FlowId = "test-flow-fields",
            ServiceName = "Order",
            ActionType = "ORDER_CREATED",
            Status = "SUCCESS",
            Message = "Order created",
            ErrorCode = null,
            ErrorMessage = null,
            RequestTime = now.AddSeconds(-1),
            ResponseTime = now,
            DurationMs = 1000,
            CorrelationId = "corr-123"
        };

        var result = await _repo.InsertAsync(action);

        var saved = await _db.LogActions.FirstAsync(a => a.Id == result.Id);
        saved.ServiceName.Should().Be("Order");
        saved.ActionType.Should().Be("ORDER_CREATED");
        saved.Status.Should().Be("SUCCESS");
        saved.Message.Should().Be("Order created");
        saved.DurationMs.Should().Be(1000);
        saved.CorrelationId.Should().Be("corr-123");
    }

    private static LogAction CreateValidLogAction(Guid id, string flowId, string actionType, string status)
    {
        return new LogAction
        {
            Id = id,
            EventId = Guid.NewGuid(),
            FlowId = flowId,
            ServiceName = "Order",
            ActionType = actionType,
            Status = status
        };
    }
}
