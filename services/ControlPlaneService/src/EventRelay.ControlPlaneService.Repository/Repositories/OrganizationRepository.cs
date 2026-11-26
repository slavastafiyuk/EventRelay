using System.Data;
using EventRelay.ControlPlaneService.Application.Abstractions.Organizations;
using EventRelay.ControlPlaneService.Domain.Entities.Organization;
using EventRelay.ControlPlaneService.Domain.Entities.Organization.Access;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Organization;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Project.Access;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EventRelay.ControlPlaneService.Repository.Repositories;

public sealed class OrganizationRepository(
    ControlPlaneDbContext dbContext) : IOrganizationRepository
{
    #region Organization Management

    public async Task AddAsync(Organization organization, CancellationToken cancellationToken)
    {
        await dbContext.Organizations.AddAsync(organization.Adapt<OrganizationRecord>(), cancellationToken);
    }

    public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var record = await dbContext.Organizations
            .AsNoTracking()
            .Include(x => x.Owner)
            .Include(x => x.Members)
            .ThenInclude(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return record?.Adapt<Organization>();
    }

    public async Task<OrganizationKey?> GetKeyAsync(OrganizationKey key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        var record = await dbContext
            .Set<OrganizationKeyRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == key.Id, cancellationToken);

        return record?.Adapt<OrganizationKey>();
    }

    #endregion Organization Management

    #region Members Management

    public async Task AddMemberAsync(Guid organizationId, OrganizationMember member, CancellationToken cancellationToken)
    {
        var record = member.Adapt<OrganizationMemberRecord>();

        record.OrganizationId = organizationId;

        await dbContext.Set<OrganizationMemberRecord>().AddAsync(record, cancellationToken);
    }

    public Task ChangeMemberRoleAsync(Guid organizationId, Guid userId, OrganizationRole role, CancellationToken cancellationToken)
    {
        var record = new OrganizationMemberRecord
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = role
        };

        dbContext.Attach(record);
        dbContext.Entry(record).Property(x => x.Role).IsModified = true;

        return Task.CompletedTask;
    }

    public Task<bool> HasOwnedProjectsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        return dbContext.Projects.AnyAsync(
            project =>
            project.OrganizationId == organizationId &&
            project.OwnerId == userId,
            cancellationToken);
    }

    public async Task RemoveMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        EnsureSerializableTransaction();

        await RemoveProjectMembershipsAsync(organizationId, userId, cancellationToken);

        var record = new OrganizationMemberRecord
        {
            OrganizationId = organizationId,
            UserId = userId
        };

        dbContext.Set<OrganizationMemberRecord>().Remove(record);
    }

    #endregion Members Management

    #region Helper Methods

    private async Task RemoveProjectMembershipsAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        await dbContext.Set<ProjectMemberRecord>()
            .Where(member =>
                member.UserId == userId &&
                dbContext.Projects.Any(project => project.Id == member.ProjectId && project.OrganizationId == organizationId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private void EnsureSerializableTransaction()
    {
        var transaction = dbContext.Database.CurrentTransaction;

        if (transaction is null || transaction.GetDbTransaction().IsolationLevel != IsolationLevel.Serializable)
            throw new InvalidOperationException("Organization membership removal requires an active Serializable transaction.");
    }

    #endregion Helper Methods
}
