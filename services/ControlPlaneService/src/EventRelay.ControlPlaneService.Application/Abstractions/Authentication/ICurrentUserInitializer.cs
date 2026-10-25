using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Application.Abstractions.Authentication;

public interface ICurrentUserInitializer
{
    void Initialize(UserKey userKey);
}
