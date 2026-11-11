using EventRelay.ControlPlaneService.Application.Abstractions.Api;
using EventRelay.ControlPlaneService.Application.Abstractions.Authentication;
using EventRelay.ControlPlaneService.Application.Abstractions.Persistence;
using EventRelay.ControlPlaneService.Application.Abstractions.Projects;
using EventRelay.ControlPlaneService.Application.Exceptions;
using EventRelay.ControlPlaneService.Domain.Entities.Api;
using EventRelay.ControlPlaneService.Domain.Entities.Project;
using EventRelay.ControlPlaneService.Domain.Entities.Project.Access;

namespace EventRelay.ControlPlaneService.Application.Services;

public sealed class ProjectApiService(
    IProjectApiRepository apiRepository,
    IProjectService projectService,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : IProjectApiService
{
    public async Task<IssuedApi> IssueAsync(Guid projectId, string name, CancellationToken ct)
    {
        var project = await GetManageableProjectAsync(projectId, ct);
        var (api, secret) = ProjectApi.Issue(project.Key, currentUser.UserKey, name);

        await apiRepository.AddAsync(api, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new IssuedApi(api, secret);
    }

    public async Task RevokeAsync(Guid projectId, Guid apiKeyId, CancellationToken ct)
    {
        await GetManageableProjectAsync(projectId, ct);

        var api = await apiRepository.GetByIdAsync(apiKeyId, ct)
            ?? throw new KeyNotFoundException("API key not found.");

        if (api.Project.Id != projectId)
            throw new KeyNotFoundException("API key not found.");

        if (!api.IsActive)
            return; // Idempotent revocation

        api.Revoke(currentUser.UserKey);

        await apiRepository.MarkRevokedAsync(api, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public Task<Guid?> ResolveProjectIdAsync(string keyHash, CancellationToken cancellationToken)
    {
        return apiRepository.ResolveProjectIdAsync(keyHash, cancellationToken);
    }

    #region Helper Methods

    private async Task<Project> GetManageableProjectAsync(Guid projectId, CancellationToken ct)
    {
        var project = await projectService.GetByIdAsync(projectId, ct)
            ?? throw new KeyNotFoundException("Project not found.");

        if (!project.HasPermission(currentUser.UserKey, ProjectPermission.ManageApiKeys))
        {
            throw new ProjectAccessDeniedException();
        }

        return project;
    }

    #endregion Helper Methods
}
