using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using Shared.Logging;

namespace Shared.Tests.Logging;

public class HttpUserEnricherTests
{
    private static readonly FakePropertyFactory PropertyFactory = new();

    [Fact]
    public void Enrich_NameClaimPresent_UsesName()
    {
        var logEvent = Enrich(new Claim("name", "P5229"), new Claim(ClaimTypes.Name, "ignored"));

        ((ScalarValue)logEvent.Properties["UserName"]).Value.Should().Be("P5229");
    }

    // Same fallback order as CurrentUserService.UserCode — see that property's own comment.
    [Fact]
    public void Enrich_NoNameClaim_FallsBackToClaimTypesName()
    {
        var logEvent = Enrich(new Claim(ClaimTypes.Name, "P5229"), new Claim("preferred_username", "ignored"));

        ((ScalarValue)logEvent.Properties["UserName"]).Value.Should().Be("P5229");
    }

    [Fact]
    public void Enrich_OnlyPreferredUsernameClaim_FallsBackToPreferredUsername()
    {
        var logEvent = Enrich(new Claim("preferred_username", "P5229"));

        ((ScalarValue)logEvent.Properties["UserName"]).Value.Should().Be("P5229");
    }

    // Machine clients (LOS/CLS) authenticate with client-credentials tokens that carry none of the
    // user claims above, only client_id.
    [Fact]
    public void Enrich_NoUserClaims_FallsBackToClientId()
    {
        var logEvent = Enrich(new Claim("client_id", "los-client"));

        ((ScalarValue)logEvent.Properties["UserName"]).Value.Should().Be("los-client");
    }

    [Fact]
    public void Enrich_NoClaimsAtAll_DoesNotAddUserName()
    {
        var logEvent = Enrich();

        logEvent.Properties.Should().NotContainKey("UserName");
    }

    private static LogEvent Enrich(params Claim[] claims)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims)) };
        var enricher = new HttpUserEnricher(new FakeHttpContextAccessor(context));

        var template = new MessageTemplateParser().Parse("test");
        var logEvent = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Information, null, template, []);
        enricher.Enrich(logEvent, PropertyFactory);
        return logEvent;
    }

    private sealed class FakeHttpContextAccessor(HttpContext context) : IHttpContextAccessor
    {
        public HttpContext? HttpContext
        {
            get => context;
            set => throw new NotSupportedException();
        }
    }

    private sealed class FakePropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false) =>
            new(name, new ScalarValue(value));
    }
}
