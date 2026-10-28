using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace EventRelay.ControlPlaneService.Tests.E2E.Keycloak;

public sealed class AuthenticatedApiFixture(
    KeycloakFixture keycloak,
    string connectionString) : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ProjectDatabase"] = connectionString
            }));

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var realm = $"{keycloak.BaseUrl.TrimEnd('/')}/realms/eventrelay";
        var metadata = $"{realm}/.well-known/openid-configuration";

        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Issuer"] = realm,
                ["Authentication:MetadataAddress"] = metadata,
                ["Authentication:Audience"] = "eventrelay-api",
                ["ConnectionStrings:ProjectDatabase"] = connectionString
            }));

        builder.ConfigureTestServices(services =>
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.MetadataAddress = metadata;
                    options.RequireHttpsMetadata = false;
                    options.MapInboundClaims = false;

                    options.ConfigurationManager =
                        new ConfigurationManager<OpenIdConnectConfiguration>(
                            metadata,
                            new OpenIdConnectConfigurationRetriever(),
                            new HttpDocumentRetriever { RequireHttps = false });

                    var validation = options.TokenValidationParameters;

                    validation.ValidIssuer = realm;
                    validation.ValidIssuers = null;
                    validation.ValidateIssuer = true;

                    validation.ValidAudience = "eventrelay-api";
                    validation.ValidAudiences = null;
                    validation.ValidateAudience = true;

                    validation.ValidateLifetime = true;
                    validation.ValidateIssuerSigningKey = true;
                    validation.RequireSignedTokens = true;
                    validation.RequireExpirationTime = true;
                }));
    }
}
