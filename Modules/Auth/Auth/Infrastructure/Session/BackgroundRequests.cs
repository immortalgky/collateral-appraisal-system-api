using Microsoft.AspNetCore.Http;

namespace Auth.Infrastructure.Session;

/// <summary>
/// Requests a timer fires rather than a person: they must not keep a session alive. The SPA marks its own
/// polling with <c>X-Background-Request: 1</c>; the Hangfire dashboard's JavaScript cannot be given the header
/// and SignalR's transport traffic is not the SPA's own call, so those are recognised by path instead.
/// </summary>
public static class BackgroundRequests
{
    public static bool IsBackground(HttpRequest request)
    {
        if (request.Headers[AuthSchemes.BackgroundHeader].ToString() == "1")
            return true;

        // SignalR's transport traffic — negotiate, long-polling GET/POST, server-sent events — is the connection
        // keeping itself alive, not a person. (A WebSocket is one request at connect time.)
        if (request.Path.StartsWithSegments("/notificationHub")) return true;

        // The dashboard's realtime graph and counters POST to {dashboard}/stats on a timer. Its other jQuery
        // GETs (X-Requested-With: XMLHttpRequest, which the SPA never sends) are
        // refreshes of the page already open, not new work. Buttons are POSTs to other paths and count.
        if (!request.Path.StartsWithSegments("/hangfire")) return false;
        if (request.Path.StartsWithSegments("/hangfire/stats")) return true;
        return HttpMethods.IsGet(request.Method) && request.Headers["X-Requested-With"].ToString() == "XMLHttpRequest";
    }
}
