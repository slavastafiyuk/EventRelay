using EventRelay.ControlPlaneService.Application.Abstractions.Persistence;
using EventRelay.ControlPlaneService.Application.Abstractions.Users;
using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Application.Services;

public sealed class UserService(
    IUserRepository repository,
    IUnitOfWork unitOfWork) : IUserService
{
    public async Task<UserKey> GetOrCreateAsync(User user, CancellationToken cancellationToken)
    {
        var userKey = await repository.GetOrCreateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return userKey;
    }
}
