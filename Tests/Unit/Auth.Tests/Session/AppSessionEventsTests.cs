using Auth.Infrastructure.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Auth.Tests.Session;

public class AppSessionEventsTests
{
    private const string Name = "__Host-cas-session";

    [Fact]
    public void Only_the_app_cookie_is_dropped_and_every_other_value_keeps_its_exact_encoding()
    {
        var header = $"a=b%3Dc%3B; {Name}=CfDJ8%2Babc%3D%3D; other=%20x%2Fy; plain=1";

        var result = AppSessionEvents.WithoutCookie(header, Name);

        // Decoded-and-rebuilt would have turned %3B into ';' and broken the header.
        Assert.Equal("a=b%3Dc%3B; other=%20x%2Fy; plain=1", result.ToString());
    }

    [Fact]
    public void Chunks_of_the_cookie_go_too_but_lookalike_names_stay()
    {
        var header = $"{Name}C1=x; {Name}C2=y; {Name}C=z; {Name}-other=keep; {Name}Cx=keep; {Name}=main";

        var result = AppSessionEvents.WithoutCookie(header, Name);

        Assert.Equal($"{Name}C=z; {Name}-other=keep; {Name}Cx=keep", result.ToString());
    }

    [Fact]
    public void Several_cookie_header_lines_are_all_handled()
    {
        var result = AppSessionEvents.WithoutCookie(new StringValues([$"a=1; {Name}=x", $"{Name}C1=y; b=2"]), Name);
        Assert.Equal("a=1; b=2", result.ToString());
    }

    [Fact]
    public void A_header_holding_only_the_app_cookie_becomes_empty()
    {
        Assert.True(StringValues.IsNullOrEmpty(AppSessionEvents.WithoutCookie($"{Name}=x", Name)));
        Assert.True(StringValues.IsNullOrEmpty(AppSessionEvents.WithoutCookie(StringValues.Empty, Name)));
    }

    [Fact]
    public void A_value_with_an_equals_sign_inside_is_not_mistaken_for_a_cookie_name()
    {
        var result = AppSessionEvents.WithoutCookie($"tricky=v={Name}=x; {Name}=y", Name);
        Assert.Equal($"tricky=v={Name}=x", result.ToString());
    }

    private static HttpRequest Req(string method, params (string Name, string Value)[] headers)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        foreach (var (name, value) in headers) context.Request.Headers[name] = value;
        return context.Request;
    }

    [Theory]
    [InlineData("navigate", "document")]
    [InlineData("navigate", "iframe")]
    public void A_page_or_iframe_load_is_a_navigation(string mode, string dest) =>
        Assert.True(AppSessionEvents.IsDocumentNavigation(Req("GET", ("Sec-Fetch-Mode", mode), ("Sec-Fetch-Dest", dest))));

    [Theory]
    [InlineData("cors", "empty")] // fetch / XHR
    [InlineData("same-origin", "empty")] // jQuery XHR to the same origin
    [InlineData("no-cors", "image")]
    public void Script_requests_are_not_navigations(string mode, string dest) =>
        Assert.False(AppSessionEvents.IsDocumentNavigation(Req("POST", ("Sec-Fetch-Mode", mode), ("Sec-Fetch-Dest", dest))));

    [Fact]
    public void Without_fetch_metadata_a_get_asking_for_html_is_a_navigation() =>
        Assert.True(AppSessionEvents.IsDocumentNavigation(Req("GET", ("Accept", "text/html,application/xhtml+xml"))));

    [Fact]
    public void Without_fetch_metadata_a_jquery_xhr_is_not() =>
        Assert.False(AppSessionEvents.IsDocumentNavigation(
            Req("GET", ("Accept", "text/html, */*; q=0.01"), ("X-Requested-With", "XMLHttpRequest"))));

    [Fact]
    public void Without_fetch_metadata_a_post_is_not() =>
        Assert.False(AppSessionEvents.IsDocumentNavigation(Req("POST", ("Accept", "text/html"))));
}
