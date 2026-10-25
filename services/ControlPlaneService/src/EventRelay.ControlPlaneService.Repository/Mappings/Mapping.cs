using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models;
using Mapster;

namespace EventRelay.ControlPlaneService.Repository.Mappings;

public sealed class Mapping : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Project, ProjectRecord>()
            .Map(dest => dest.Id, src => src.Key.Id)
            .Map(dest => dest.Name, src => src.Key.Name);

        config.NewConfig<ProjectRecord, Project>()
            .MapWith(src => new Project(
                new ProjectKey(src.Id, src.Name),
                new UserKey(src.OwnerId, src.OwnerName))
            {
                Description = src.Description
            });

        config.NewConfig<User, UserRecord>()
            .Map(dest => dest.Id, src => src.Key.Id)
            .Map(dest => dest.Issuer, src => src.Issuer)
            .Map(dest => dest.Subject, src => src.Subject)
            .Map(dest => dest.Name, src => src.Key.Name);

        config.NewConfig<UserRecord, User>()
            .MapWith(src => new User(new UserKey(src.Id, src.Name), src.Issuer, src.Subject));
    }
}
