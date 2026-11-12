
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using EventRelay.ControlPlaneService.Infrastructure.Persistence;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization.Access;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization.Access.Actions;
using EventRelay.ControlPlaneService.Presentation.Contracts.User;
using EventRelay.ControlPlaneService.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace EventRelay.ControlPlaneService.Tests.Integration.OrganizationTests;

public sealed class OrganizationMemberManagementIntegrationTests(
    CreatePostgresContainer fixture)
    : IClassFixture<CreatePostgresContainer>
{
    private static readonly JsonSerializerOptions JsonOptions = new(
        JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static string NewSubject() =>
        $"user-{Guid.NewGuid():N}";

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string? subject,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);

        if (subject is not null)
            request.Headers.Add("X-Test-User", subject);

        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);

        return await fixture.Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> ManageMemberAsync(
        Guid organizationId,
        string? subject,
        OrganizationMemberActionDTO action)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/organizations/{organizationId}/members/actions");

        if (subject is not null)
            request.Headers.Add("X-Test-User", subject);

        // Serialize as the base type so the "action"
        // discriminator is included in the JSON.
        request.Content = JsonContent.Create<OrganizationMemberActionDTO>(
            action,
            options: JsonOptions);

        return await fixture.Client.SendAsync(request);
    }

    private async Task<Guid> CreateOrganizationAsync(string subject)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            "/api/organizations",
            subject,
            new OrganizationCreateDTO
            {
                Name = $"Organization-{Guid.NewGuid():N}"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<OrganizationKeyDTO>(JsonOptions);

        Assert.NotNull(result);

        return result.Id;
    }

    private async Task<OrganizationDTO> GetOrganizationAsync(
        Guid organizationId,
        string subject)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            $"/api/organizations/{organizationId}",
            subject);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content
            .ReadFromJsonAsync<OrganizationDTO>(JsonOptions);

        Assert.NotNull(result);

        return result;
    }

    private async Task<UserKeyDTO> ProvisionUserAsync(
        Guid organizationId,
        string subject)
    {
        // The middleware provisions the authenticated user,
        // even when organization access is denied.
        using (var response = await SendAsync(
            HttpMethod.Get,
            $"/api/organizations/{organizationId}",
            subject))
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        var options = new DbContextOptionsBuilder<ControlPlaneDbContext>()
            .UseNpgsql(fixture.ConnectionString)
            .Options;

        await using var db = new ControlPlaneDbContext(options);

        var user = await db.Users
            .AsNoTracking()
            .SingleAsync(x => x.Subject == subject);

        return new UserKeyDTO
        {
            Id = user.Id,
            Name = user.Name
        };
    }

    [Fact]
    public async Task Owner_CanAddPromoteDemoteAndRemoveMember()
    {
        // Arrange
        var ownerSubject = NewSubject();
        var memberSubject = NewSubject();

        var organizationId = await CreateOrganizationAsync(ownerSubject);
        var member = await ProvisionUserAsync(organizationId, memberSubject);

        var initial = await GetOrganizationAsync(
            organizationId, ownerSubject);

        Assert.Single(initial.Members);
        Assert.Equal(initial.Owner.Id, initial.Members.Single().User.Id);
        Assert.Equal(
            OrganizationRoleDTO.Admin,
            initial.Members.Single().Role);

        // Add member
        using (var response = await ManageMemberAsync(
            organizationId,
            ownerSubject,
            new AddOrganizationMemberDTO
            {
                UserKey = member,
                Role = OrganizationRoleDTO.Member
            }))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var afterAdd = await GetOrganizationAsync(
            organizationId, ownerSubject);

        Assert.Equal(2, afterAdd.Members.Count);

        var added = Assert.Single(
            afterAdd.Members,
            x => x.User.Id == member.Id);

        Assert.Equal(OrganizationRoleDTO.Member, added.Role);

        // Member now has access to the organization.
        using (var response = await SendAsync(
            HttpMethod.Get,
            $"/api/organizations/{organizationId}",
            memberSubject))
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // Promote to Admin
        using (var response = await ManageMemberAsync(
            organizationId,
            ownerSubject,
            new UpdateOrganizationMemberToAdminDTO
            {
                UserKey = member
            }))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var afterPromotion = await GetOrganizationAsync(
            organizationId, ownerSubject);

        var promoted = Assert.Single(
            afterPromotion.Members,
            x => x.User.Id == member.Id);

        Assert.Equal(OrganizationRoleDTO.Admin, promoted.Role);
        Assert.Equal(added.JoinedAt, promoted.JoinedAt);

        // Demote to Member
        using (var response = await ManageMemberAsync(
            organizationId,
            ownerSubject,
            new UpdateOrganizationMemberToMemberDTO
            {
                UserKey = member
            }))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var afterDemotion = await GetOrganizationAsync(
            organizationId, ownerSubject);

        var demoted = Assert.Single(
            afterDemotion.Members,
            x => x.User.Id == member.Id);

        Assert.Equal(OrganizationRoleDTO.Member, demoted.Role);

        // Remove member
        using (var response = await ManageMemberAsync(
            organizationId,
            ownerSubject,
            new DeleteOrganizationMemberDTO
            {
                UserKey = member
            }))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var afterRemoval = await GetOrganizationAsync(
            organizationId, ownerSubject);

        Assert.Single(afterRemoval.Members);
        Assert.DoesNotContain(
            afterRemoval.Members,
            x => x.User.Id == member.Id);

        // Removed member loses organization access.
        using var removedMemberResponse = await SendAsync(
            HttpMethod.Get,
            $"/api/organizations/{organizationId}",
            memberSubject);

        Assert.Equal(
            HttpStatusCode.NotFound,
            removedMemberResponse.StatusCode);
    }

    [Theory]
    [InlineData(OrganizationRoleDTO.Member)]
    [InlineData(OrganizationRoleDTO.Admin)]
    public async Task NonOwner_CannotManageMembers(
        OrganizationRoleDTO role)
    {
        // Arrange
        var ownerSubject = NewSubject();
        var memberSubject = NewSubject();
        var otherSubject = NewSubject();

        var organizationId = await CreateOrganizationAsync(ownerSubject);

        var member = await ProvisionUserAsync(organizationId, memberSubject);
        var other = await ProvisionUserAsync(organizationId, otherSubject);

        using (var response = await ManageMemberAsync(
            organizationId,
            ownerSubject,
            new AddOrganizationMemberDTO
            {
                UserKey = member,
                Role = role
            }))
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        // Act: a non-owner attempts to add another user.
        using var forbiddenResponse = await ManageMemberAsync(
            organizationId,
            memberSubject,
            new AddOrganizationMemberDTO
            {
                UserKey = other,
                Role = OrganizationRoleDTO.Member
            });

        // Assert: even Admin cannot manage members
        // because the current policy is owner-only.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            forbiddenResponse.StatusCode);

        var organization = await GetOrganizationAsync(
            organizationId, ownerSubject);

        Assert.Equal(2, organization.Members.Count);
        Assert.DoesNotContain(
            organization.Members,
            x => x.User.Id == other.Id);
    }

    [Fact]
    public async Task Owner_CannotRemoveOrDemoteSelf()
    {
        // Arrange
        var ownerSubject = NewSubject();
        var organizationId = await CreateOrganizationAsync(ownerSubject);

        var organization = await GetOrganizationAsync(
            organizationId, ownerSubject);

        // Attempt to demote owner
        using (var response = await ManageMemberAsync(
            organizationId,
            ownerSubject,
            new UpdateOrganizationMemberToMemberDTO
            {
                UserKey = organization.Owner
            }))
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        // Attempt to remove owner
        using (var response = await ManageMemberAsync(
            organizationId,
            ownerSubject,
            new DeleteOrganizationMemberDTO
            {
                UserKey = organization.Owner
            }))
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        // Owner remains an Admin.
        var after = await GetOrganizationAsync(
            organizationId, ownerSubject);

        var ownerMember = Assert.Single(after.Members);

        Assert.Equal(organization.Owner.Id, ownerMember.User.Id);
        Assert.Equal(OrganizationRoleDTO.Admin, ownerMember.Role);
    }

    [Fact]
    public async Task AddingExistingMember_ReturnsConflict()
    {
        var ownerSubject = NewSubject();
        var memberSubject = NewSubject();

        var organizationId = await CreateOrganizationAsync(ownerSubject);
        var member = await ProvisionUserAsync(organizationId, memberSubject);

        var action = new AddOrganizationMemberDTO
        {
            UserKey = member,
            Role = OrganizationRoleDTO.Member
        };

        using (var first = await ManageMemberAsync(
            organizationId, ownerSubject, action))
        {
            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        }

        using var duplicate = await ManageMemberAsync(
            organizationId, ownerSubject, action);

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var organization = await GetOrganizationAsync(
            organizationId, ownerSubject);

        Assert.Equal(2, organization.Members.Count);

        var savedMember = Assert.Single(
            organization.Members,
            x => x.User.Id == member.Id);

        Assert.Equal(OrganizationRoleDTO.Member, savedMember.Role);
    }

    [Fact]
    public async Task UserFromAnotherOrganization_CannotManageMembers()
    {
        var ownerSubject = NewSubject();
        var foreignOwnerSubject = NewSubject();

        var organizationId = await CreateOrganizationAsync(ownerSubject);
        await CreateOrganizationAsync(foreignOwnerSubject);

        var organization = await GetOrganizationAsync(
            organizationId, ownerSubject);

        // Foreign organization owner cannot read this organization.
        using (var response = await SendAsync(
            HttpMethod.Get,
            $"/api/organizations/{organizationId}",
            foreignOwnerSubject))
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        // Foreign organization owner cannot manage its members.
        using (var response = await ManageMemberAsync(
            organizationId,
            foreignOwnerSubject,
            new DeleteOrganizationMemberDTO
            {
                UserKey = organization.Owner
            }))
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        var unchanged = await GetOrganizationAsync(
            organizationId, ownerSubject);

        Assert.Single(unchanged.Members);
    }

    [Fact]
    public async Task MemberManagement_RequiresAuthentication()
    {
        var ownerSubject = NewSubject();
        var organizationId = await CreateOrganizationAsync(ownerSubject);

        var organization = await GetOrganizationAsync(
            organizationId, ownerSubject);

        using var response = await ManageMemberAsync(
            organizationId,
            subject: null,
            new DeleteOrganizationMemberDTO
            {
                UserKey = organization.Owner
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
