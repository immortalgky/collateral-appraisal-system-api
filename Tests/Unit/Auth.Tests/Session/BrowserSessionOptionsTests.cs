using Auth.Infrastructure.Session;

namespace Auth.Tests.Session;

public class BrowserSessionOptionsTests
{
    [Theory]
    [InlineData("https://api.example.test:7112", "-7112")]
    [InlineData("https://api.example.test:7111/", "-7111")]
    [InlineData("https://api.example.test", "-443")]
    [InlineData("http://api.example.test", "-80")]
    public void With_no_suffix_configured_it_is_derived_from_the_app_base_url_port(string appBaseUrl, string expected)
    {
        var options = new BrowserSessionOptions().Resolve(appBaseUrl);

        Assert.Equal(expected, options.CookieSuffix);
        // Same rule for all three cookies.
        Assert.Equal($"__Host-cas-session{expected}", options.AppCookieName);
        Assert.Equal($"__Host-cas-idp{expected}", options.IdpCookieName);
        Assert.Equal($"cas-oidc-retry{expected}", options.RetryCookieName);
        Assert.Equal($"__Host-cas-2fa{expected}", options.TwoFactorUserCookieName);
        Assert.Equal($"__Host-cas-2fa-remember{expected}", options.TwoFactorRememberCookieName);
    }

    [Theory]
    [InlineData("-sit1")]
    [InlineData("-UAT-2")]
    public void A_configured_suffix_wins_over_the_port(string suffix)
    {
        var options = new BrowserSessionOptions { CookieSuffix = suffix }.Resolve("https://api.example.test:7112");

        Assert.Equal($"__Host-cas-session{suffix}", options.AppCookieName);
        Assert.Equal($"__Host-cas-idp{suffix}", options.IdpCookieName);
        Assert.Equal($"cas-oidc-retry{suffix}", options.RetryCookieName);
    }

    [Fact]
    public void Resolving_twice_changes_nothing()
    {
        var options = new BrowserSessionOptions().Resolve("https://api.example.test:7112");
        Assert.Equal("-7112", options.Resolve("https://api.example.test:7112").CookieSuffix);
    }

    [Theory]
    [InlineData("#{SESSION_COOKIE_SUFFIX}#")] // an unsubstituted deployment token, if someone puts it back
    [InlineData(" sit1")]
    [InlineData("-a;b")]
    public void Invalid_suffix_fails_at_startup_instead_of_producing_a_broken_cookie(string suffix) =>
        Assert.Throws<InvalidOperationException>(() =>
            new BrowserSessionOptions { CookieSuffix = suffix }.Resolve("https://api.example.test:7112"));

    [Fact]
    public void Idle_timeout_below_one_minute_is_rejected() =>
        Assert.Throws<InvalidOperationException>(() =>
            new BrowserSessionOptions { IdleTimeoutMinutes = 0 }.Resolve("https://api.example.test:7112"));
}
