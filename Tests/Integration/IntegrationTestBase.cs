using Integration.Fixtures;

namespace Integration;

[Collection("Integration")]
public class IntegrationTestBase(IntegrationTestFixture fixture)
{
    // Exposed so a derived class that needs the fixture for more than the two clients below can use
    // this instead of re-referencing its own primary-constructor parameter — referencing that parameter
    // again in the derived class's body, on top of passing it to this base constructor, triggers CS9107
    // (captured into the derived type's state AND passed to the base constructor).
    protected IntegrationTestFixture Fixture { get; } = fixture;

    protected readonly HttpClient _client = fixture.IntegrationTestWebApplicationFactory.CreateClient();
    protected readonly HttpClient _authClient = fixture.AuthWebApplicationFactory.CreateClient();
}