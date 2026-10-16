using EventRelay.ProjectService.Domain.Entities.Project;
using EventRelay.ProjectService.Infrastructure.Persistence.Models;
using Mapster;

namespace EventRelay.ProjectService.Repository.Mappings;

public sealed class ProjectPersistenceMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Project, ProjectRecord>()
            .Map(dest => dest.Id, src => src.Key.Id)
            .Map(dest => dest.Name, src => src.Key.Name);

        config.NewConfig<ProjectRecord, Project>()
            .MapWith(src => new Project(
                new ProjectKey(src.Id, src.Name))
            {
                Description = src.Description
            });
    }
}
