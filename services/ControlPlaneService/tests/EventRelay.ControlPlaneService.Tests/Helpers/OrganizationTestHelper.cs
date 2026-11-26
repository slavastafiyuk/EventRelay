
using System.Net;
using System.Net.Http.Json;
using EventRelay.ControlPlaneService.Presentation.Contracts.Organization;

namespace EventRelay.ControlPlaneService.Tests.Helpers;

public static class OrganizationTestHelper
{
    public static async Task<OrganizationKeyDTO> CreateAsync(
        HttpClient client,
        string subject)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/organizations");

        request.Headers.Add("X-Test-User", subject);

        request.Content = JsonContent.Create(
            new OrganizationCreateDTO
            {
                Name = $"Org-{Guid.NewGuid():N}"
            });

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var organization = await response.Content
            .ReadFromJsonAsync<OrganizationKeyDTO>();

        return Assert.IsType<OrganizationKeyDTO>(organization);
    }
}
