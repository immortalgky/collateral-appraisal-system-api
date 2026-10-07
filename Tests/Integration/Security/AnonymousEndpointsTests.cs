using System.Net;
using FluentAssertions;
using Integration.Fixtures;
using Integration.WebApplicationFactories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.Security;

/// <summary>
/// Guards every module's endpoints against silently opting out of the fallback authorization policy
/// (login required): the set of endpoints carrying IAllowAnonymous must equal an explicit allowlist.
/// One live request proves the fallback policy itself is active.
/// </summary>
[Collection("Integration")]
public class AnonymousEndpointsTests(IntegrationTestFixture fixture)
{
    // Every endpoint allowed to skip the fallback login policy, as "METHOD template" ("ANY" when the
    // endpoint has no HTTP method metadata). Anything else carrying IAllowAnonymous fails the test.
    private static readonly string[] AnonymousAllowlist =
    [
        "POST /auth/token",                    // login
        "POST /auth/refresh",                  // token refresh
        "GET /documents/{id:guid}/download",   // new-tab downloads cannot send a Bearer header
        "POST /api/v1/appraisals/result",      // legacy AS400 contract
        "GET connect/authorize",               // OpenIddict controller, [AllowAnonymous]
        "POST connect/token",                  // OpenIddict controller, [AllowAnonymous]
        "GET connect/logout",                  // OpenIddict controller, [AllowAnonymous]
        "POST connect/logout",                 // OpenIddict controller, [AllowAnonymous]
        "ANY Account/Login",                   // interactive login page, [AllowAnonymous]
        "ANY Account/AccessDenied",            // interactive access-denied page, [AllowAnonymous]
        "GET /openapi/{documentName}.json",    // Program.cs: MapOpenApi().AllowAnonymous()
        "ANY /health",                         // Program.cs: load balancer / monitoring probes
        "ANY /health/live",                    // Program.cs
        "ANY /health/ready",                   // Program.cs
        "ANY /health/external",                // Program.cs
        "ANY /hangfire/{**path}",              // anonymous only in Development (HangfireExtensions.cs); the test host always runs as Development
    ];

    // Program.cs: MapScalarApiReference().AllowAnonymous() also covers Scalar's static assets.
    private const string ScalarRoot = "GET /scalar";
    private const string ScalarAssets = "GET /scalar/";

    private static IEnumerable<string> Keys(RouteEndpoint e)
    {
        var methods = e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;
        return methods is { Count: > 0 }
            ? methods.Select(m => $"{m} {e.RoutePattern.RawText}")
            : [$"ANY {e.RoutePattern.RawText}"];
    }

    [Fact]
    public async Task Endpoints_RequireLogin_AndOnlyAllowlistedOnesAreAnonymous()
    {
        await using var factory = new AnonymousWebApplicationFactory(
            fixture.ConnectionString, fixture.RabbitMq.GetConnectionString());
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>().ToList();

        var anonymous = endpoints
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .SelectMany(Keys)
            .Where(k => k != ScalarRoot && !k.StartsWith(ScalarAssets))
            .ToList();
        anonymous.Should().BeEquivalentTo(AnonymousAllowlist);

        // The fallback policy itself is active: an endpoint without IAllowAnonymous rejects an anonymous caller.
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/requests", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
