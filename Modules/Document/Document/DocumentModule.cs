using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Document.Data;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Document;

public static class DocumentModule
{
    public static IServiceCollection AddDocumentModule(this IServiceCollection services, IConfiguration configuration)
    {
        MappingConfiguration.ConfigureMappings();
        
        services.AddScoped<IDocumentRepository, DocumentRepository>();

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventInterceptor>();

        services.AddDbContext<DocumentDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"));
        });


        return services;
    }
    public static IApplicationBuilder UseDocumentModule(this IApplicationBuilder app)
    {
        app.UseMigration<DocumentDbContext>();
        return app;
    }
}