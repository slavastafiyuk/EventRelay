using EventRelay.ControlPlaneService.Application.Abstractions.Organizations;
using EventRelay.ControlPlaneService.Domain.Entities.Organization;
using EventRelay.ControlPlaneService.Domain.Entities.Organization.Access;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Infrastructure.Persistence.Models.Organization;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Repository.Repositories;

public sealed class OrganizationRepository(
    ControlPlaneDbContext dbContext) : IOrganizationRepository
{
    public async Task AddAsync(Organization organization, CancellationToken cancellationToken)
    {
        await dbContext.Organizations.AddAsync(
            organization.Adapt<OrganizationRecord>(),
            cancellationToken);
    }

    public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var record = await dbContext.Organizations
            .AsNoTracking()
            .Include(x => x.Owner)
            .Include(x => x.Members)
            .ThenInclude(x => x.User)
            .SingleOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);

        return record?.Adapt<Organization>();
    }

    public async Task AddMemberAsync(
        Guid organizationId,
        OrganizationMember member,
        CancellationToken cancellationToken)
    {
        var record = member.Adapt<OrganizationMemberRecord>();

        record.OrganizationId = organizationId;
        await dbContext.Set<OrganizationMemberRecord>()
            .AddAsync(record, cancellationToken);
    }


    public Task ChangeMemberRoleAsync(
        Guid organizationId,
        Guid userId,
        OrganizationRole role,
        CancellationToken cancellationToken)
    {
        var record = new OrganizationMemberRecord
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = role
        };

        dbContext.Attach(record);

        dbContext.Entry(record)
            .Property(x => x.Role)
            .IsModified = true;

        return Task.CompletedTask;
    }

    public Task RemoveMemberAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var record = new OrganizationMemberRecord
        {
            OrganizationId = organizationId,
            UserId = userId
        };

        dbContext.Set<OrganizationMemberRecord>().Remove(record);

        return Task.CompletedTask;
    }

}
