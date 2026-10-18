using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Integration.WebApplicationFactories;

/// <summary>
/// A host whose authentication scheme never authenticates anyone, so endpoints can be tested for
/// returning 401. The shared fixture's bypass handler authenticates every request, so it cannot.
/// Reuses the fixture's container connection strings.
/// </summary>
public sealed class AnonymousWebApplicationFactory(
    string mssqlConnectionString,
    string rabbitMqConnectionString
) : IntegrationTestWebApplicationFactory(mssqlConnectionString, rabbitMqConnectionString)
{
    protected override void ConfigureAuthServices(IServiceCollection services)
    {
        services
            .AddAuthentication("Anonymous")
            .AddScheme<AuthenticationSchemeOptions, AnonymousAuthHandler>("Anonymous", _ => { });
        services.AddAuthorization();
        services.Configure<AuthenticationOptions>(options =>
        {
            options.DefaultAuthenticateScheme = "Anonymous";
            options.DefaultChallengeScheme = "Anonymous";
        });
    }

    private sealed class AnonymousAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder
    ) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            => Task.FromResult(AuthenticateResult.NoResult());
    }
}
