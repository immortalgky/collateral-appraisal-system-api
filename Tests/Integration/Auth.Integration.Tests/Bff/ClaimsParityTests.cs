using System.Net;
using System.Security.Claims;
using Auth.Application.Services;
using FluentAssertions;
using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace Integration.Auth.Integration.Tests.Bff;

/// <summary>
/// The app cookie holds identity only; permissions, roles and company_id are added per request. If they
/// ever diverge from what the access token carries the failures are silent — audits stamped "anonymous",
/// every IsInRole false, and an external-company user treated as internal staff (their data filter gone).
/// </summary>
[Collection("Integration")]
public class ClaimsParityTests(IntegrationTestFixture fixture)
{
    [Theory]
    [InlineData("RequestMaker", false, true)] // internal staff, with a user-level grant and deny on top of the role
    [InlineData("ExtAppraisalStaff", true, false)] // external company
    [InlineData("Admin", false, false)]
    public async Task Cookie_principal_matches_the_access_token_principal(string role, bool external,
        bool userLevelPermissions)
    {
        var factory = fixture.BffWebApplicationFactory;
        var user = await BffTestSupport.CreateUserAsync(factory.Services, role,
            external ? Guid.NewGuid() : null, userLevelPermissions);

        // What a Bearer caller (the access token) would carry.
        HashSet<(string Type, string Value)> bearer;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
            var identity = new ClaimsIdentity();
            identity.AddClaim(new Claim("sub", user.Id.ToString()));
            identity.AddClaim(new Claim("name", user.UserName));
            var grant = await tokens.CreateAuthCodeFlowAccessTokenPrincipal(
                new OpenIddictRequest(), new ClaimsPrincipal(identity));
            grant.Rejection.Should().BeNull();
            bearer = grant.Principal!.Claims
                .Where(c => c.Type is "permissions" or "roles" or "company_id")
                .Select(c => (c.Type, c.Value))
                .ToHashSet();
        }

        // What the application sees for a browser session.
        using var browser = factory.CreateBrowser(handleCookies: false);
        var cookie = await BffTestSupport.SignInAsync(browser, user);
        using var response = await BffTestSupport.GetAsync(browser, "/test/whoami", cookie);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var json = await BffTestSupport.ReadJsonAsync(response);
        var whoAmI = json.RootElement;

        bearer.Should().Contain(c => c.Type == "permissions").And.Contain(c => c.Type == "roles");
        BffTestSupport.AuthorizationClaims(whoAmI).Should().BeEquivalentTo(bearer);

        // The consumers that fail silently without those claims.
        whoAmI.GetProperty("userCode").GetString().Should().Be(user.UserName); // not "anonymous"/null
        whoAmI.GetProperty("authenticationType").GetString().Should().Be("CasSession");
        whoAmI.GetProperty("isExternal").GetBoolean().Should().Be(external);
        if (external)
            whoAmI.GetProperty("companyId").GetGuid().Should().Be(user.CompanyId!.Value);
        whoAmI.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).Should().Contain(role);
    }
}
