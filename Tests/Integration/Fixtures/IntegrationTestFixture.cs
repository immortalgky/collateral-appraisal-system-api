using System.Reflection;
using Assignment;
using Auth;
using Database.Migration;
using Document;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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
    public MsSqlContainer Mssql { get; } = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString => Mssql.GetConnectionString();
    public RabbitMqContainer RabbitMq { get; } = new RabbitMqBuilder()
        .WithImage("rabbitmq:3-management")
        .WithEnvironment("RABBITMQ_DEFAULT_USER", "testuser")
        .WithEnvironment("RABBITMQ_DEFAULT_PASS", "testpw")
        .WithPortBinding(5672, true)
        .Build();

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        await Mssql.StartAsync();
        await RabbitMq.StartAsync();
        await ApplyAllMigrationsAsync();
        await ApplyDatabaseModuleAsync();
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await Mssql.DisposeAsync();
        await RabbitMq.DisposeAsync();
    }

    private async Task ApplyAllMigrationsAsync()
    {
        var dbContexts = GetAllDbContexts();
        var migrateMethod = GetType().GetMethod("MigrateAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        foreach (var dbContext in dbContexts)
        {
            var genericMethod = migrateMethod.MakeGenericMethod(dbContext);
            Task task = (Task)genericMethod.Invoke(this, null)!;
            await task;
        }
    }

    private async Task MigrateAsync<TContext>() where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseSqlServer(Mssql.GetConnectionString())
            .Options;

        using var context = (TContext)Activator.CreateInstance(typeof(TContext), options)!;
        await context.Database.MigrateAsync();
    }

    private static List<Type> GetAllDbContexts()
    {
        var requestAssembly = typeof(RequestModule).Assembly;
        var authAssembly = typeof(AuthModule).Assembly;
        var notificationAssembly = typeof(NotificationModule).Assembly;
        var documentAssembly = typeof(DocumentModule).Assembly;
        var assignmentAssembly = typeof(AssignmentModule).Assembly;

        var dbContexts = GetDbContextsFromAssemblies(requestAssembly, authAssembly, notificationAssembly, documentAssembly, assignmentAssembly);
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

    private async Task ApplyDatabaseModuleAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", Mssql.GetConnectionString() }
            })
            .Build();

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var databaseLogger = loggerFactory.CreateLogger<DatabaseMigrator>();
        var databaseMigrator = new DatabaseMigrator(configuration, databaseLogger);
        var migrationLogger = loggerFactory.CreateLogger<MigrationService>();

        var service = new MigrationService(databaseMigrator, configuration, migrationLogger);
        await service.MigrateAsync();
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Development);
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = Mssql.GetConnectionString(),
                ["ConnectionStrings:Database"] = Mssql.GetConnectionString(),
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
        var replaceMethod = GetType().GetMethod("ReplaceDbContextConnection", BindingFlags.Instance | BindingFlags.NonPublic)!;
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
        services.AddDbContext<T>(options => options.UseSqlServer(Mssql.GetConnectionString()));
    }
}