using Integration.Application.Services;
using Integration.Domain.IdempotencyRecords;
using Integration.Domain.WebhookDeliveries;
using Integration.Domain.WebhookSubscriptions;
using Integration.Infrastructure;
using Integration.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;
using Shared.Data.Extensions;

public static class IntegrationModule
{
    public static IServiceCollection AddIntegrationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register repositories
        services.AddScoped<IWebhookSubscriptionRepository, WebhookSubscriptionRepository>();
        services.AddScoped<IRepository<WebhookSubscription, Guid>, WebhookSubscriptionRepository>();

        services.AddScoped<IIdempotencyRecordRepository, IdempotencyRecordRepository>();
        services.AddScoped<IRepository<IdempotencyRecord, Guid>, IdempotencyRecordRepository>();

        services.AddScoped<IWebhookDeliveryRepository, WebhookDeliveryRepository>();
        services.AddScoped<IRepository<WebhookDelivery, Guid>, WebhookDeliveryRepository>();

        // Register services
        services.AddScoped<IWebhookService, WebhookService>();
        services.AddHttpClient("Webhook");

        // Register unit of work
        services.AddScoped<IIntegrationUnitOfWork>(sp =>
            new IntegrationUnitOfWork(sp.GetRequiredService<IntegrationDbContext>(), sp));

        // Register DbContext
        services.AddDbContext<IntegrationDbContext>((sp, options) =>
        {
            options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
            options.UseSqlServer(configuration.GetConnectionString("Database"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(IntegrationDbContext).Assembly.GetName().Name);
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "integration");
                sqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            });
        });

        return services;
    }

    public static IApplicationBuilder UseIntegrationModule(this IApplicationBuilder app)
    {
        app.UseMigration<IntegrationDbContext>();
        return app;
    }
}
