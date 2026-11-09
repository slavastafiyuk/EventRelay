
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.User;

namespace EventRelay.ControlPlaneService.Domain.Entities.Api;

public sealed class ProjectApi
{
    public ApiKey Key { get; private set; }
    public ProjectKey Project { get; private set; }
    public UserKey CreatedBy { get; private set; }
    public UserKey? RevokedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public bool IsActive => RevokedAt is null;

    private ProjectApi(
        ApiKey key,
        ProjectKey project,
        UserKey createdBy,
        DateTimeOffset createdAt)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        Project = project ?? throw new ArgumentNullException(nameof(project));
        CreatedBy = createdBy ?? throw new ArgumentNullException(nameof(createdBy));
        CreatedAt = createdAt;
    }

    public static (ProjectApi Api, string Secret) Issue(ProjectKey project, UserKey createdBy, string name)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(createdBy);

        var (key, secret) = ApiKey.Generate(name);

        var api = new ProjectApi(key, project, createdBy, DateTimeOffset.UtcNow);

        return (api, secret);
    }

    public void Revoke(UserKey revokedBy)
    {
        ArgumentNullException.ThrowIfNull(revokedBy);
        if (!IsActive) return;

        RevokedBy = revokedBy;
        RevokedAt = DateTimeOffset.UtcNow;
    }


    public static ProjectApi Restore(
        ApiKey key,
        ProjectKey project,
        UserKey createdBy,
        DateTimeOffset createdAt,
        UserKey? revokedBy,
        DateTimeOffset? revokedAt)
    {
        if (createdAt == default)
            throw new ArgumentException("Invalid creation timestamp.", nameof(createdAt));

        if ((revokedBy is null) != (revokedAt is null))
            throw new ArgumentException("Revocation user and timestamp must both be present or absent.");

        if (revokedAt < createdAt)
            throw new ArgumentException("Revocation cannot precede creation.");

        return new ProjectApi(key, project, createdBy, createdAt)
        {
            RevokedBy = revokedBy,
            RevokedAt = revokedAt
        };
    }

}
