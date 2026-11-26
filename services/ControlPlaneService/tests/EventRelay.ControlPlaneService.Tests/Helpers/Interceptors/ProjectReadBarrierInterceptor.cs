using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EventRelay.ControlPlaneService.Tests.Helpers.Interceptors;

public sealed class ProjectReadBarrierInterceptor(
    Guid projectId) : DbCommandInterceptor
{
    private readonly TaskCompletionSource<IsolationLevel?> _entered =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource _released =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _claimed;

    public Task<IsolationLevel?> Entered => _entered.Task;

    public void Release()
    {
        _released.TrySetResult();
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        if (!IsTargetProjectQuery(command))
        {
            return result;
        }

        if (Interlocked.CompareExchange(
                ref _claimed,
                1,
                0) != 0)
        {
            return result;
        }

        _entered.TrySetResult(
            command.Transaction?.IsolationLevel);

        try
        {
            await _released.Task.WaitAsync(
                TimeSpan.FromSeconds(45),
                cancellationToken);

            return result;
        }
        catch
        {
            await result.DisposeAsync();

            throw;
        }
    }

    private bool IsTargetProjectQuery(DbCommand command)
    {
        return command.CommandText
                   .TrimStart()
                   .StartsWith(
                       "SELECT",
                       StringComparison.OrdinalIgnoreCase)

               && command.CommandText.Contains(
                   "project_access",
                   StringComparison.OrdinalIgnoreCase)

               && command.Parameters
                   .Cast<DbParameter>()
                   .Any(parameter =>
                       parameter.Value is Guid id &&
                       id == projectId);
    }
}
