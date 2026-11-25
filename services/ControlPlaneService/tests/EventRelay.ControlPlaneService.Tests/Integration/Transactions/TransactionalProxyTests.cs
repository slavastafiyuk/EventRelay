using System.Data;
using EventRelay.ControlPlaneService.Application.Abstractions.Persistence;
using EventRelay.ControlPlaneService.Application.Transactions;

namespace EventRelay.ControlPlaneService.Tests.Unit.Transactions;

public sealed class TransactionalProxyTests
{
    private static readonly CancellationToken Ct =
        CancellationToken.None;

    public sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int TransactionCalls { get; private set; }

        public int Depth { get; private set; }

        public int MaxDepth { get; private set; }

        public IsolationLevel? LastIsolationLevel { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken)
        {
            return Task.FromResult(1);
        }

        public async Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            IsolationLevel isolationLevel,
            CancellationToken cancellationToken)
        {
            TransactionCalls++;

            LastIsolationLevel = isolationLevel;
            LastCancellationToken = cancellationToken;

            Depth++;
            MaxDepth = Math.Max(MaxDepth, Depth);

            try
            {
                return await operation(cancellationToken);
            }
            finally
            {
                Depth--;
            }
        }
    }

    public interface IBaseTestService
    {
        Task<int> InheritedAsync(CancellationToken ct);
    }

    public interface ITestService : IBaseTestService
    {
        Task ExecuteAsync(CancellationToken ct);

        Task<int> GetResultAsync(CancellationToken ct);

        Task<int> NonTransactionalAsync(CancellationToken ct);

        Task<int> ReadCommittedAsync(CancellationToken ct);

        Task ThrowAsync(
            Exception exception,
            CancellationToken ct);

        Task ThrowAfterAwaitAsync(
            Exception exception,
            CancellationToken ct);

        Task<int> WaitAsync(
            Task completion,
            CancellationToken ct);

        [Transactional]
        Task<int> InterfaceAnnotatedAsync(CancellationToken ct);
    }

    public sealed class TestService(
        RecordingUnitOfWork unitOfWork) : ITestService
    {
        [Transactional]
        public Task ExecuteAsync(CancellationToken ct)
        {
            Assert.True(unitOfWork.Depth > 0);

            return Task.CompletedTask;
        }

        [Transactional]
        public Task<int> GetResultAsync(CancellationToken ct)
        {
            Assert.True(unitOfWork.Depth > 0);

            return Task.FromResult(42);
        }

        public Task<int> NonTransactionalAsync(
            CancellationToken ct)
        {
            return Task.FromResult(unitOfWork.Depth);
        }

        [Transactional(IsolationLevel.ReadCommitted)]
        public Task<int> ReadCommittedAsync(
            CancellationToken ct)
        {
            return Task.FromResult(42);
        }

        [Transactional]
        public Task ThrowAsync(
            Exception exception,
            CancellationToken ct)
        {
            throw exception;
        }

        [Transactional]
        public async Task ThrowAfterAwaitAsync(
            Exception exception,
            CancellationToken ct)
        {
            await Task.Yield();

            throw exception;
        }

        [Transactional]
        public Task<int> InheritedAsync(
            CancellationToken ct)
        {
            return Task.FromResult(42);
        }

        public Task<int> InterfaceAnnotatedAsync(
            CancellationToken ct)
        {
            return Task.FromResult(42);
        }

        [Transactional]
        public async Task<int> WaitAsync(
            Task completion,
            CancellationToken ct)
        {
            await completion.WaitAsync(ct);

            return 42;
        }
    }

    private static (
        ITestService Service,
        RecordingUnitOfWork UnitOfWork) CreateProxy()
    {
        var unitOfWork = new RecordingUnitOfWork();

        var target = new TestService(unitOfWork);

        var proxy = (ITestService)TransactionalProxy.Wrap(
            typeof(ITestService),
            target,
            unitOfWork);

        return (proxy, unitOfWork);
    }

    [Fact]
    public async Task AnnotatedTask_ExecutesTransaction()
    {
        var (service, unitOfWork) = CreateProxy();

        await service.ExecuteAsync(Ct);

        Assert.Equal(1, unitOfWork.TransactionCalls);
        Assert.Equal(0, unitOfWork.Depth);

        Assert.Equal(
            IsolationLevel.Serializable,
            unitOfWork.LastIsolationLevel);
    }

    [Fact]
    public async Task AnnotatedTaskWithResult_PreservesResult()
    {
        var (service, unitOfWork) = CreateProxy();

        var result = await service.GetResultAsync(Ct);

        Assert.Equal(42, result);
        Assert.Equal(1, unitOfWork.TransactionCalls);
        Assert.Equal(0, unitOfWork.Depth);
    }

    [Fact]
    public async Task UnannotatedMethod_DoesNotStartTransaction()
    {
        var (service, unitOfWork) = CreateProxy();

        var result = await service.NonTransactionalAsync(Ct);

        Assert.Equal(0, result);
        Assert.Equal(0, unitOfWork.TransactionCalls);
    }

    [Fact]
    public async Task CustomIsolationLevel_IsForwarded()
    {
        var (service, unitOfWork) = CreateProxy();

        await service.ReadCommittedAsync(Ct);

        Assert.Equal(
            IsolationLevel.ReadCommitted,
            unitOfWork.LastIsolationLevel);
    }

    [Fact]
    public async Task CancellationToken_IsForwarded()
    {
        var (service, unitOfWork) = CreateProxy();

        using var cancellation = new CancellationTokenSource();

        await service.ExecuteAsync(cancellation.Token);

        Assert.Equal(
            cancellation.Token,
            unitOfWork.LastCancellationToken);
    }

    [Fact]
    public async Task SynchronousException_IsPreserved()
    {
        var (service, unitOfWork) = CreateProxy();

        var expected = new InvalidOperationException(
            "Business operation failed.");

        var actual =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ThrowAsync(expected, Ct));

        Assert.Same(expected, actual);

        Assert.Equal(1, unitOfWork.TransactionCalls);
        Assert.Equal(0, unitOfWork.Depth);
    }

    [Fact]
    public async Task AsynchronousException_IsPreserved()
    {
        var (service, unitOfWork) = CreateProxy();

        var expected = new InvalidOperationException(
            "Asynchronous operation failed.");

        var actual =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ThrowAfterAwaitAsync(expected, Ct));

        Assert.Same(expected, actual);

        Assert.Equal(1, unitOfWork.TransactionCalls);
        Assert.Equal(0, unitOfWork.Depth);
    }

    [Fact]
    public async Task InheritedInterfaceMethod_IsIntercepted()
    {
        var (service, unitOfWork) = CreateProxy();

        var result = await service.InheritedAsync(Ct);

        Assert.Equal(42, result);
        Assert.Equal(1, unitOfWork.TransactionCalls);
    }

    [Fact]
    public async Task InterfaceAttribute_IsDetected()
    {
        var (service, unitOfWork) = CreateProxy();

        var result = await service.InterfaceAnnotatedAsync(Ct);

        Assert.Equal(42, result);
        Assert.Equal(1, unitOfWork.TransactionCalls);
    }

    [Fact]
    public async Task AnnotatedAsyncMethod_AwaitsCompletion()
    {
        var (service, unitOfWork) = CreateProxy();

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var operation = service.WaitAsync(
            completion.Task,
            Ct);

        try
        {
            Assert.False(operation.IsCompleted);

            Assert.Equal(1, unitOfWork.Depth);

            Assert.Equal(
                1,
                unitOfWork.TransactionCalls);
        }
        finally
        {
            completion.TrySetResult();
        }

        var result = await operation.WaitAsync(
            TimeSpan.FromSeconds(10));

        Assert.Equal(42, result);
        Assert.Equal(0, unitOfWork.Depth);
    }

    [Fact]
    public async Task CancelledAsyncMethod_PreservesCancellation()
    {
        var (service, unitOfWork) = CreateProxy();

        var completion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        using var cancellation = new CancellationTokenSource();

        var operation = service.WaitAsync(
            completion.Task,
            cancellation.Token);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => operation.WaitAsync(TimeSpan.FromSeconds(10)));

        Assert.Equal(1, unitOfWork.TransactionCalls);
        Assert.Equal(0, unitOfWork.Depth);
    }
}
