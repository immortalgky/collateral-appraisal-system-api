using Microsoft.AspNetCore.Identity;
using Auth.Infrastructure.Session;

namespace Auth.Tests.Session;

public class LoginHintTests
{
    private static readonly string Allowed = new IdentityOptions().User.AllowedUserNameCharacters;

    [Theory]
    [InlineData("int.staff1", "int.staff1")]
    [InlineData("  a_b-c@d.e  ", "a_b-c@d.e")]
    public void A_plausible_username_passes_trimmed(string input, string expected) =>
        Assert.Equal(expected, LoginHint.Sanitize(input, Allowed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<script>")]
    [InlineData("a b")]
    [InlineData("a\"b")]
    [InlineData("a,b")]
    public void Anything_else_is_dropped(string? input) => Assert.Null(LoginHint.Sanitize(input, Allowed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void No_configured_charset_means_any_character_but_never_a_control_one(string? allowed)
    {
        Assert.Equal("DOM\\user x", LoginHint.Sanitize("DOM\\user x", allowed));
        Assert.Null(LoginHint.Sanitize("a\nb", allowed));
        Assert.Null(LoginHint.Sanitize(new string('a', 257), allowed));
        Assert.Null(LoginHint.FromAuthorizeUrl("/connect/authorize?login_hint=a%00b", allowed));
    }

    [Fact]
    public void Length_is_capped_at_256()
    {
        Assert.NotNull(LoginHint.Sanitize(new string('a', 256), Allowed));
        Assert.Null(LoginHint.Sanitize(new string('a', 257), Allowed));
    }

    [Theory]
    [InlineData("/connect/authorize?client_id=spa&login_hint=int.staff1&state=x", "int.staff1")]
    [InlineData("/connect/authorize?login_hint=%3Cscript%3E", null)]
    [InlineData("/connect/authorize?login_hint=a&login_hint=b", null)]
    [InlineData("/connect/authorize?client_id=spa", null)]
    [InlineData("/connect/authorize", null)]
    [InlineData(null, null)]
    public void The_hint_is_read_from_the_authorize_url(string? url, string? expected) =>
        Assert.Equal(expected, LoginHint.FromAuthorizeUrl(url, Allowed));
}
