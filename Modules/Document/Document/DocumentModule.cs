using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Document.Data;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Document.Documents.Services;
using Document.Contracts.Documents.Services;
using System.Runtime.InteropServices;
using Document.Services;

namespace Document;

public static class DocumentModule
{
    public static IServiceCollection AddDocumentModule(this IServiceCollection services, IConfiguration configuration)
    {
        MappingConfiguration.ConfigureMappings();

        services.AddScoped<IDocumentRepository, DocumentRepository>();

        // Register document domain services
        services.AddScoped<IDocumentDomainService, DocumentDomainService>();
        services.AddScoped<IDocumentValidationService, DocumentValidationService>();
        services.AddScoped<IDocumentService, DocumentService>();

        // Register document security service based on platform
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddScoped<IDocumentSecurityService, WindowsDefenderDocumentSecurityService>();
        }
        else
        {
            services.AddScoped<IDocumentSecurityService, ClamAvDocumentSecurityService>();
        }

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventInterceptor>();

        services.AddDbContext<DocumentDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(DocumentDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "document");
            });
        });


        return services;
    }

    public static IApplicationBuilder UseDocumentModule(this IApplicationBuilder app)
    {
        app.UseMigration<DocumentDbContext>();
        return app;
    }
}