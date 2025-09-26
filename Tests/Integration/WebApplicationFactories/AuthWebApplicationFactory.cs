using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;

namespace Integration.WebApplicationFactories;

public class AuthWebApplicationFactory(
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
                services
                    .AddOpenIddict()
                    .AddServer(options =>
                    {
                        options.UseAspNetCore().DisableTransportSecurityRequirement();
                        options.RegisterScopes(
                            OpenIddictConstants.Scopes.OpenId,
                            OpenIddictConstants.Scopes.Profile,
                            OpenIddictConstants.Scopes.Email,
                            OpenIddictConstants.Scopes.OfflineAccess
                        );
                    });
            }
        );
    }
}
