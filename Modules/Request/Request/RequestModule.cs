using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Request.Application.Configurations;
using Request.Application.Services;
using Request.Domain.RequestComments;
using Request.Domain.RequestTitles;
using Request.Infrastructure;
using Request.Infrastructure.Repositories;
using Request.Infrastructure.Seed;
using Shared.Data.Interceptors;

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
        services.AddScoped<IRequestSyncService, RequestSyncService>();

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

        return services;
    }

    public static IApplicationBuilder UseRequestModule(this IApplicationBuilder app)
    {
        app.UseMigration<RequestDbContext>();

        return app;
    }
}