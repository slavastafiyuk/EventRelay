using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Application.Abstractions.Authentication;

public interface ICurrentUser
{
    UserKey? UserKey { get; }
}
