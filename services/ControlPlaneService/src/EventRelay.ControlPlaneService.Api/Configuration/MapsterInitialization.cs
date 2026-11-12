using Mapster;

namespace EventRelay.ControlPlaneService.Api.Configuration;

public static class MapsterInitialization
{
    private static readonly Lazy<bool> Initialized = new(() =>
    {
        var config = TypeAdapterConfig.GlobalSettings;

        config.Scan(
            typeof(Repository.Mappings.Mapping).Assembly,
            typeof(Presentation.Mappings.Mapping).Assembly);

        config.RequireExplicitMapping = true;
        config.Compile();

        return true;
    });

    public static void Initialize() => _ = Initialized.Value;
}
