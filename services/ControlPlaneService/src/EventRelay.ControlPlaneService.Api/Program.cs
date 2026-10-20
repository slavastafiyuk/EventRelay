using System.Reflection;
using EventRelay.ControlPlaneService.Application;
using EventRelay.ControlPlaneService.Infrastructure;
using EventRelay.ControlPlaneService.Presentation;
using EventRelay.ControlPlaneService.Presentation.Abstractions;
using EventRelay.ControlPlaneService.Repository;
using Mapster;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var config = TypeAdapterConfig.GlobalSettings;

config.Scan(
    typeof(EventRelay.ControlPlaneService.Repository.Mappings.Mapping).Assembly,
    typeof(EventRelay.ControlPlaneService.Presentation.Mappings.Mapping).Assembly);

config.RequireExplicitMapping = true;
config.Compile();

var builder = WebApplication.CreateBuilder(args);

var isGeneratingOpenApi =
    Assembly.GetEntryAssembly()?.GetName().Name
    == "GetDocument.Insider";


builder.Services.AddControllers();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Components ??= new OpenApiComponents();

            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

            document.Components.SecuritySchemes["OAuth2"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Description = "Keycloak Authorization Code flow with PKCE",
                Flows = new OpenApiOAuthFlows
                {
                    AuthorizationCode = new OpenApiOAuthFlow
                    {
                        AuthorizationUrl = new Uri("http://localhost:8081/realms/eventrelay/protocol/openid-connect/auth"),
                        TokenUrl = new Uri("http://localhost:8081/realms/eventrelay/protocol/openid-connect/token"),
                        Scopes = new Dictionary<string, string>
                        {
                            ["openid"] = "OpenID Connect",
                            ["profile"] = "Basic user profile"
                        }
                    }
                }
            };
            return Task.CompletedTask;
        });

    // Mark protected operations as requiring OAuth2.
    options.AddOperationTransformer((operation, context, cancellationToken) =>
        {
            var metadata = context.Description.ActionDescriptor.EndpointMetadata;
            var requiresAuthorization = metadata.OfType<IAuthorizeData>().Any()
                && !metadata.OfType<IAllowAnonymous>().Any();

            if (requiresAuthorization)
            {
                operation.Security ??= [];

                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("OAuth2", context.Document)] = []
                });
            }

            return Task.CompletedTask;
        });
});

builder.Services.AddInfrastructure(builder.Configuration, isGeneratingOpenApi);
builder.Services.AddRepositories();
builder.Services.AddApplication();
builder.Services.AddPresentation();
builder.Services.AddHealthChecks();


var issuer = builder.Configuration["Authentication:Issuer"];
var metadataAddress = builder.Configuration["Authentication:MetadataAddress"];
var audience = builder.Configuration["Authentication:Audience"];

// The real application must have valid authentication
// configuration. OpenAPI generation does not require
// a live identity provider.
if (!isGeneratingOpenApi &&
    (string.IsNullOrWhiteSpace(issuer) ||
     string.IsNullOrWhiteSpace(metadataAddress) ||
     string.IsNullOrWhiteSpace(audience)))
{
    throw new InvalidOperationException("JWT authentication configuration is missing.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MetadataAddress = metadataAddress!; // forgiven because of the check above
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,

            ValidateAudience = true,
            ValidAudience = audience,

            ValidateLifetime = true,

            ValidateIssuerSigningKey = true,

            RequireSignedTokens = true,
            RequireExpirationTime = true
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment()) app.MapOpenApi();

app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapControllers();

app.MapGet(
    "/api/projects/ping",
    (IPingHandler handler) =>
    {
        var response = handler.Handle();
        return Results.Ok(response);
    });

app.Run();

public partial class Program { }
