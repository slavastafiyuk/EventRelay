using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference("/scalar", options =>
    {
        options
            .WithTitle("EventRelay API")
            .AddDocument(
                "controlplane",
                "Control Plane API",
                "/docs/specs/controlplane.json")
            .AddDocument(
                "events",
                "Event Ingestion API",
                "/docs/specs/events.json")
            .AddServer(
                "http://localhost:5000",
                "Local Gateway")
            .AddPreferredSecuritySchemes("OAuth2")
            .AddAuthorizationCodeFlow("OAuth2", flow =>
            {
                flow.ClientId = "eventrelay-scalar";
                flow.Pkce = Pkce.Sha256;
                flow.RedirectUri = "http://localhost:5000/scalar/";
                flow.SelectedScopes =
                [
                    "openid",
                    "profile"
                ];
            });
    });
}

app.MapReverseProxy();
app.Run();

public partial class Program { }
