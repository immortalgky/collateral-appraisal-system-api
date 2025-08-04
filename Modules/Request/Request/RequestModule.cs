using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Request.Configurations;
using Shared.Data.Interceptors;

namespace Request;

public static class RequestModule
{
    public static IServiceCollection AddRequestModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure Mapster mappings
        MappingConfiguration.ConfigureMappings();

        // Application User Case services
        services.AddScoped<IRequestRepository, RequestRepository>();
        services.AddScoped<IRequestReadRepository, RequestReadRepository>();
        services.AddTransient<IAppraisalNumberGenerator, AppraisalNumberGenerator>();

        // Infrastructure services
        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventInterceptor>();

        services.AddDbContext<RequestDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(RequestDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "request");
            });
        });

        services.AddScoped<IDataSeeder<RequestDbContext>, RequestDataSeed>();

        return services;
    }

    public static IApplicationBuilder UseRequestModule(this IApplicationBuilder app)
    {
        app.UseMigration<RequestDbContext>();

        return app;
    }
}