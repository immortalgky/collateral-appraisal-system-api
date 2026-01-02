using Database.Extensions;
using Database.Migration;
using Integration.WebApplicationFactories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

namespace Integration.Fixtures;

public class IntegrationTestFixture : IAsyncLifetime
{
    public MsSqlContainer Mssql { get; } =
        new MsSqlBuilder().WithImage("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public RabbitMqContainer RabbitMq { get; } =
        new RabbitMqBuilder()
            .WithImage("rabbitmq:3-management")
            .WithEnvironment("RABBITMQ_DEFAULT_USER", "testuser")
            .WithEnvironment("RABBITMQ_DEFAULT_PASS", "testpw")
            .WithPortBinding(5672, true)
            .Build();

    public string ConnectionString
    {
        get
        {
            var baseConnectionString = Mssql.GetConnectionString();
            var builder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                InitialCatalog = "CollateralAppraisal",
            };
            return builder.ConnectionString;
        }
    }
    public IntegrationTestWebApplicationFactory IntegrationTestWebApplicationFactory
    {
        get;
        private set;
    } = default!;
    
    public AuthWebApplicationFactory AuthWebApplicationFactory { get; private set; } = default!;

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        await Mssql.StartAsync();
        await RabbitMq.StartAsync();

        // Use the Database project's setup service to handle all migrations
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    { "ConnectionStrings:DefaultConnection", ConnectionString },
                    { "ConnectionStrings:Database", ConnectionString },
                }
            )
            .Build();

        // Create a host with the database migration services
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices(
                (context, services) =>
                {
                    services.AddDatabaseMigration(configuration);
                }
            )
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        using var scope = host.Services.CreateScope();
        var testSetupService =
            scope.ServiceProvider.GetRequiredService<IDatabaseTestSetupService>();

        var setupResult = await testSetupService.SetupDatabaseAsync(ConnectionString);
        if (!setupResult)
        {
            throw new InvalidOperationException("Failed to setup database for integration tests");
        }

        IntegrationTestWebApplicationFactory = new IntegrationTestWebApplicationFactory(
            ConnectionString,
            RabbitMq.GetConnectionString()
        );
        AuthWebApplicationFactory = new AuthWebApplicationFactory(
            ConnectionString,
            RabbitMq.GetConnectionString()
        );
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await Mssql.DisposeAsync();
        await RabbitMq.DisposeAsync();
    }
}
