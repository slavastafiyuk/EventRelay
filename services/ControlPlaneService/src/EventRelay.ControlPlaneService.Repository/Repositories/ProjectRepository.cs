using EventRelay.ControlPlaneService.Application.Abstractions.Projects;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Repository.Repositories;

public sealed class ProjectRepository(ControlPlaneDbContext dbContext) : IProjectRepository
{
    public async Task AddAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        await dbContext.Projects.AddAsync(project.Adapt<ProjectRecord>(), cancellationToken);
    }

    public async Task<Project?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var record = await dbContext.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        return record?.Adapt<Project>();
    }
}
