using Integration.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.WebApplicationFactories;

/// <summary>
/// Same host as <see cref="IntegrationTestWebApplicationFactory"/>, but the "Test" scheme carries no
/// permission claims at all — for the one test that needs to see the FAILED_MESSAGE_VIEW/MANAGE
/// policies actually deny someone (403), rather than only ever exercising the granted path.
/// </summary>
public class NoFailedMessagePermissionWebApplicationFactory(
    string mssqlConnectionString,
    string rabbitMqConnectionString
) : IntegrationTestWebApplicationFactory(mssqlConnectionString, rabbitMqConnectionString)
{
    protected override void ConfigureAuthServices(IServiceCollection services)
    {
        services
            .AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, NoPermissionAuthenticationHandler>("Test", _ => { });
        services.AddAuthorization();
        services.Configure<AuthenticationOptions>(options =>
        {
            options.DefaultAuthenticateScheme = "Test";
            options.DefaultChallengeScheme = "Test";
        });
    }
}
