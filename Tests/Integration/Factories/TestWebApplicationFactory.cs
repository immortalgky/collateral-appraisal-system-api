using Document.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Request.Data;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

namespace Integration.Factories;

public sealed class TestWebApplicationFactory(MsSqlContainer mssql, RabbitMqContainer rabbitMq) : WebApplicationFactory<Program>
{
    private readonly MsSqlContainer _mssql = mssql;
    private readonly RabbitMqContainer _rabbitMq = rabbitMq;
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Development);
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _mssql.GetConnectionString(),
                ["RabbitMq:Host"] = $"amqp://{_rabbitMq.Hostname}:{_rabbitMq.GetMappedPublicPort(5672)}/",
                ["RabbitMq:Username"] = "testuser",
                ["RabbitMq:Password"] = "testpw"
            });
        });

        builder.ConfigureServices(services =>
        {
            ReplaceDbContextConnection<RequestDbContext>(services);
            ReplaceDbContextConnection<DocumentDbContext>(services);
        });
    }

    private void ReplaceDbContextConnection<T>(IServiceCollection services) where T : DbContext
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<T>));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }
        services.AddDbContext<T>(options => options.UseSqlServer(_mssql.GetConnectionString()));

        var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<T>();
        db.Database.Migrate();
    }
}