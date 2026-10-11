using Auth.Infrastructure.Session;

namespace Auth.Tests.Session;

public class BffUrlsTests
{
    private static BffUrls Create() => new(
        "https://api.example.test:7111/",
        ["https://spa.example.test:3000", "https://spa2.example.test:3001/"]);

    [Fact]
    public void Missing_or_placeholder_app_base_url_fails_fast() =>
        Assert.Throws<InvalidOperationException>(() => new BffUrls("<APP_BASE_URL>", []));

    [Fact]
    public void A_path_in_app_base_url_or_the_idp_url_fails_fast()
    {
        Assert.Throws<InvalidOperationException>(() => new BffUrls("https://api.example.test:7111/cas", []));
        Assert.Throws<InvalidOperationException>(() =>
            new BffUrls("https://api.example.test:7111", [], "https://idp.example.test:8443/idp"));
    }

    [Theory]
    [InlineData("https://spa.example.test:3000/appraisal/1?tab=2")]
    [InlineData("https://SPA.example.test:3000/")]
    [InlineData("https://spa2.example.test:3001/x")]
    public void Return_url_on_an_allowed_spa_origin_is_kept(string returnUrl) =>
        Assert.Equal(new Uri(returnUrl).AbsoluteUri, Create().ResolveReturnUrl(returnUrl));

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("https://spa.example.test:3000.evil.example/")] // prefix match would let this through
    [InlineData("https://spa.example.test:30000/")]
    [InlineData("https://spa.example.test:3000@evil.example/")] // user-info trick
    [InlineData("http://spa.example.test:3000/")] // scheme is part of the origin
    [InlineData("//evil.example/x")]
    [InlineData("/\\evil.example/x")]
    [InlineData("/x\r\nSet-Cookie: a=b")]
    [InlineData("relative/no/slash")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    [InlineData(null)]
    public void Any_other_return_url_falls_back_to_the_spa_home(string? returnUrl) =>
        Assert.Equal("https://spa.example.test:3000/", Create().ResolveReturnUrl(returnUrl));

    [Fact]
    public void Spa_home_is_slash_when_no_origin_is_configured() =>
        Assert.Equal("/", new BffUrls("https://api.example.test", []).SpaHome);

    [Fact]
    public void ToPublic_swaps_the_internal_origin_for_the_public_one_and_keeps_the_path() =>
        Assert.Equal(
            "https://api.example.test:7111/connect/logout",
            Create().ToPublic("https://localhost:7111/connect/logout"));

    [Theory]
    [InlineData("https://spa.example.test:3000", true)]
    [InlineData("https://spa2.example.test:3001/", true)]
    [InlineData("https://api.example.test:7111", true)] // the API itself
    [InlineData("https://evil.example", false)]
    [InlineData("null", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Trusted_origins_are_the_spa_origins_and_the_api(string? origin, bool expected) =>
        Assert.Equal(expected, Create().IsTrustedOrigin(origin));

    [Fact]
    public void The_idp_address_defaults_to_the_api_address() =>
        Assert.Equal(Create().PublicBase, Create().IdpPublicBase);

    [Fact]
    public void A_split_idp_gets_the_identity_provider_endpoints_while_callbacks_stay_on_the_api()
    {
        var urls = new BffUrls("https://api.example.test:7111", ["https://spa.example.test:3000"],
            "https://idp.example.test:8443/");

        Assert.Equal("https://idp.example.test:8443", urls.IdpPublicBase);
        Assert.Equal("https://api.example.test:7111", urls.PublicBase);
        Assert.Equal("https://idp.example.test:8443/connect/logout?x=1",
            urls.ToPublic("https://localhost:5001/connect/logout?x=1"));
    }

    [Theory]
    [InlineData("https://api.example.test:7111", true)]
    [InlineData("https://idp.example.test:8443", true)] // the IdP's own login form posts from here
    [InlineData("https://evil.example", false)]
    public void A_split_idp_origin_is_trusted_for_its_own_pages(string origin, bool expected) =>
        Assert.Equal(expected, new BffUrls("https://api.example.test:7111", [], "https://idp.example.test:8443")
            .IsTrustedOrigin(origin));

    [Fact]
    public void An_idp_address_that_is_not_a_url_fails_fast() =>
        Assert.Throws<InvalidOperationException>(() => new BffUrls("https://api.example.test", [], "not a url"));

    [Theory]
    [InlineData("/hangfire")]
    [InlineData("/hangfire/recurring?x=1")]
    [InlineData("/")]
    public void A_path_on_the_apis_own_origin_is_kept(string path) =>
        Assert.Equal(path, Create().ResolveReturnUrl(path));

    private static Microsoft.AspNetCore.Http.HttpRequest Request(string host, string? forwardedHost = null)
    {
        var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        context.Request.Host = Microsoft.AspNetCore.Http.HostString.FromUriComponent(host);
        if (forwardedHost is not null) context.Request.Headers["X-Forwarded-Host"] = forwardedHost;
        return context.Request;
    }

    [Theory]
    [InlineData("api.example.test:7111", true)]
    [InlineData("API.example.test:7111", true)]
    [InlineData("api.example.test", false)] // default port 443 is not 7111
    [InlineData("api.example.test:7112", false)]
    [InlineData("other.example.test:7111", false)]
    [InlineData("localhost:7111", false)]
    public void The_request_host_must_be_the_one_in_app_base_url(string host, bool expected) =>
        Assert.Equal(expected, Create().RequestMatchesPublicHost(Request(host)));

    [Fact]
    public void A_default_port_may_be_written_or_omitted() =>
        Assert.True(new BffUrls("https://api.example.test", []).RequestMatchesPublicHost(Request("api.example.test:443")));

    [Fact]
    public void A_proxys_forwarded_host_stands_in_for_the_host_it_replaced()
    {
        Assert.True(Create().RequestMatchesPublicHost(Request("node-1:5001", "api.example.test:7111")));
        Assert.False(Create().RequestMatchesPublicHost(Request("api.example.test:7111", "evil.example")));
    }
}
