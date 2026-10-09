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

    /// <summary>
    /// Shared for the whole "Integration" collection, like the two factories above — never
    /// explicitly disposed (same as them). A [Theory] over several 403 cases that instead created and
    /// `await using`-disposed a FRESH WebApplicationFactory per case hit a real, intermittent
    /// NullReferenceException out of Reporting's PuppeteerBrowserPool.DisposeAsync (every
    /// WebApplicationFactory&lt;Program&gt; boots one); a single long-lived instance whose HttpClient is
    /// created fresh per call (CreateClient() reuses the same host) avoids that teardown path entirely.
    /// </summary>
    public ViewOnlyFailedMessagePermissionWebApplicationFactory ViewOnlyFailedMessagePermissionWebApplicationFactory
    {
        get;
        private set;
    } = default!;

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

        bool setupResult;
        try
        {
            setupResult = await testSetupService.SetupDatabaseAsync(ConnectionString);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Database setup threw: {ex.GetType().Name}: {ex.Message}\nInner: {ex.InnerException?.Message}", ex);
        }
        if (!setupResult)
        {
            throw new InvalidOperationException("Failed to setup database for integration tests (returned false)");
        }

        IntegrationTestWebApplicationFactory = new IntegrationTestWebApplicationFactory(
            ConnectionString,
            RabbitMq.GetConnectionString()
        );
        AuthWebApplicationFactory = new AuthWebApplicationFactory(
            ConnectionString,
            RabbitMq.GetConnectionString()
        );
        ViewOnlyFailedMessagePermissionWebApplicationFactory = new ViewOnlyFailedMessagePermissionWebApplicationFactory(
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
