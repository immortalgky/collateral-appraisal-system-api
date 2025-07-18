namespace Parameter;

public static class ParameterModule
{
    public static IServiceCollection AddParameterModule(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IParameterRepository, ParameterRepository>();

        services.AddDbContext<ParameterDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"));
        });

        return services;
    }

    public static IApplicationBuilder UseParameterModule(this IApplicationBuilder app)
    {
        app.UseMigration<ParameterDbContext>();

        return app;
    }
}