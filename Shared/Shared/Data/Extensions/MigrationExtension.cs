using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data.Seed;

namespace Shared.Data.Extensions;

public static class MigrationExtension
{
    public static async Task<IApplicationBuilder> UseMigration<TContext>(this IApplicationBuilder app) where TContext : DbContext
    {
        using var context = Activator.CreateInstance<TContext>()!;
        var pending = await context.Database.GetPendingMigrationsAsync();

        if (pending.Any())
        {
            MigrateDatabaseAsync<TContext>(app.ApplicationServices).GetAwaiter().GetResult();
        }
        SeedDatabaseAsync<TContext>(app.ApplicationServices).GetAwaiter().GetResult();
        return app;
    }

    private static async Task MigrateDatabaseAsync<TContext>(IServiceProvider serviceProvider)
        where TContext : DbContext
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TContext>();
        await dbContext.Database.MigrateAsync();
    }

    private static async Task SeedDatabaseAsync<TContext>(IServiceProvider serviceProvider) where TContext : DbContext
    {
        using var scope = serviceProvider.CreateScope();
        var seeders = scope.ServiceProvider.GetServices<IDataSeeder<TContext>>();
        foreach (var seeder in seeders) await seeder.SeedAllAsync();
    }
}