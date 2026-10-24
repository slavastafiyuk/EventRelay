namespace EventRelay.ControlPlaneService.Domain.Entities.User;

public class User
{
    public UserKey Key { get; set; } = null!;
    public string Issuer { get; }
    public string Subject { get; }

    public User(UserKey key, string issuer, string subject)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));

        Issuer = !string.IsNullOrWhiteSpace(issuer)
            ? issuer
            : throw new ArgumentException(nameof(issuer));

        Subject = !string.IsNullOrWhiteSpace(subject)
            ? subject
            : throw new ArgumentException(nameof(subject));

    }
}
