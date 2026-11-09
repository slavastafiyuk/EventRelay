namespace EventRelay.ControlPlaneService.Domain.Entities.Api;

public sealed class IssuedApi
{
    public ProjectApi Api { get; set; }
    public string Secret { get; set; }
    public IssuedApi(ProjectApi api, string secret)
    {
        Api = api;
        Secret = secret;
    }
}
