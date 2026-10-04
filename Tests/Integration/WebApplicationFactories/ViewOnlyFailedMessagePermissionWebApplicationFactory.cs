using Integration.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.WebApplicationFactories;

/// <summary>
/// Same host as <see cref="IntegrationTestWebApplicationFactory"/>, but the "Test" scheme carries only
/// FAILED_MESSAGE_VIEW — for the one test that proves a write endpoint denies a viewer (403), not just
/// someone with no permissions at all.
/// </summary>
public class ViewOnlyFailedMessagePermissionWebApplicationFactory(
    string mssqlConnectionString,
    string rabbitMqConnectionString
) : IntegrationTestWebApplicationFactory(mssqlConnectionString, rabbitMqConnectionString)
{
    protected override void ConfigureAuthServices(IServiceCollection services)
    {
        services
            .AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, ViewOnlyFailedMessagePermissionAuthenticationHandler>(
                "Test", _ => { });
        services.AddAuthorization();
        services.Configure<AuthenticationOptions>(options =>
        {
            options.DefaultAuthenticateScheme = "Test";
            options.DefaultChallengeScheme = "Test";
        });
    }
}
