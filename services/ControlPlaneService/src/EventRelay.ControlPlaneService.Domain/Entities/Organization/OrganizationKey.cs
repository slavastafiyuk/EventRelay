namespace EventRelay.ControlPlaneService.Domain.Entities.Organization;

public sealed class OrganizationKey : KeyBaseAlternative<OrganizationKey, string>
{
    public string Name
    {
        get;
        set => field = string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Invalid project name.")
            : value.Trim();
    } = null!;

    public override string SecondaryKey => Name;

    private OrganizationKey() { }

    public OrganizationKey(string name) => Name = name;

    public OrganizationKey(Guid id, string name) : base(id) => Name = name;
}
