using System.Security.Cryptography;
using System.Text;

namespace EventRelay.ControlPlaneService.Domain.Entities.Api;

public sealed class ApiKey : KeyBase<ApiKey>
{
    public string Name { get; private set; }
    public string KeyHash { get; private set; }

    private ApiKey(string name, string keyHash)
    {
        Name = ValidateName(name);
        KeyHash = ValidateHash(keyHash);
    }

    private ApiKey(Guid id, string name, string keyHash) : base(id)
    {
        Name = ValidateName(name);
        KeyHash = ValidateHash(keyHash);
    }

    public static (ApiKey Key, string Secret) Generate(string name)
    {
        var secret = "er_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var key = new ApiKey(name, Hash(secret));
        return (key, secret);
    }

    public static ApiKey Restore(Guid id, string name, string keyHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyHash);
        return new ApiKey(id, name, keyHash);
    }

    public static string Hash(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    }

    private static string ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        name = name.Trim();

        if (name.Length > 150)
            throw new ArgumentException(
                "API key name cannot exceed 150 characters.",
                nameof(name));

        return name;
    }

    private static string ValidateHash(string keyHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyHash);

        if (keyHash.Length != 64 || !keyHash.All(Uri.IsHexDigit))
            throw new ArgumentException("Invalid SHA-256 hash.", nameof(keyHash));

        return keyHash.ToUpperInvariant();
    }
}
