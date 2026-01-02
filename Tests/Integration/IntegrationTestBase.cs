using Integration.Fixtures;

namespace Integration;

[Collection("Integration")]
public class IntegrationTestBase(IntegrationTestFixture fixture)
{
    protected readonly HttpClient _client = fixture.IntegrationTestWebApplicationFactory.CreateClient();
    protected readonly HttpClient _authClient = fixture.AuthWebApplicationFactory.CreateClient();
}