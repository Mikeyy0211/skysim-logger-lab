using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Skysim.Logger.Infrastructure.Data;

public class NoRetryExecutionStrategy : IExecutionStrategy
{
    public NoRetryExecutionStrategy()
    {
    }

    bool IExecutionStrategy.RetriesOnFailure => false;

    public void Execute(Action operation)
    {
        operation();
    }

    public TResult Execute<TResult>(Func<TResult> operation)
    {
        return operation();
    }

    public TResult Execute<TState, TResult>(
        TState state,
        Func<DbContext, TState, TResult> operation,
        Func<DbContext, TState, ExecutionResult<TResult>>? verifySucceeded)
    {
        return operation(null!, state);
    }

    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await operation(cancellationToken);
    }

    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        return await operation(cancellationToken);
    }

    public async Task<TResult> ExecuteAsync<TState, TResult>(
        TState state,
        Func<DbContext, TState, CancellationToken, Task<TResult>> operation,
        Func<DbContext, TState, CancellationToken, Task<ExecutionResult<TResult>>>? verifySucceeded,
        CancellationToken cancellationToken = default)
    {
        return await operation(null!, state, cancellationToken);
    }
}
