using Mapster;

namespace EventRelay.ControlPlaneService.Tests.Unit.Mappings;

[CollectionDefinition("Mapster mappings", DisableParallelization = true)]
public sealed class MappingTestsCollection : ICollectionFixture<MappingTestsFixture>
{

}

public sealed class MappingTestsFixture
{
    public MappingTestsFixture()
    {
        var config = TypeAdapterConfig.GlobalSettings;

        config.Scan(
            typeof(Repository.Mappings.Mapping).Assembly,
            typeof(Presentation.Mappings.Mapping).Assembly);
        config.RequireExplicitMapping = true;

        config.Compile();

    }
}
