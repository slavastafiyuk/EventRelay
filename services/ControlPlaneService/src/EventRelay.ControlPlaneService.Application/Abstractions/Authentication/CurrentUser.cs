using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Application.Abstractions.Authentication;

public sealed class CurrentUser : ICurrentUser, ICurrentUserInitializer
{
    private UserKey? _userKey;

    public UserKey UserKey => _userKey
        ?? throw new InvalidOperationException("Current user has not been initialized.");

    public void Initialize(UserKey userKey)
    {
        ArgumentNullException.ThrowIfNull(userKey);

        if (_userKey is not null)
            throw new InvalidOperationException("Current user is already initialized.");

        _userKey = userKey;
    }
}
