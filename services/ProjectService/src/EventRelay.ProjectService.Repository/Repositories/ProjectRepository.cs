using EventRelay.ProjectService.Application.Abstractions;
using EventRelay.ProjectService.Domain.Entities.Project;
using EventRelay.ProjectService.Infrastructure.Persistence;
using EventRelay.ProjectService.Infrastructure.Persistence.Models;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ProjectService.Repository.Repositories;

public sealed class ProjectRepository(ProjectDbContext dbContext) : IProjectRepository
{
    public async Task AddAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        await dbContext.Projects.AddAsync(
            project.Adapt<ProjectRecord>(),
            cancellationToken);
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
