using System.Reflection;
using Assignment;
using Auth;
using Database.Extensions;
using Database.Migration;
using Document;
using Integration.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Notification;
using Request;
using Testcontainers.MsSql;
using Testcontainers.RabbitMq;

namespace Integration.Fixtures;

public class IntegrationTestFixture : WebApplicationFactory<Program>, IAsyncLifetime
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

        builder.ConfigureAppConfiguration(
            (context, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                        ["ConnectionStrings:Database"] = ConnectionString,
                        ["RabbitMq:Host"] = RabbitMq.GetConnectionString(),
                        ["RabbitMq:Username"] = "testuser",
                        ["RabbitMq:Password"] = "testpw",
                    }
                );
            }
        );

        builder.ConfigureServices(ConfigureBuilderServices);
    }

    protected virtual void ConfigureBuilderServices(IServiceCollection services)
    {
        ReplaceAllDbContextConnection(services);
        ConfigureAuthServices(services);
    }

    protected void ReplaceAllDbContextConnection(IServiceCollection services)
    {
        var dbContexts = GetAllDbContexts();
        var replaceMethod = GetType()
            .GetMethod(
                "ReplaceDbContextConnection",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!;
        foreach (var dbContext in dbContexts)
        {
            var genericMethod = replaceMethod.MakeGenericMethod(dbContext);
            genericMethod.Invoke(this, [services]);
        }
    }

    private void ReplaceDbContextConnection<T>(IServiceCollection services)
        where T : DbContext
    {
        var descriptor = services.SingleOrDefault(d =>
            d.ServiceType == typeof(DbContextOptions<T>)
        );
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
        var assignmentAssembly = typeof(AssignmentModule).Assembly;

        var dbContexts = GetDbContextsFromAssemblies(
            requestAssembly,
            authAssembly,
            notificationAssembly,
            documentAssembly,
            assignmentAssembly
        );
        return dbContexts;
    }

    private static List<Type> GetDbContextsFromAssemblies(params Assembly[] assemblies)
    {
        var allDbContexts = new List<Type> { };
        foreach (var assembly in assemblies)
        {
            var dbContexts = assembly
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(DbContext)))
                .ToArray();
            allDbContexts.AddRange(dbContexts);
        }

        return allDbContexts;
    }

    protected static void ConfigureAuthServices(IServiceCollection services)
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
