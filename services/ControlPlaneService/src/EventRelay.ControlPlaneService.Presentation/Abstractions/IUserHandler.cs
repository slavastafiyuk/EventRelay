using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Presentation.Contracts.User;

namespace EventRelay.ControlPlaneService.Presentation.Abstractions;

public interface IUserHandler
{
    Task<UserKey> EnsureExistsAsync(UserCreateDTO dto, CancellationToken cancellationToken);
}
