using Shared.Data.Interceptors;

namespace Collateral;

public static class CollateralModule
{
    public static IServiceCollection AddCollateralModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventInterceptor>();

        services.AddDbContext<CollateralDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(CollateralDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "collateral");
            });
        });

        services.AddScoped<ICollateralMasterRepository, CollateralMasterRepository>();
        services.AddScoped<ICollateralMasterUpsertService, CollateralMasterUpsertService>();

        // Singleton: in-memory job state must survive across requests
        services.AddSingleton<CollateralBackfillJob>();

        return services;
    }

    public static IApplicationBuilder UseCollateralModule(this IApplicationBuilder app)
    {
        app.UseMigration<CollateralDbContext>();

        return app;
    }
}
