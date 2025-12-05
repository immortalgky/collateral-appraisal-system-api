using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Document.Services;
using Document.Documents;
using Document.UploadSessions;
using Document.UploadSessions.Model;
using Shared.Data;

namespace Document;

public static class DocumentModule
{
    public static IServiceCollection AddDocumentModule(this IServiceCollection services, IConfiguration configuration)
    {
        MappingConfiguration.ConfigureMappings();

        services.AddDbContext<DocumentDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(DocumentDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "document");
            });
        });

        services.AddScoped<IDocumentUnitOfWork>(sp =>
            new DocumentUnitOfWork(sp.GetRequiredService<DocumentDbContext>(), sp));

        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IUploadSessionRepository, UploadSessionRepository>();
        services.AddScoped<IRepository<Documents.Models.Document, Guid>, DocumentRepository>();
        services.AddScoped<IRepository<UploadSession, Guid>, UploadSessionRepository>();

        services.AddScoped<IDocumentService, DocumentService>();

        services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        services.AddScoped<ISaveChangesInterceptor, DispatchDomainEventInterceptor>();


        return services;
    }

    public static IApplicationBuilder UseDocumentModule(this IApplicationBuilder app)
    {
        app.UseMigration<DocumentDbContext>();
        return app;
    }
}