
using EventRelay.ControlPlaneService.Api.Configuration;

namespace EventRelay.ControlPlaneService.Tests.Unit.Mappings;

[CollectionDefinition("Mapster mappings", DisableParallelization = true)]
public sealed class MappingTestsCollection
    : ICollectionFixture<MappingTestsFixture>
{
}

public sealed class MappingTestsFixture
{
    public MappingTestsFixture()
    {
        MapsterInitialization.Initialize();
    }
}
