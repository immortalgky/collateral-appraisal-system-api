using System.Reflection;
using Workflow;
using Auth;
using Database.Migration;
using Microsoft.AspNetCore.Mvc.Testing;
using Database.Extensions;
using Document;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Notification;
using Request;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Integration.Fixtures;

public class IntegrationTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public MsSqlContainer Mssql { get; } = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public RabbitMqContainer RabbitMq { get; } = new RabbitMqBuilder()
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
                InitialCatalog = "CollateralAppraisal"
            };
            return builder.ConnectionString;
        }
    }

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        await Mssql.StartAsync();
        await RabbitMq.StartAsync();

        // Use the Database project's setup service to handle all migrations
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", ConnectionString },
                { "ConnectionStrings:Database", ConnectionString }
            })
            .Build();

        // Create a host with the database migration services
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) => { services.AddDatabaseMigration(configuration); })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
            })
            .Build();

        using var scope = host.Services.CreateScope();
        var testSetupService = scope.ServiceProvider.GetRequiredService<IDatabaseTestSetupService>();

        var setupResult = await testSetupService.SetupDatabaseAsync(ConnectionString);
        if (!setupResult)
        {
            throw new InvalidOperationException("Failed to setup database for integration tests");
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await Mssql.DisposeAsync();
        await RabbitMq.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Development);
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                ["ConnectionStrings:Database"] = ConnectionString,
                ["RabbitMq:Host"] = RabbitMq.GetConnectionString(),
                ["RabbitMq:Username"] = "testuser",
                ["RabbitMq:Password"] = "testpw",
            });
        });

        builder.ConfigureServices(ReplaceAllDbContextConnection);
    }

    private void ReplaceAllDbContextConnection(IServiceCollection services)
    {
        var dbContexts = GetAllDbContexts();
        var replaceMethod =
            GetType().GetMethod("ReplaceDbContextConnection", BindingFlags.Instance | BindingFlags.NonPublic)!;
        foreach (var dbContext in dbContexts)
        {
            var genericMethod = replaceMethod.MakeGenericMethod(dbContext);
            genericMethod.Invoke(this, [services]);
        }
    }

    private void ReplaceDbContextConnection<T>(IServiceCollection services) where T : DbContext
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<T>));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        services.AddDbContext<T>(options => options.UseSqlServer(ConnectionString));
    }

    private static List<Type> GetAllDbContexts()
    {
        var requestAssembly = typeof(RequestModule).Assembly;
        var authAssembly = typeof(AuthModule).Assembly;
        var notificationAssembly = typeof(NotificationModule).Assembly;
        var documentAssembly = typeof(DocumentModule).Assembly;
        var workflowAssembly = typeof(WorkflowModule).Assembly;

        var dbContexts = GetDbContextsFromAssemblies(requestAssembly, authAssembly, notificationAssembly,
            documentAssembly, workflowAssembly);
        return dbContexts;
    }

    private static List<Type> GetDbContextsFromAssemblies(params Assembly[] assemblies)
    {
        var allDbContexts = new List<Type> { };
        foreach (var assembly in assemblies)
        {
            var dbContexts = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(DbContext)))
                .ToArray();
            allDbContexts.AddRange(dbContexts);
        }

        return allDbContexts;
    }
}