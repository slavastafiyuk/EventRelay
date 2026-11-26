using System.Data;
using EventRelay.ControlPlaneService.Application.Abstractions.Persistence;
using EventRelay.ControlPlaneService.Application.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventRelay.ControlPlaneService.Infrastructure.Persistence;

public sealed class EfUnitOfWork(
    ControlPlaneDbContext dbContext,
    DbUpdateExceptionPipeline exceptionPipeline)
    : IUnitOfWork
{
    private TransactionState? _activeTransaction;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (_activeTransaction is { } state) ThrowIfRollbackOnly(state);

        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            MarkRollbackOnly(exception);

            var translated = exceptionPipeline.Process(exception);

            if (translated is null)
                throw;

            throw translated;
        }
        catch (Exception exception)
        {
            MarkRollbackOnly(exception);
            throw;
        }
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (_activeTransaction is { } existingTransaction)
        {
            return await JoinTransactionAsync(existingTransaction, operation, isolationLevel, cancellationToken);
        }

        return await ExecuteNewTransactionAsync(operation, isolationLevel, cancellationToken);
    }

    private async Task<TResult> ExecuteNewTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("An externally managed transaction is already active.");
        }

        try
        {
            await using var transaction =
                await dbContext.Database.BeginTransactionAsync(isolationLevel, cancellationToken);

            var state = new TransactionState(isolationLevel);

            _activeTransaction = state;

            var result = await operation(cancellationToken);

            ThrowIfRollbackOnly(state);

            cancellationToken.ThrowIfCancellationRequested();

            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch (Exception exception) when (IsTransactionConflict(exception))
        {
            throw new TransactionConflictException(
                "The transaction conflicted with another operation.",
                exception);
        }
        finally
        {
            _activeTransaction = null;
        }
    }

    private static async Task<TResult> JoinTransactionAsync<TResult>(TransactionState transaction, Func<CancellationToken, Task<TResult>> operation, IsolationLevel isolationLevel, CancellationToken cancellationToken)
    {
        try
        {
            ThrowIfRollbackOnly(transaction);

            if (transaction.IsolationLevel != isolationLevel)
            {
                throw new InvalidOperationException(
                    $"Cannot join a transaction with isolation level " +
                    $"{transaction.IsolationLevel} when " +
                    $"{isolationLevel} is required.");
            }

            cancellationToken.ThrowIfCancellationRequested();

            var result = await operation(cancellationToken);

            ThrowIfRollbackOnly(transaction);

            cancellationToken.ThrowIfCancellationRequested();

            return result;
        }
        catch (Exception exception)
        {
            transaction.MarkRollbackOnly(exception);

            throw;
        }
    }

    private void MarkRollbackOnly(Exception exception)
    {
        _activeTransaction?.MarkRollbackOnly(exception);
    }

    private static void ThrowIfRollbackOnly(TransactionState transaction)
    {
        if (transaction.Failure is { } failure)
        {
            throw new TransactionRollbackOnlyException("The transaction has been marked for rollback.", failure);
        }
    }

    private static bool IsTransactionConflict(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException &&
                postgresException.SqlState is PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected)
            {
                return true;
            }
        }

        return false;
    }

    private sealed class TransactionState(IsolationLevel isolationLevel)
    {
        public IsolationLevel IsolationLevel { get; } = isolationLevel;
        public Exception? Failure { get; private set; }

        public void MarkRollbackOnly(Exception exception)
        {
            Failure ??= exception;
        }
    }
}
