using System.Reflection;
using Workflow;
using Auth;
using Document;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Notification;
using Request;
using Appraisal;
using Collateral;
using Common;
using Parameter;
using Integration.Infrastructure;
using Shared.Messaging.Services;

namespace Integration.WebApplicationFactories;

public static class WebApplicationFactoryHelper
{
    internal static void ConfigureWebHost(
        IWebHostBuilder builder,
        string mssqlConnectionString,
        string rabbitMqConnectionString,
        Action<IServiceCollection> configureServicesAction
    )
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Development);
        builder.UseEnvironment(Environments.Development);

        builder.ConfigureAppConfiguration(
            (context, configBuilder) =>
            {
                configBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:DefaultConnection"] = mssqlConnectionString,
                        ["ConnectionStrings:Database"] = mssqlConnectionString,
                        ["RabbitMq:Host"] = rabbitMqConnectionString,
                        ["RabbitMq:Username"] = "testuser",
                        ["RabbitMq:Password"] = "testpw",
                        // appsettings.Development.json turns the failed-messages collector on; the
                        // shared test host must not poll a broker/management API that isn't there for
                        // every other test. The collector e2e test constructs its own instance instead.
                        ["FailedMessages:Enabled"] = "false",
                    }
                );
            }
        );

        // ConfigureAppConfiguration (above) is applied too late for IntegrationModule's registration-time
        // `FailedMessages:Enabled` check, so the collector was still registered and polling every 15 s in the
        // shared test host — retrying any RetryRequested row seeded for this node mid-test (flaky under load).
        // UseSetting is visible to builder.Configuration during Program's service registration.
        builder.UseSetting("FailedMessages:Enabled", "false");

        // The real outbox delivery loops (one per module DbContext) poll the same tables the tests seed, so
        // they can claim a seeded Pending row or re-process a just-reset orphan between a test's write and its
        // assertion. No test relies on real delivery, so the shared host does not run them.
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services
                         .Where(d => d.ServiceType == typeof(IHostedService)
                                     && d.ImplementationType is { IsGenericType: true } type
                                     && type.GetGenericTypeDefinition() == typeof(IntegrationEventDeliveryService<>))
                         .ToList())
                services.Remove(descriptor);
        });

        builder.ConfigureServices(configureServicesAction.Invoke);
    }

    internal static void ConfigureBuilderServices(
        IServiceCollection services,
        string mssqlConnectionString
    )
    {
        ReplaceAllDbContextConnection(services, mssqlConnectionString);
    }

    internal static void ReplaceAllDbContextConnection(
        IServiceCollection services,
        string mssqlConnectionString
    )
    {
        var dbContexts = GetAllDbContexts();
        var replaceMethod = typeof(WebApplicationFactoryHelper).GetMethod(
            "ReplaceDbContextConnection",
            BindingFlags.Static | BindingFlags.NonPublic
        )!;
        foreach (var dbContext in dbContexts)
        {
            var genericMethod = replaceMethod.MakeGenericMethod(dbContext);
            genericMethod.Invoke(null, [services, mssqlConnectionString]);
        }
    }

    private static void ReplaceDbContextConnection<T>(
        IServiceCollection services,
        string mssqlConnectionString
    )
        where T : DbContext
    {
        var descriptor = services.SingleOrDefault(d =>
            d.ServiceType == typeof(DbContextOptions<T>)
        );
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        services.AddDbContext<T>(options => options.UseSqlServer(mssqlConnectionString));
    }

    private static List<Type> GetAllDbContexts()
    {
        var requestAssembly = typeof(RequestModule).Assembly;
        var authAssembly = typeof(AuthModule).Assembly;
        var notificationAssembly = typeof(NotificationModule).Assembly;
        var documentAssembly = typeof(DocumentModule).Assembly;
        var workflowAssembly = typeof(WorkflowModule).Assembly;
        var appraisalAssembly = typeof(AppraisalModule).Assembly;
        var collateralAssembly = typeof(CollateralModule).Assembly;
        var commonAssembly = typeof(CommonModule).Assembly;
        var parameterAssembly = typeof(ParameterModule).Assembly;
        var integrationAssembly = typeof(IntegrationDbContext).Assembly;

        var dbContexts = GetDbContextsFromAssemblies(
            requestAssembly,
            authAssembly,
            notificationAssembly,
            documentAssembly,
            workflowAssembly,
            appraisalAssembly,
            collateralAssembly,
            commonAssembly,
            parameterAssembly,
            integrationAssembly
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
}
