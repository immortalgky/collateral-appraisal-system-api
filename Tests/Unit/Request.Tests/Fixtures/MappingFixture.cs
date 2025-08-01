using Request.Configurations;
using Request.Tests.Fixtures;

[assembly: AssemblyFixture(typeof(MappingFixture))]

namespace Request.Tests.Fixtures;

public class MappingFixture : IDisposable
{
    public MappingFixture()
    {
        MappingConfiguration.ConfigureMappings();
    }
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    protected virtual void Dispose(bool disposing)
    {
    }
}