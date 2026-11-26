using EventRelay.ControlPlaneService.Application.Abstractions.Authentication;
using EventRelay.ControlPlaneService.Application.Abstractions.Persistence;
using EventRelay.ControlPlaneService.Application.Abstractions.Projects;
using EventRelay.ControlPlaneService.Domain.Entities.Organization;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;
using EventRelay.ControlPlaneService.Domain.Entities.User;
using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project.Access;
using EventRelay.ControlPlaneService.Presentation.Contracts.Project.Access.Actions;
using Mapster;

namespace EventRelay.ControlPlaneService.Presentation.Handlers;

public class ProjectHandler(
    IProjectService projectService,
    ICurrentUser currentUser) : IProjectHandler
{
    [Transactional]
    public async Task<ProjectKeyDTO> CreateAsync(ProjectCreateDTO dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var project = dto.Adapt(
            new Project(
                new ProjectKey(dto.Name),
                currentUser.UserKey,
                dto.Organization.Adapt<OrganizationKey>()));
        var result = await projectService.CreateAsync(project, cancellationToken);

        return result.Adapt<ProjectKeyDTO>();
    }

    [Transactional]
    public async Task<ProjectDTO?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await projectService.GetByIdAsync(
           id,
           cancellationToken);

        return result?.Adapt<ProjectDTO>();
    }

    #region Members Management

    [Transactional]
    public Task ManageMemberAsync(Guid id, ProjectMemberActionDTO dto, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(dto.UserKey);

        var user = dto.UserKey.Adapt<UserKey>();

        return dto switch
        {
            AddProjectMemberDTO add => projectService.AddMemberAsync(id, user, (ProjectRole)add.Role, cancellationToken),
            UpdateProjectMemberRoleDTO update => projectService.ChangeMemberRoleAsync(id, user, (ProjectRole)update.Role, cancellationToken),
            DeleteProjectMemberDTO => projectService.RemoveMemberAsync(id, user, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(dto))
        };
    }

    [Transactional]
    public Task ChangeVisibilityAsync(Guid id, ProjectVisibilityDTO visibility, CancellationToken cancellationToken)
    {
        return projectService.ChangeVisibilityAsync(id, (ProjectVisibility)visibility, cancellationToken);
    }

    #endregion Members Management
}
