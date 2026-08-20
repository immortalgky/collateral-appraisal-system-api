using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Data.Seed;

namespace Shared.Data.Extensions;

public static class MigrationExtension
{
    /// <summary>
    /// Asserts the database schema is current, then — only where seeding is explicitly enabled —
    /// runs the module's data seeders.
    ///
    /// The app deliberately does NOT apply migrations. Schema is owned by the DBA and applied
    /// out-of-band from the generated SQL bundle (see deploy/README.md) — running two IIS nodes
    /// that each call <c>Database.MigrateAsync()</c> on boot would race the same DDL, and the
    /// module order here does not match the dependency order in EfCoreMigrationService.
    ///
    /// The check below is read-only: it applies nothing, it just refuses to start a node whose
    /// schema is behind the code.
    ///
    /// Data seeding is a fresh-install convenience, NOT a production mechanism. On a live database
    /// the whole-table-guarded seeders are already no-ops, while the per-key ones silently re-insert
    /// rows an admin deleted — undoing their work on every app-pool recycle. So reference data that
    /// code depends on ships as a one-off script in Database/Migration/Scripts/ (journaled once per
    /// database), and everything else belongs to the admin UI and is never rewritten from code.
    /// </summary>
    public static IApplicationBuilder UseDataSeeding<TContext>(this IApplicationBuilder app)
        where TContext : DbContext
    {
        EnsureSchemaCurrentAsync<TContext>(app.ApplicationServices).GetAwaiter().GetResult();

        // Fails closed: an unset SeedData:RunSeeders means NO seeding. A dropped config line or a
        // failed deployment transform therefore costs nothing on an environment that is already
        // seeded, instead of silently resuming the overwrites described above.
        var configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();
        if (!configuration.GetValue<bool>("SeedData:RunSeeders"))
        {
            app.ApplicationServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(MigrationExtension))
                .LogInformation(
                    "Data seeding is disabled (SeedData:RunSeeders); skipping seeders for {Context}.",
                    typeof(TContext).Name);
            return app;
        }

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
