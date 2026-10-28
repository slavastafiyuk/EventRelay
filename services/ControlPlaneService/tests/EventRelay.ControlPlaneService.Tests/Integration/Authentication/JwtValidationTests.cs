namespace EventRelay.ControlPlaneService.Tests.Integration.Authentication;


using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using EventRelay.ControlPlaneService.Tests.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

public sealed class JwtValidationTests(CreatePostgresContainer db)
    : IClassFixture<CreatePostgresContainer>
{
    private const string Issuer = "http://localhost:8081/realms/eventrelay";
    private const string Audience = "eventrelay-api";
    private static readonly SymmetricSecurityKey Key = new(RandomNumberGenerator.GetBytes(32));

    [Theory]
    [InlineData("valid", HttpStatusCode.NotFound)]
    [InlineData("missing", HttpStatusCode.Unauthorized)]
    [InlineData("malformed", HttpStatusCode.Unauthorized)]
    [InlineData("invalid-signature", HttpStatusCode.Unauthorized)]
    [InlineData("invalid-issuer", HttpStatusCode.Unauthorized)]
    [InlineData("invalid-audience", HttpStatusCode.Unauthorized)]
    [InlineData("expired", HttpStatusCode.Unauthorized)]
    public async Task ProtectedEndpoint_ValidatesJwt(string scenario, HttpStatusCode expected)
    {
        using var baseFactory = new ApiFactory(db.ConnectionString);

        using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme =
                        JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme =
                        JwtBearerDefaults.AuthenticationScheme;
                });

                services.PostConfigure<JwtBearerOptions>(
                    JwtBearerDefaults.AuthenticationScheme,
                    options =>
                    {
                        options.ConfigurationManager =
                            new StaticConfigurationManager<OpenIdConnectConfiguration>(
                                new OpenIdConnectConfiguration
                                {
                                    Issuer = Issuer,
                                    SigningKeys = { Key }
                                });

                        options.TokenValidationParameters = new()
                        {
                            ValidateIssuer = true,
                            ValidIssuer = Issuer,
                            ValidateAudience = true,
                            ValidAudience = Audience,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = Key,
                            RequireSignedTokens = true,
                            RequireExpirationTime = true,
                            ClockSkew = TimeSpan.Zero
                        };
                    });
            }));

        using var client = factory.CreateClient();

        string? token = scenario switch
        {
            "missing" => null,
            "malformed" => "not-a-jwt",
            "invalid-signature" => CreateToken(
                key: new SymmetricSecurityKey(
                    RandomNumberGenerator.GetBytes(32))),
            "invalid-issuer" => CreateToken(issuer: "wrong-issuer"),
            "invalid-audience" => CreateToken(audience: "wrong-api"),
            "expired" => CreateToken(expired: true),
            _ => CreateToken()
        };

        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.GetAsync(
            $"/api/projects/{Guid.NewGuid()}");

        Assert.Equal(expected, response.StatusCode);
    }

    private static string CreateToken(
        string? issuer = null,
        string? audience = null,
        SecurityKey? key = null,
        bool expired = false)
    {
        return new JsonWebTokenHandler().CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = issuer ?? Issuer,
                Audience = audience ?? Audience,
                Subject = new ClaimsIdentity(
                    [new Claim("sub", Guid.NewGuid().ToString())]),
                NotBefore = DateTime.UtcNow.AddMinutes(-10),
                Expires = expired
                    ? DateTime.UtcNow.AddMinutes(-5)
                    : DateTime.UtcNow.AddMinutes(5),
                SigningCredentials = new SigningCredentials(
                    key ?? Key,
                    SecurityAlgorithms.HmacSha256)
            });
    }
}
