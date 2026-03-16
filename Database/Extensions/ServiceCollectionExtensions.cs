using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Request.Infrastructure;
using Workflow.Data;
using Document.Data;
using Notification.Data;
using Auth.Infrastructure;
using Shared.Data;
using Microsoft.AspNetCore.Identity;
using Auth;
using Auth.Domain.Identity;

namespace Database.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseMigration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register DbUp services
        services.AddSingleton<Migration.DatabaseMigrator>();
        services.AddSingleton<Migration.MigrationService>();
        services.AddSingleton<Migration.MigrationCoordinator>();

        // Register EF Core migration service
        services.AddScoped<Migration.IEfCoreMigrationService, Migration.EfCoreMigrationService>();

        // Register database test setup service
        services.AddScoped<Migration.IDatabaseTestSetupService, Migration.DatabaseTestSetupService>();

        // Register simplified migration service as the primary IMigrationService
        // This only handles views, stored procedures, and functions via DbUp
        services.AddScoped<Migration.IMigrationService>(provider =>
        {
            var dbUpService = provider.GetRequiredService<Migration.MigrationService>();
            var config = provider.GetRequiredService<IConfiguration>();
            var logger = provider.GetRequiredService<ILogger<Migration.SimplifiedMigrationService>>();

            return new Migration.SimplifiedMigrationService(
                dbUpService,
                config,
                logger);
        });

        // Register DbContexts for standalone operation
        var connectionString = configuration.GetConnectionString("Database")
                               ?? configuration["Environments:Development:ConnectionString"];

        services.AddDbContext<RequestDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(RequestDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "request");
            });
        });

        services.AddDbContext<WorkflowDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(WorkflowDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "workflow");
            });
        });

        services.AddDbContext<DocumentDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(DocumentDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "document");
            });
        });

        services.AddDbContext<NotificationDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(NotificationDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "notification");
            });
        });

        // Register minimal Identity and OpenIddict core services for DbContext support
        services.AddIdentity<ApplicationUser, ApplicationRole>()
            .AddEntityFrameworkStores<AuthDbContext>();

        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<AuthDbContext>();
            });

        services.AddDbContext<AuthDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(AuthDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "auth");
            });
            options.UseOpenIddict();
        });

        return services;
    }
}