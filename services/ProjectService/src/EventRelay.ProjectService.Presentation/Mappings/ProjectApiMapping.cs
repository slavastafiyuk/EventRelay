using EventRelay.ProjectService.Application.Models;
using EventRelay.ProjectService.Presentation.Contracts;
using Mapster;

namespace EventRelay.ProjectService.Presentation.Mappings;

public sealed class ProjectApiMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectStatus, ProjectPingResponse>()
            .MapToConstructor(true);
    }
}
