using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Assignment.Configurations;
using Assignment.Data.Repository;
using Shared.Data.Extensions;
using Shared.Data.Interceptors;

namespace Assignment;

public static class AssignmentModule
{
    public static IServiceCollection AddAssignmentModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure Mapster mappings
        MappingConfiguration.ConfigureMappings();

        // Application User Case services
        services.AddScoped<IAssignmentRepository, AssignmentRepository>();

        // Infrastructure services
        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventInterceptor>();

        services.AddDbContext<AssignmentDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"));
        });

        // services.AddScoped<IDataSeeder<RequestDbContext>, RequestDataSeed>();

        return services;
    }

    public static IApplicationBuilder UseAssignmentModule(this IApplicationBuilder app)
    {
        app.UseMigration<AssignmentDbContext>();

        return app;
    }
}