using FluentAssertions;
using Integration.Fixtures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Auth.Integration.Tests;

/// <summary>
/// POST /auth/register used to let an anonymous caller create an account with any roles and permissions.
/// It is gone; account creation is only POST /auth/users, which needs a login (guarded elsewhere).
/// </summary>
[Collection("Integration")]
public class AnonymousAccountCreationTests(IntegrationTestFixture fixture)
{
    // Asserted on the route table, not on a status code: an unmatched path may be answered 401 by a
    // fallback policy, which would look the same as a protected endpoint.
    [Fact]
    public void Register_Route_DoesNotExist()
    {
        var routes = fixture.IntegrationTestWebApplicationFactory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText?.TrimStart('/') ?? "")
            .ToList();

        routes.Should().Contain("auth/users", "the route table must actually include the Auth endpoints");
        routes.Should().NotContain(
            route => route.StartsWith("auth/register", StringComparison.OrdinalIgnoreCase));
    }
}
