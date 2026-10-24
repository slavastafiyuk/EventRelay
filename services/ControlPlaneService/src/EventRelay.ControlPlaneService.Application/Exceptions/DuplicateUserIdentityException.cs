
namespace EventRelay.ControlPlaneService.Application.Exceptions;

public sealed class DuplicateUserIdentityException(
    Exception innerException)
    : Exception("User identity already exists.", innerException);
