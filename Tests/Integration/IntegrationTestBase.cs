using Integration.Factories;
using Integration.Fixtures;

namespace Integration;

[Collection("Integration")]
public class IntegrationTestBase
{
    protected readonly HttpClient _client;

    public IntegrationTestBase(IntegrationTestFixture fixture)
    {
        var factory = new TestWebApplicationFactory(fixture.Mssql, fixture.RabbitMq);
        _client = factory.CreateClient();
    }
}