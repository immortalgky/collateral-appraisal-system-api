using Auth.Infrastructure.Session;
using Microsoft.AspNetCore.Http;

namespace Auth.Tests.Session;

public class BackgroundRequestsTests
{
    private static HttpRequest Request(string method, string path, params (string Name, string Value)[] headers)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        foreach (var (name, value) in headers) context.Request.Headers[name] = value;
        return context.Request;
    }

    [Fact]
    public void The_spa_marks_its_own_polling_with_the_header() =>
        Assert.True(BackgroundRequests.IsBackground(
            Request("GET", "/requests", (AuthSchemes.BackgroundHeader, "1"))));

    [Fact]
    public void A_header_value_other_than_1_does_not_count() =>
        Assert.False(BackgroundRequests.IsBackground(
            Request("GET", "/requests", (AuthSchemes.BackgroundHeader, "0"))));

    [Theory]
    [InlineData("POST", "/hangfire/stats")] // what the dashboard's poller really sends
    [InlineData("POST", "/hangfire/stats/")]
    [InlineData("GET", "/hangfire/stats")]
    public void The_hangfire_dashboard_stats_poll_is_background(string method, string path) =>
        Assert.True(BackgroundRequests.IsBackground(Request(method, path)));

    [Fact]
    public void Hangfire_xhr_gets_are_page_refreshes_not_activity() =>
        Assert.True(BackgroundRequests.IsBackground(
            Request("GET", "/hangfire/jobs/enqueued", ("X-Requested-With", "XMLHttpRequest"))));

    [Theory]
    [InlineData("GET", "/hangfire")] // opening the dashboard is a person
    [InlineData("GET", "/hangfire/recurring")]
    [InlineData("POST", "/hangfire/jobs/actions/requeue")] // a button press
    public void Opening_pages_and_pressing_buttons_in_the_dashboard_is_activity(string method, string path) =>
        Assert.False(BackgroundRequests.IsBackground(Request(method, path)));

    [Fact]
    public void An_spa_xhr_is_activity_even_though_it_is_an_xhr() =>
        Assert.False(BackgroundRequests.IsBackground(
            Request("GET", "/requests", ("X-Requested-With", "XMLHttpRequest"))));

    [Fact]
    public void A_hangfire_lookalike_path_is_not_the_dashboard() =>
        Assert.False(BackgroundRequests.IsBackground(
            Request("GET", "/hangfirestuff/stats", ("X-Requested-With", "XMLHttpRequest"))));

    [Theory]
    [InlineData("POST", "/notificationHub/negotiate")]
    [InlineData("GET", "/notificationHub")] // long-polling / SSE / the websocket upgrade
    [InlineData("POST", "/notificationHub")] // long-polling send
    [InlineData("DELETE", "/notificationHub")]
    public void SignalR_transport_traffic_is_never_activity(string method, string path) =>
        Assert.True(BackgroundRequests.IsBackground(Request(method, path)));

    [Fact]
    public void A_notificationHub_lookalike_is_not_the_hub() =>
        Assert.False(BackgroundRequests.IsBackground(Request("POST", "/notificationHubs/x")));
}
