using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Skysim.Logger.Api.Common;

/// <summary>
/// A no-op execution strategy for Npgsql / EF Core.
/// The Kafka consumer already uses Polly (ResiliencePipeline) to retry the
/// entire persist operation as an atomic unit.  Having two retry layers
/// simultaneously causes connection-state corruption between the EF Core
/// retry attempt and the Polly retry — specifically when PostgreSQL is
/// temporarily unavailable, the EF Core strategy retries a single query,
/// the connection becomes corrupted, and all subsequent queries in that
/// DbContext instance fail with ObjectDisposedException.
/// </summary>
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
