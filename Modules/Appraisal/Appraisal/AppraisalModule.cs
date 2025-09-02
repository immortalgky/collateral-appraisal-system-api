namespace Appraisal;

public static class AppraisalModule
{
    public static IServiceCollection AddAppraisalModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAppraisalRepository, AppraisalRepository>();

        services.AddScoped<IAppraisalService, AppraisalService>();

        services.AddDbContext<AppraisalDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(AppraisalDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "appraisal");
            });
        });

        return services;
    }

    public static IApplicationBuilder UseAppraisalModule(this IApplicationBuilder app)
    {
        app.UseMigration<AppraisalDbContext>();
        return app;
    }
}