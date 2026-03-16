using Parameter.DocumentRequirements.Models;

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

        // Document Requirements
        services.AddScoped<IDocumentRequirementRepository, DocumentRequirementRepository>();

        // Unit of Work
        services.AddScoped<IParameterUnitOfWork, ParameterUnitOfWork>();

        // Document Checklist Service (cross-module contract)
        services.AddScoped<Parameter.Contracts.DocumentRequirements.IDocumentChecklistService,
            DocumentRequirements.Services.DocumentChecklistService>();

        services.AddDbContext<ParameterDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"));
        });

        services.AddScoped<IDataSeeder<ParameterDbContext>, ParameterDataSeed>();
        services.AddScoped<IDataSeeder<ParameterDbContext>, DocumentRequirementDataSeed>();

        return services;
    }

    public static IApplicationBuilder UseParameterModule(this IApplicationBuilder app)
    {
        app.UseMigration<ParameterDbContext>();

        return app;
    }
}