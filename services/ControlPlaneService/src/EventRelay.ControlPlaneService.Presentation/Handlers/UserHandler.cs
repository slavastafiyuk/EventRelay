using EventRelay.ControlPlaneService.Application.Abstractions.Users;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Contracts.User;
using Mapster;

namespace EventRelay.ControlPlaneService.Presentation.Handlers;

public class UserHandler(IUserService userService) : IUserHandler
{
    public async Task<UserKey> EnsureExistsAsync(
        UserCreateDTO dto,
        CancellationToken cancellationToken)
    {
        var persistedUser = await userService.GetOrCreateAsync(
            dto.Adapt<User>(),
            cancellationToken);

        return persistedUser;
    }

}
