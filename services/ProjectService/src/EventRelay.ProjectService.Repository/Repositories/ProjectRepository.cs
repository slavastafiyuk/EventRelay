using EventRelay.ProjectService.Application.Abstractions;
using EventRelay.ProjectService.Domain.Entities.Project;
using EventRelay.ProjectService.Infrastructure.Persistence;
using EventRelay.ProjectService.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ProjectService.Repository.Repositories;

public sealed class ProjectRepository(ProjectDbContext dbContext) : IProjectRepository
{
    public async Task AddAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        var record = new ProjectRecord
        {
            Id = project.Key.Id,
            Name = project.Key.Name,
            Description = project.Description
        };

        await dbContext.Projects.AddAsync(
            record,
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

        if (record is null)
            return null;

        return new Project(
            new ProjectKey(
                record.Id,
                record.Name))
        {
            Description = record.Description
        };
    }
}
