using Integration.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Integration.WebApplicationFactories;

public class IntegrationTestWebApplicationFactory(
    string mssqlConnectionString,
    string rabbitMqConnectionString
) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        WebApplicationFactoryHelper.ConfigureWebHost(
            builder,
            mssqlConnectionString,
            rabbitMqConnectionString,
            services =>
            {
                WebApplicationFactoryHelper.ReplaceAllDbContextConnection(
                    services,
                    mssqlConnectionString
                );
                ConfigureAuthServices(services);
            }
        );
    }

    protected virtual void ConfigureAuthServices(IServiceCollection services)
    {
        services
            .AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, BypassAuthenticationHandler>(
                "Test",
                options => { }
            );
        services.AddAuthorization();
        services.Configure<AuthenticationOptions>(options =>
        {
            options.DefaultAuthenticateScheme = "Test";
            options.DefaultChallengeScheme = "Test";
        });
    }
}
