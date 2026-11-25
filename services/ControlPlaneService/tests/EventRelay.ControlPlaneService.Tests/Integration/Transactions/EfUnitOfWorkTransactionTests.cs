using System.Data;
using EventRelay.ControlPlaneService.Application.Abstractions.Persistence;
using EventRelay.ControlPlaneService.Application.Exceptions;
using EventRelay.ControlPlaneService.Application.Transactions;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;
using EventRelay.ControlPlaneService.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EventRelay.ControlPlaneService.Tests.Integration.Transactions;

public sealed class EfUnitOfWorkTransactionTests(
    CreatePostgresContainer fixture)
    : IClassFixture<CreatePostgresContainer>
{
    private static readonly CancellationToken Ct =
        CancellationToken.None;

    private ControlPlaneDbContext CreateDbContext()
    {
        var options =
            new DbContextOptionsBuilder<ControlPlaneDbContext>()
                .UseNpgsql(fixture.ConnectionString)
                .Options;

        return new ControlPlaneDbContext(options);
    }

    private static EfUnitOfWork CreateUnitOfWork(
        ControlPlaneDbContext db)
    {
        return new EfUnitOfWork(
            db,
            new DbUpdateExceptionPipeline([], []));
    }

    private static UserRecord CreateUser()
    {
        return new UserRecord
        {
            Id = Guid.CreateVersion7(),
            Name = "Transaction test user",
            Issuer = "integration-test",
            Subject = Guid.NewGuid().ToString()
        };
    }

    private async Task<bool> UserExistsAsync(Guid id)
    {
        await using var db = CreateDbContext();

        return await db.Users
            .AsNoTracking()
            .AnyAsync(x => x.Id == id, Ct);
    }

    [Fact]
    public async Task Transaction_Success_CommitsChanges()
    {
        await using var db = CreateDbContext();

        var unitOfWork = CreateUnitOfWork(db);

        var user = CreateUser();

        var result = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                db.Users.Add(user);

                await unitOfWork.SaveChangesAsync(ct);

                Assert.False(await UserExistsAsync(user.Id));

                return user.Id;
            },
            IsolationLevel.Serializable,
            Ct);

        Assert.Equal(user.Id, result);

        Assert.True(await UserExistsAsync(user.Id));

        Assert.Null(db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task Transaction_Failure_RollsBackChanges()
    {
        await using var db = CreateDbContext();

        var unitOfWork = CreateUnitOfWork(db);

        var user = CreateUser();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => unitOfWork.ExecuteInTransactionAsync<bool>(
                async ct =>
                {
                    db.Users.Add(user);

                    await unitOfWork.SaveChangesAsync(ct);

                    throw new InvalidOperationException(
                        "Operation failed.");
                },
                IsolationLevel.Serializable,
                Ct));

        Assert.False(await UserExistsAsync(user.Id));

        Assert.Null(db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task NestedTransaction_Success_CommitsAllChanges()
    {
        await using var db = CreateDbContext();

        var unitOfWork = CreateUnitOfWork(db);

        var firstUser = CreateUser();

        var secondUser = CreateUser();

        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                db.Users.Add(firstUser);

                await unitOfWork.SaveChangesAsync(ct);

                var outerTransactionId =
                    db.Database.CurrentTransaction!.TransactionId;

                await unitOfWork.ExecuteInTransactionAsync(
                    async innerCt =>
                    {
                        Assert.Equal(
                            outerTransactionId,
                            db.Database.CurrentTransaction!.TransactionId);

                        db.Users.Add(secondUser);

                        await unitOfWork.SaveChangesAsync(innerCt);

                        return true;
                    },
                    IsolationLevel.Serializable,
                    ct);

                Assert.Equal(
                    outerTransactionId,
                    db.Database.CurrentTransaction!.TransactionId);

                Assert.False(
                    await UserExistsAsync(firstUser.Id));

                Assert.False(
                    await UserExistsAsync(secondUser.Id));

                return true;
            },
            IsolationLevel.Serializable,
            Ct);

        Assert.True(await UserExistsAsync(firstUser.Id));

        Assert.True(await UserExistsAsync(secondUser.Id));

        Assert.Null(db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task NestedTransaction_Failure_RollsBackAllChanges()
    {
        await using var db = CreateDbContext();

        var unitOfWork = CreateUnitOfWork(db);

        var firstUser = CreateUser();

        var secondUser = CreateUser();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    db.Users.Add(firstUser);

                    await unitOfWork.SaveChangesAsync(ct);

                    await unitOfWork.ExecuteInTransactionAsync<bool>(
                        async innerCt =>
                        {
                            db.Users.Add(secondUser);

                            await unitOfWork.SaveChangesAsync(innerCt);

                            throw new InvalidOperationException(
                                "Inner operation failed.");
                        },
                        IsolationLevel.Serializable,
                        ct);

                    return true;
                },
                IsolationLevel.Serializable,
                Ct));

        Assert.False(await UserExistsAsync(firstUser.Id));

        Assert.False(await UserExistsAsync(secondUser.Id));

        Assert.Null(db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task NestedTransaction_CaughtFailure_PreventsCommit()
    {
        await using var db = CreateDbContext();

        var unitOfWork = CreateUnitOfWork(db);

        var user = CreateUser();

        var expectedFailure = new InvalidOperationException(
            "Inner operation failed.");

        var exception =
            await Assert.ThrowsAsync<TransactionRollbackOnlyException>(
                () => unitOfWork.ExecuteInTransactionAsync(
                    async ct =>
                    {
                        db.Users.Add(user);

                        await unitOfWork.SaveChangesAsync(ct);

                        try
                        {
                            await unitOfWork
                                .ExecuteInTransactionAsync<bool>(
                                    _ => throw expectedFailure,
                                    IsolationLevel.Serializable,
                                    ct);
                        }
                        catch (InvalidOperationException)
                        {
                            // Simulate an application swallowing
                            // the original operation failure.
                        }

                        return true;
                    },
                    IsolationLevel.Serializable,
                    Ct));

        Assert.Same(
            expectedFailure,
            exception.InnerException);

        Assert.False(await UserExistsAsync(user.Id));

        Assert.Null(db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task NestedTransaction_IncompatibleIsolation_IsRejected()
    {
        await using var db = CreateDbContext();

        var unitOfWork = CreateUnitOfWork(db);

        var user = CreateUser();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    db.Users.Add(user);

                    await unitOfWork.SaveChangesAsync(ct);

                    await unitOfWork.ExecuteInTransactionAsync(
                        _ => Task.FromResult(true),
                        IsolationLevel.Serializable,
                        ct);

                    return true;
                },
                IsolationLevel.ReadCommitted,
                Ct));

        Assert.False(await UserExistsAsync(user.Id));

        Assert.Null(db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task NestedTransaction_CaughtIsolationMismatch_PreventsCommit()
    {
        await using var db = CreateDbContext();

        var unitOfWork = CreateUnitOfWork(db);

        var user = CreateUser();

        await Assert.ThrowsAsync<TransactionRollbackOnlyException>(
            () => unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    db.Users.Add(user);

                    await unitOfWork.SaveChangesAsync(ct);

                    try
                    {
                        await unitOfWork.ExecuteInTransactionAsync(
                            _ => Task.FromResult(true),
                            IsolationLevel.Serializable,
                            ct);
                    }
                    catch (InvalidOperationException)
                    {
                        // The isolation mismatch must still
                        // invalidate the surrounding transaction.
                    }

                    return true;
                },
                IsolationLevel.ReadCommitted,
                Ct));

        Assert.False(await UserExistsAsync(user.Id));

        Assert.Null(db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task Transaction_Cancellation_RollsBackChanges()
    {
        await using var db = CreateDbContext();

        var unitOfWork = CreateUnitOfWork(db);

        var user = CreateUser();

        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    db.Users.Add(user);

                    await unitOfWork.SaveChangesAsync(ct);

                    cancellation.Cancel();

                    return true;
                },
                IsolationLevel.Serializable,
                cancellation.Token));

        Assert.False(await UserExistsAsync(user.Id));

        Assert.Null(db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task NestedTransaction_CaughtCancellation_PreventsCommit()
    {
        await using var db = CreateDbContext();

        var unitOfWork = CreateUnitOfWork(db);

        var user = CreateUser();

        using var innerCancellation =
            new CancellationTokenSource();

        await Assert.ThrowsAsync<TransactionRollbackOnlyException>(
            () => unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    db.Users.Add(user);

                    await unitOfWork.SaveChangesAsync(ct);

                    innerCancellation.Cancel();

                    try
                    {
                        await unitOfWork.ExecuteInTransactionAsync(
                            _ => Task.FromResult(true),
                            IsolationLevel.Serializable,
                            innerCancellation.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        // Even if cancellation is swallowed,
                        // the transaction must not commit.
                    }

                    return true;
                },
                IsolationLevel.Serializable,
                Ct));

        Assert.False(await UserExistsAsync(user.Id));

        Assert.Null(db.Database.CurrentTransaction);
    }

    [Theory]
    [InlineData(
        IsolationLevel.Serializable,
        "serializable")]
    [InlineData(
        IsolationLevel.ReadCommitted,
        "read committed")]
    public async Task Transaction_UsesRequestedIsolationLevel(
        IsolationLevel isolationLevel,
        string expected)
    {
        await using var db = CreateDbContext();

        var unitOfWork = CreateUnitOfWork(db);

        var actual = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var result = await db.Database
                    .SqlQueryRaw<string>(
                        """
                        SELECT current_setting(
                            'transaction_isolation'
                        ) AS "Value"
                        """)
                    .ToListAsync(ct);

                return result.Single();
            },
            isolationLevel,
            Ct);

        Assert.Equal(expected, actual);

        Assert.Null(db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task SerializableTransaction_ConcurrentUpdate_ThrowsConflict()
    {
        var user = CreateUser();

        await using (var seedDb = CreateDbContext())
        {
            seedDb.Users.Add(user);

            await seedDb.SaveChangesAsync(Ct);
        }

        await using var firstDb = CreateDbContext();

        var firstUnitOfWork = CreateUnitOfWork(firstDb);

        var exception =
            await Assert.ThrowsAsync<TransactionConflictException>(
                () => firstUnitOfWork.ExecuteInTransactionAsync(
                    async ct =>
                    {
                        var firstUser = await firstDb.Users
                            .SingleAsync(
                                x => x.Id == user.Id,
                                ct);

                        // The first transaction now owns
                        // its Serializable snapshot.

                        await using (var secondDb =
                                     CreateDbContext())
                        {
                            var secondUnitOfWork =
                                CreateUnitOfWork(secondDb);

                            await secondUnitOfWork
                                .ExecuteInTransactionAsync(
                                    async innerCt =>
                                    {
                                        var secondUser =
                                            await secondDb.Users
                                                .SingleAsync(
                                                    x => x.Id == user.Id,
                                                    innerCt);

                                        secondUser.Name =
                                            "Second transaction";

                                        await secondUnitOfWork
                                            .SaveChangesAsync(innerCt);

                                        return true;
                                    },
                                    IsolationLevel.Serializable,
                                    ct);
                        }

                        // The second transaction has committed.
                        // This transaction still sees the old version.

                        firstUser.Name = "First transaction";

                        await firstUnitOfWork.SaveChangesAsync(ct);

                        return true;
                    },
                    IsolationLevel.Serializable,
                    Ct));

        var causes = new List<Exception>();

        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            causes.Add(current);
        }

        Assert.Contains(
            causes,
            x => x is PostgresException
            {
                SqlState: PostgresErrorCodes.SerializationFailure
            });

        Assert.Null(firstDb.Database.CurrentTransaction);

        await using var verificationDb = CreateDbContext();

        var stored = await verificationDb.Users
            .AsNoTracking()
            .SingleAsync(x => x.Id == user.Id, Ct);

        Assert.Equal(
            "Second transaction",
            stored.Name);
    }

    [Fact]
    public async Task SaveChangesFailure_WhenCaught_PreventsCommit()
    {
        await using var db = CreateDbContext();

        var unitOfWork = CreateUnitOfWork(db);

        var firstUser = CreateUser();

        var duplicate = CreateUser();

        duplicate.Issuer = firstUser.Issuer;
        duplicate.Subject = firstUser.Subject;

        await Assert.ThrowsAsync<TransactionRollbackOnlyException>(
            () => unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    db.Users.Add(firstUser);

                    await unitOfWork.SaveChangesAsync(ct);

                    db.Users.Add(duplicate);

                    try
                    {
                        await unitOfWork.SaveChangesAsync(ct);
                    }
                    catch (DbUpdateException)
                    {
                        // Deliberately swallow a unique
                        // constraint violation.
                    }

                    return true;
                },
                IsolationLevel.Serializable,
                Ct));

        Assert.Null(db.Database.CurrentTransaction);

        Assert.False(await UserExistsAsync(firstUser.Id));

        Assert.False(await UserExistsAsync(duplicate.Id));
    }

    [Fact]
    public async Task ExternalTransaction_IsNotSilentlyJoined()
    {
        await using var db = CreateDbContext();

        var unitOfWork = CreateUnitOfWork(db);

        await using var externalTransaction =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                Ct);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => unitOfWork.ExecuteInTransactionAsync(
                _ => Task.FromResult(true),
                IsolationLevel.Serializable,
                Ct));

        Assert.Equal(
            externalTransaction.TransactionId,
            db.Database.CurrentTransaction!.TransactionId);

        await externalTransaction.RollbackAsync(Ct);
    }

    public interface ITransactionalWriteService
    {
        Task<Guid> WriteAsync(
            UserRecord user,
            CancellationToken ct);
    }

    public sealed class TransactionalWriteService(
        ControlPlaneDbContext db,
        IUnitOfWork unitOfWork)
        : ITransactionalWriteService
    {
        [Transactional]
        public async Task<Guid> WriteAsync(
            UserRecord user,
            CancellationToken ct)
        {
            var transactionId =
                db.Database.CurrentTransaction!.TransactionId;

            db.Users.Add(user);

            await unitOfWork.SaveChangesAsync(ct);

            return transactionId;
        }
    }

    public interface ITransactionalWriteHandler
    {
        Task<Guid> HandleAsync(
            UserRecord firstUser,
            UserRecord secondUser,
            bool shouldFail,
            CancellationToken ct);
    }

    public sealed class TransactionalWriteHandler(
        ControlPlaneDbContext db,
        IUnitOfWork unitOfWork,
        ITransactionalWriteService writeService)
        : ITransactionalWriteHandler
    {
        [Transactional]
        public async Task<Guid> HandleAsync(
            UserRecord firstUser,
            UserRecord secondUser,
            bool shouldFail,
            CancellationToken ct)
        {
            var outerTransactionId =
                db.Database.CurrentTransaction!.TransactionId;

            db.Users.Add(firstUser);

            await unitOfWork.SaveChangesAsync(ct);

            var innerTransactionId =
                await writeService.WriteAsync(
                    secondUser,
                    ct);

            Assert.Equal(
                outerTransactionId,
                innerTransactionId);

            Assert.Equal(
                outerTransactionId,
                db.Database.CurrentTransaction!.TransactionId);

            if (shouldFail)
            {
                throw new InvalidOperationException(
                    "Handler failed after the service completed.");
            }

            return outerTransactionId;
        }
    }

    private ServiceProvider CreateTransactionalProvider()
    {
        var services = new ServiceCollection();

        services.AddDbContext<ControlPlaneDbContext>(
            options =>
                options.UseNpgsql(fixture.ConnectionString));

        services.AddScoped<IUnitOfWork>(
            provider => CreateUnitOfWork(
                provider.GetRequiredService<
                    ControlPlaneDbContext>()));

        services.AddScoped<
            ITransactionalWriteService,
            TransactionalWriteService>();

        services.AddScoped<
            ITransactionalWriteHandler,
            TransactionalWriteHandler>();

        services.EnableTransactionalInterception();

        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandlerAndService_PropagateRealTransaction(
        bool shouldFail)
    {
        var firstUser = CreateUser();

        var secondUser = CreateUser();

        await using var provider =
            CreateTransactionalProvider();

        await using var scope =
            provider.CreateAsyncScope();

        var handler = scope.ServiceProvider
            .GetRequiredService<ITransactionalWriteHandler>();

        Assert.IsAssignableFrom<TransactionalProxy>(handler);

        if (shouldFail)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => handler.HandleAsync(
                    firstUser,
                    secondUser,
                    true,
                    Ct));
        }
        else
        {
            var transactionId = await handler.HandleAsync(
                firstUser,
                secondUser,
                false,
                Ct);

            Assert.NotEqual(
                Guid.Empty,
                transactionId);
        }

        Assert.Equal(
            !shouldFail,
            await UserExistsAsync(firstUser.Id));

        Assert.Equal(
            !shouldFail,
            await UserExistsAsync(secondUser.Id));

        var db = scope.ServiceProvider
            .GetRequiredService<ControlPlaneDbContext>();

        Assert.Null(db.Database.CurrentTransaction);
    }
}
