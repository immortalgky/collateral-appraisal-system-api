using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Integration.Fixtures;

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
            }
        );
    }
}
