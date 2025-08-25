namespace Collateral;

public static class CollateralModule
{
    public static IServiceCollection AddCollateralModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ICollateralRepository, CollateralRepository>();
        services.AddDbContext<CollateralDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(CollateralDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "collateral");
            });
        });

        return services;
    }

    public static IApplicationBuilder UseCollateralModule(this IApplicationBuilder app)
    {
        app.UseMigration<CollateralDbContext>();

        return app;
    }
}