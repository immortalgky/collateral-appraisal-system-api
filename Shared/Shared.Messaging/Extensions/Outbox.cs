using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;
using Shared.Messaging.Services;
using Shared.OutboxPatterns.Repository;
using Shared.OutboxPatterns.Services;
using Shared.Workers;

namespace Shared.Messaging.Extensions;

public static class Outbox
{
    /// <summary>
    /// Outbox services for DbContext
    /// IOutboxService BackgroundService
    /// </summary>
    public static IServiceCollection AddOutbox<TDbContext>(this IServiceCollection services, string schema)
        where TDbContext : DbContext
    {       
        // Register repositories first
        services.AddScoped<IOutboxRepository, OutboxRepository<TDbContext>>();
        
        services.AddScoped<IOutboxReadRepository>(provider => 
            new OutboxReadRepository<TDbContext>(
                provider.GetRequiredService<TDbContext>(),
                provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredService<ISqlConnectionFactory>()));

        // Then register service that depends on repositories
        services.AddScoped<IOutboxService>(provider => 
            new OutboxService(
                provider.GetRequiredService<IPublishEndpoint>(),
                provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredService<IOutboxReadRepository>(),
                provider.GetRequiredService<IOutboxRepository>(),
                schema));

        services.AddHostedService<OutboxHostedService<TDbContext>>();
        
        return services;
    }
}
