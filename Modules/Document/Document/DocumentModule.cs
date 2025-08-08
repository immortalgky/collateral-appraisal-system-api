﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Builder;
using Document.Data;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Document.Services;

namespace Document;

public static class DocumentModule
{
    public static IServiceCollection AddDocumentModule(this IServiceCollection services, IConfiguration configuration)
    {
        MappingConfiguration.ConfigureMappings();

        services.AddScoped<IDocumentRepository, DocumentRepository>();

        services.AddScoped<IDocumentService, DocumentService>();

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