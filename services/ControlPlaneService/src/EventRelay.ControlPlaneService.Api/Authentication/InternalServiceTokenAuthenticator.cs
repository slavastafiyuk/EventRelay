using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace EventRelay.ControlPlaneService.Api.Authentication;

public sealed class InternalServiceTokenAuthenticator(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration) : AuthenticationHandler<AuthenticationSchemeOptions>(
        options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var expectedToken = configuration["InternalApi:Token"];

        if (string.IsNullOrWhiteSpace(expectedToken))
        {
            return Task.FromResult(AuthenticateResult.Fail("Internal authentication is not configured."));
        }

        if (!Request.Headers.TryGetValue(InternalServiceAuthenticationDefaults.HeaderName, out var values) || values.Count != 1)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var suppliedToken = values[0];
        if (string.IsNullOrWhiteSpace(suppliedToken))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid internal token."));
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedToken);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedToken);
        var isValid = expectedBytes.Length == suppliedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);

        if (!isValid)
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid internal token."));
        }

        var claims = new[]
        {
            new Claim(  ClaimTypes.NameIdentifier, "eventrelay-internal-service")
        };

        var identity = new ClaimsIdentity(claims, InternalServiceAuthenticationDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);

        return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(
                    principal,
                    InternalServiceAuthenticationDefaults.Scheme)));
    }
}
