using EventRelay.ControlPlaneService.Application.Abstractions.Projects;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project.Access;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Repository.Repositories;

public sealed class ProjectRepository(ControlPlaneDbContext dbContext) : IProjectRepository
{
    public async Task AddAsync(Project project, CancellationToken cancellationToken)
    {
        await dbContext.Projects.AddAsync(project.Adapt<ProjectRecord>(), cancellationToken);
    }
    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var record = await dbContext.Projects
            .AsNoTracking()
            .Include(x => x.Owner)
            .Include(x => x.Organization)
            .Include(x => x.Access).ThenInclude(x => x.Members).ThenInclude(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return record?.Adapt<Project>();
    }

    public async Task<ProjectKey?> GetKeyAsync(ProjectKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        var record = await dbContext.Set<ProjectKeyRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == key.Id, cancellationToken);

        return record?.Adapt<ProjectKey>();
    }

    #region Members Management

    public async Task AddMemberAsync(ProjectKey key, ProjectMember member, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(member);
        var record = member.Adapt<ProjectMemberRecord>();
        record.ProjectId = key.Id;

        await dbContext.Set<ProjectMemberRecord>().AddAsync(record, cancellationToken);
    }

    public Task ChangeMemberRoleAsync(ProjectKey key, ProjectMember member, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(member);

        var record = member.Adapt<ProjectMemberRecord>();
        record.ProjectId = key.Id;

        dbContext.Attach(record);

        dbContext.Entry(record)
            .Property(x => x.Role)
            .IsModified = true;

        return Task.CompletedTask;
    }

    public Task RemoveMemberAsync(ProjectKey key, UserKey user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(user);

        var record = new ProjectMemberRecord
        {
            ProjectId = key.Id,
            UserId = user.Id
        };

        dbContext.Set<ProjectMemberRecord>().Remove(record);
        return Task.CompletedTask;
    }

    public async Task ChangeVisibilityAsync(ProjectKey key, ProjectVisibility visibility, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        var access = await dbContext.Set<ProjectAccessRecord>()
            .Include(x => x.Members)
            .SingleAsync(x => x.ProjectId == key.Id, cancellationToken);

        access.Visibility = visibility;

        if (visibility is ProjectVisibility.Private or ProjectVisibility.Public)
            dbContext.Set<ProjectMemberRecord>().RemoveRange(access.Members);
    }

    #endregion Members Management
}
