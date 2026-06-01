using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Request.Application.Configurations;
using Request.Application.Services;
using Request.Domain.RequestComments;
using Request.Domain.RequestTitles;
using Request.Infrastructure;
using Request.Infrastructure.Reappraisal;
using Request.Infrastructure.Repositories;
using Request.Infrastructure.Seed;
using Shared.Data.Interceptors;
using Shared.Reappraisal;

namespace Request;

public static class RequestModule
{
    public static IServiceCollection AddRequestModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure Mapster mappings
        MappingConfiguration.ConfigureMappings();

        // Aggregate repositories (DDD structure)
        // Request aggregate manages RequestDocument as child entity
        services.AddScoped<IRequestRepository, RequestRepository>();

        // RequestTitle is now a separate aggregate
        services.AddScoped<IRequestTitleRepository, RequestTitleRepository>();
        services.AddScoped<IRepository<RequestTitle, Guid>, RequestTitleRepository>();

        services.AddScoped<IRequestCommentRepository, RequestCommentRepository>();
        services.AddScoped<IRepository<RequestComment, Guid>, RequestCommentRepository>();

        services.AddScoped<IAppraisalNumberGenerator, AppraisalNumberGenerator>();
        services.AddScoped<IReappraisalGroupNumberGenerator, ReappraisalGroupNumberGenerator>();

        // Reappraisal file ingestion: config-switched Local (dev) / SFTP (UAT-prod).
        services.Configure<ReappraisalOptions>(configuration.GetSection(ReappraisalOptions.SectionName));
        var fileSourceType = configuration
            .GetSection(ReappraisalOptions.SectionName)
            .GetValue<string>("FileSource") ?? "Local";

        if (string.Equals(fileSourceType, "Sftp", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IReappraisalFileSource, SftpFileSource>();
        else
            services.AddScoped<IReappraisalFileSource, LocalFolderFileSource>();

        services.AddScoped<CollatrevFileParser>();
        services.AddScoped<ReappraisalIngestionJob>();

        // Dev-only test-file generator (endpoint gated to Development in GenerateTestFileEndpoint).
        services.AddSingleton<CollatrevFileWriter>();
        services.AddScoped<CollatrevTestFileBuilder>();
        services.AddScoped<IRequestSyncService, RequestSyncService>();
        services.AddScoped<
            Request.Contracts.RequestDocuments.IRequestDocumentAttacher,
            Request.Application.Services.RequestDocumentAttacher>();

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

        services.AddScoped<IRequestUnitOfWork>(sp =>
            new RequestUnitOfWork(sp.GetRequiredService<RequestDbContext>(), sp));

        services.AddScoped<IDataSeeder<RequestDbContext>, RequestDataSeed>();

        // Add other services, handlers, etc.
        services.AddTransient<ICreateRequestService, CreateRequestService>();
        services.AddScoped<IRequestDocumentValidator, RequestDocumentValidator>();

        return services;
    }

    public static IApplicationBuilder UseRequestModule(this IApplicationBuilder app)
    {
        app.UseMigration<RequestDbContext>();

        return app;
    }
}