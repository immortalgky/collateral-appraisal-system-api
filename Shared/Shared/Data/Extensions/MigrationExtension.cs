using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Data.Seed;

namespace Shared.Data.Extensions;

public static class MigrationExtension
{
    /// <summary>
    /// Asserts the database schema is current, then runs the module's data seeders.
    ///
    /// The app deliberately does NOT apply migrations. Schema is owned by the DBA and applied
    /// out-of-band from the generated SQL bundle (see deploy/README.md) — running two IIS nodes
    /// that each call <c>Database.MigrateAsync()</c> on boot would race the same DDL, and the
    /// module order here does not match the dependency order in EfCoreMigrationService.
    ///
    /// The check below is read-only: it applies nothing, it just refuses to start a node whose
    /// schema is behind the code, so seeders never run against a stale database.
    /// </summary>
    public static IApplicationBuilder UseDataSeeding<TContext>(this IApplicationBuilder app)
        where TContext : DbContext
    {
        EnsureSchemaCurrentAsync<TContext>(app.ApplicationServices).GetAwaiter().GetResult();
        SeedDatabaseAsync<TContext>(app.ApplicationServices).GetAwaiter().GetResult();
        return app;
    }

    private static async Task EnsureSchemaCurrentAsync<TContext>(IServiceProvider serviceProvider)
        where TContext : DbContext
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(MigrationExtension));

        var pending = (await dbContext.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0) return;

        logger.LogCritical(
            "{Context}: {Count} pending migration(s). Apply the database deployment bundle before starting the app. First pending: {First}",
            typeof(TContext).Name, pending.Count, pending[0]);

        throw new InvalidOperationException(
            $"{typeof(TContext).Name} has {pending.Count} pending migration(s); the database schema is behind this build. " +
            "Apply the database deployment bundle (see deploy/README.md) before starting the app.");
    }

    private static async Task SeedDatabaseAsync<TContext>(IServiceProvider serviceProvider) where TContext : DbContext
    {
        using var scope = serviceProvider.CreateScope();
        var seeders = scope.ServiceProvider.GetServices<IDataSeeder<TContext>>();
        foreach (var seeder in seeders) await seeder.SeedAllAsync();
    }
}
