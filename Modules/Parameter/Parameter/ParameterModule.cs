namespace Parameter;

public static class ParameterModule
{
    public static IServiceCollection AddParameterModule(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMemoryCache();

        services.AddScoped<IParameterRepository, ParameterRepository>();
        services.Decorate<IParameterRepository, CachedParameterRepository>();

        services.AddScoped<IAddressRepository, AddressRepository>();
        services.Decorate<IAddressRepository, CachedAddressRepository>();

        services.AddDbContext<ParameterDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"));
        });

        services.AddScoped<IDataSeeder<ParameterDbContext>, ParameterDataSeed>();

        return services;
    }

    public static IApplicationBuilder UseParameterModule(this IApplicationBuilder app)
    {
        app.UseMigration<ParameterDbContext>();

        return app;
    }
}
