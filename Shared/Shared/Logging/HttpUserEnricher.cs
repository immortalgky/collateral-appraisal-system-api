using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;
using Shared.Identity;

namespace Shared.Logging;

/// <summary>
/// Adds a UserName property to every log event written while an HTTP request is in flight. Uses the
/// same claim chain as CurrentUserService.UserCode (see ClaimsPrincipalExtensions.GetUserCode), then
/// falls back further to "client_id" for machine clients (LOS/CLS) whose token has no user claims at
/// all. Runs at write time, so it covers both HttpLoggingMiddleware's own log line and any
/// exception-handler log for the same request.
/// </summary>
public class HttpUserEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var user = httpContextAccessor.HttpContext?.User;
        var userName = user.GetUserCode() ?? user?.FindFirst("client_id")?.Value;
        if (string.IsNullOrEmpty(userName)) return;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("UserName", userName));
    }
}
