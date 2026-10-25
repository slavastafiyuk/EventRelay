using EventRelay.ControlPlaneService.Application.Abstractions.Users;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.User;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Repository.Repositories;

public class UserRepository(ControlPlaneDbContext dbContext) : IUserRepository
{

    public async Task<UserKey> GetOrCreateAsync(
        User user,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.Issuer == user.Issuer && x.Subject == user.Subject)
            .Select(x => new UserKey(x.Id, x.Name))
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is not null) return existing;

        await dbContext.Users.AddAsync(user.Adapt<UserRecord>(), cancellationToken);

        return user.Key;
    }

}
