using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Application.Abstractions.Users;

public interface IUserRepository
{
    Task<UserKey> GetOrCreateAsync(User user, CancellationToken cancellationToken);
    Task<UserKey?> GetByIdAsync(Guid userId, CancellationToken cancellationToken);
}
