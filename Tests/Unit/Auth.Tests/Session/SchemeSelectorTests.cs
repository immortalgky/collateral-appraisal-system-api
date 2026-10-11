using Auth.Infrastructure.Session;
using Microsoft.AspNetCore.Http;
using Shared.Identity;

namespace Auth.Tests.Session;

public class SchemeSelectorTests
{
    private static HttpRequest Request(params (string Name, string Value)[] headers)
    {
        var context = new DefaultHttpContext();
        foreach (var (name, value) in headers) context.Request.Headers[name] = value;
        return context.Request;
    }

    [Fact]
    public void No_credentials_means_the_app_cookie() =>
        Assert.Equal(AuthSchemes.AppCookie, SchemeSelector.Select(Request(), allowDevBypass: false));

    [Theory]
    [InlineData("Bearer abc")]
    [InlineData("bearer abc")]
    public void A_bearer_header_means_openiddict(string header) =>
        Assert.Equal("OpenIddict.Validation.AspNetCore",
            SchemeSelector.Select(Request(("Authorization", header)), allowDevBypass: false));

    [Fact]
    public void Other_authorization_schemes_fall_through_to_the_cookie() =>
        Assert.Equal(AuthSchemes.AppCookie,
            SchemeSelector.Select(Request(("Authorization", "Basic abc")), allowDevBypass: false));

    [Theory]
    [InlineData(true, "DevBypass")]
    [InlineData(false, AuthSchemes.AppCookie)] // the header means nothing in production
    public void The_dev_header_counts_only_outside_production(bool allowDevBypass, string expected) =>
        Assert.Equal(expected, SchemeSelector.Select(
            Request((DevAuthenticationHandler.DevHeaderName, DevAuthenticationHandler.DevHeaderValue)), allowDevBypass));

    [Fact]
    public void The_dev_header_outranks_a_bearer_outside_production_and_loses_to_it_in_production()
    {
        var request = Request((DevAuthenticationHandler.DevHeaderName, DevAuthenticationHandler.DevHeaderValue),
            ("Authorization", "Bearer abc"));

        Assert.Equal("DevBypass", SchemeSelector.Select(request, allowDevBypass: true));
        Assert.Equal("OpenIddict.Validation.AspNetCore", SchemeSelector.Select(request, allowDevBypass: false));
    }
}
