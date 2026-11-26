using EventRelay.ControlPlaneService.Application.Abstractions.Persistence;
using EventRelay.ControlPlaneService.Application.Transactions;
using Microsoft.Extensions.DependencyInjection;

using static EventRelay.ControlPlaneService.Tests.Unit.Transactions
    .TransactionalProxyTests;

namespace EventRelay.ControlPlaneService.Tests.Unit.Transactions;

public sealed class TransactionalDependencyInjectionTests
{
    private static readonly CancellationToken Ct =
        CancellationToken.None;

    public interface ITestHandler
    {
        Task<int> HandleAsync(CancellationToken ct);
    }

    public sealed class TestHandler(
        ITestService service,
        RecordingUnitOfWork unitOfWork) : ITestHandler
    {
        [Transactional]
        public async Task<int> HandleAsync(
            CancellationToken ct)
        {
            Assert.Equal(1, unitOfWork.Depth);

            return await service.GetResultAsync(ct);
        }
    }

    public interface IPlainService
    {
        int GetValue();
    }

    public sealed class PlainService : IPlainService
    {
        public int GetValue() => 42;
    }

    public interface IUnsupportedService
    {
        int Execute();
    }

    public sealed class UnsupportedService : IUnsupportedService
    {
        [Transactional]
        public int Execute() => 42;
    }

    public sealed class DisposalTracker
    {
        public int Disposals { get; set; }
    }

    public interface IDisposableTestService
    {
        Task ExecuteAsync(CancellationToken ct);
    }

    public sealed class DisposableTestService(
        DisposalTracker tracker)
        : IDisposableTestService, IDisposable
    {
        [Transactional]
        public Task ExecuteAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            tracker.Disposals++;
        }
    }

    private static IServiceCollection CreateServices()
    {
        var services = new ServiceCollection();

        services.AddScoped<RecordingUnitOfWork>();

        services.AddScoped<IUnitOfWork>(
            provider =>
                provider.GetRequiredService<RecordingUnitOfWork>());

        services.AddScoped<ITestService, TestService>();

        services.AddScoped<ITestHandler, TestHandler>();

        services.AddScoped<IPlainService, PlainService>();

        return services;
    }

    private static ServiceProvider BuildProvider(
        IServiceCollection services)
    {
        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
    }

    [Fact]
    public async Task AnnotatedService_IsAutomaticallyProxied()
    {
        var services = CreateServices();

        services.EnableTransactionalInterception();

        using var provider = BuildProvider(services);

        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider
            .GetRequiredService<ITestService>();

        var unitOfWork = scope.ServiceProvider
            .GetRequiredService<RecordingUnitOfWork>();

        Assert.IsAssignableFrom<TransactionalProxy>(service);

        var result = await service.GetResultAsync(Ct);

        Assert.Equal(42, result);

        Assert.Equal(1, unitOfWork.TransactionCalls);
    }

    [Fact]
    public void UnannotatedService_RemainsUnchanged()
    {
        var services = CreateServices();

        services.EnableTransactionalInterception();

        using var provider = BuildProvider(services);

        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider
            .GetRequiredService<IPlainService>();

        Assert.IsType<PlainService>(service);

        Assert.Equal(42, service.GetValue());
    }

    [Fact]
    public void TransactionalService_PreservesScopedLifetime()
    {
        var services = CreateServices();

        services.EnableTransactionalInterception();

        using var provider = BuildProvider(services);

        using var firstScope = provider.CreateScope();

        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider
            .GetRequiredService<ITestService>();

        var second = firstScope.ServiceProvider
            .GetRequiredService<ITestService>();

        var third = secondScope.ServiceProvider
            .GetRequiredService<ITestService>();

        Assert.Same(first, second);

        Assert.NotSame(first, third);
    }

    [Fact]
    public async Task AnnotatedHandlerAndService_AreBothIntercepted()
    {
        var services = CreateServices();

        services.EnableTransactionalInterception();

        using var provider = BuildProvider(services);

        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider
            .GetRequiredService<ITestHandler>();

        var unitOfWork = scope.ServiceProvider
            .GetRequiredService<RecordingUnitOfWork>();

        Assert.IsAssignableFrom<TransactionalProxy>(handler);

        var result = await handler.HandleAsync(Ct);

        Assert.Equal(42, result);

        Assert.Equal(2, unitOfWork.TransactionCalls);

        Assert.Equal(2, unitOfWork.MaxDepth);

        Assert.Equal(0, unitOfWork.Depth);
    }

    [Fact]
    public void TransactionalSynchronousMethod_IsRejected()
    {
        var services = CreateServices();

        services.AddScoped<
            IUnsupportedService,
            UnsupportedService>();

        Assert.Throws<NotSupportedException>(
            () => services.EnableTransactionalInterception());
    }

    [Fact]
    public void TransactionalTransientService_IsRejected()
    {
        var services = CreateServices();

        services.AddTransient<IBaseTestService, TestService>();

        Assert.Throws<InvalidOperationException>(
            () => services.EnableTransactionalInterception());
    }

    [Fact]
    public void DecoratedService_IsDisposedExactlyOnce()
    {
        var services = CreateServices();

        services.AddScoped<DisposalTracker>();

        services.AddScoped<
            IDisposableTestService,
            DisposableTestService>();

        services.EnableTransactionalInterception();

        using var provider = BuildProvider(services);

        DisposalTracker tracker;

        using (var scope = provider.CreateScope())
        {
            tracker = scope.ServiceProvider
                .GetRequiredService<DisposalTracker>();

            var service = scope.ServiceProvider
                .GetRequiredService<IDisposableTestService>();

            Assert.IsAssignableFrom<TransactionalProxy>(service);

            Assert.Equal(0, tracker.Disposals);
        }

        Assert.Equal(1, tracker.Disposals);
    }

    [Fact]
    public async Task EnableInterception_CalledTwice_DoesNotDoubleDecorate()
    {
        var services = CreateServices();

        services.EnableTransactionalInterception();

        services.EnableTransactionalInterception();

        using var provider = BuildProvider(services);

        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider
            .GetRequiredService<ITestService>();

        var unitOfWork = scope.ServiceProvider
            .GetRequiredService<RecordingUnitOfWork>();

        await service.ExecuteAsync(Ct);

        Assert.Equal(1, unitOfWork.TransactionCalls);

        Assert.Equal(1, unitOfWork.MaxDepth);
    }

    [Fact]
    public async Task DifferentScopes_UseIndependentUnitOfWorkInstances()
    {
        var services = CreateServices();

        services.EnableTransactionalInterception();

        using var provider = BuildProvider(services);

        using var firstScope = provider.CreateScope();

        using var secondScope = provider.CreateScope();

        var firstService = firstScope.ServiceProvider
            .GetRequiredService<ITestService>();

        var firstUnitOfWork = firstScope.ServiceProvider
            .GetRequiredService<RecordingUnitOfWork>();

        var secondUnitOfWork = secondScope.ServiceProvider
            .GetRequiredService<RecordingUnitOfWork>();

        await firstService.ExecuteAsync(Ct);

        Assert.NotSame(firstUnitOfWork, secondUnitOfWork);

        Assert.Equal(1, firstUnitOfWork.TransactionCalls);

        Assert.Equal(0, secondUnitOfWork.TransactionCalls);
    }
}
