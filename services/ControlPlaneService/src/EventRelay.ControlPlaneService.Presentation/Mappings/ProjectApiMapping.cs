using EventRelay.ControlPlaneService.Application.Models;
using EventRelay.ControlPlaneService.Presentation.Contracts;
using Mapster;

namespace EventRelay.ControlPlaneService.Presentation.Mappings;

public sealed class ProjectApiMapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<ProjectStatus, ProjectPingResponse>()
            .MapToConstructor(true);
    }
}
