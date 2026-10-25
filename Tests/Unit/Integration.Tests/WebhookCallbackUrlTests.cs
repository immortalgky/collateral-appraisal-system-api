using FluentAssertions;
using Integration.Application.Features.WebhookDeliveries;

namespace Integration.Tests;

public class WebhookCallbackUrlTests
{
    [Theory]
    [InlineData("https://user:pass@example.com/hook", "https://example.com/hook")]
    [InlineData("https://example.com/hook?token=abc&x=1", "https://example.com/hook")]
    [InlineData("https://example.com/hook#frag", "https://example.com/hook")]
    [InlineData("https://user:pw@example.com:8443/a/b?k=v#f", "https://example.com:8443/a/b")]
    [InlineData("http://example.com:80/hook", "http://example.com/hook")]
    [InlineData("https://example.com/api", "https://example.com/api")]
    [InlineData("https://example.com", "https://example.com/")]
    public void ToDisplay_KeepsSchemeHostPortPathOnly(string url, string expected) =>
        WebhookCallbackUrl.ToDisplay(url).Should().Be(expected);

    [Theory]
    [InlineData("not a url?secret=1")]
    [InlineData("/relative/path?k=v#f")]
    [InlineData("plain-value")]
    [InlineData("ftp://user:pass@host/x?k=v")]
    [InlineData("ht!tp://user:pass@host:99/x#f")]
    [InlineData("user:pass@host/x")]
    [InlineData("//user:pw@host/x")]
    [InlineData("https://user:p?ss@host/x")]
    [InlineData("https://user:p#ss@host/x")]
    [InlineData("ftp://user:p/ss@host/x")]
    [InlineData(@"https:\\user:pw@host\x")]
    [InlineData("")]
    public void ToDisplay_UnparseableOrNonHttp_ReturnsThePlaceholderAndNeverTheInput(string url)
    {
        var display = WebhookCallbackUrl.ToDisplay(url);

        display.Should().Be(WebhookCallbackUrl.InvalidPlaceholder);
        display.Should().NotContain("pw").And.NotContain("pass");
    }
}
