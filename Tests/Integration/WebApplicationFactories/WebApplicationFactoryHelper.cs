using System.Reflection;
using Assignment;
using Auth;
using Document;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Notification;
using Request;

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
                    }
                );
            }
        );

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
}
