using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;
using Shared.Messaging.Services;
using Shared.OutboxPatterns.Repository;
using Shared.Messaging.Workers;
using Shared.Messaging.OutboxPatterns.Repository;
using Shared.Messaging.OutboxPatterns.Services;

namespace Shared.OutboxPatterns.Extensions;

public static class Outbox
{
    /// <summary>
    /// Outbox-Patten ("Outbox" is Messages Output)
    /// Outbox services for DbContext
    /// IOutboxService BackgroundService
    /// </summary>
    public static IServiceCollection AddOutbox<TDbContext>(this IServiceCollection services, string schema)
        where TDbContext : DbContext
    {       
        // Register repositories with keyed services for specific DbContext type
        var contextKey = typeof(TDbContext).Name;
        
        services.AddKeyedScoped<IOutboxRepository>(contextKey, (provider, key) => 
            new OutboxRepository<TDbContext>(provider.GetRequiredService<TDbContext>()));
        
        services.AddKeyedScoped<IOutboxReadRepository>(contextKey, (provider, key) => 
            new OutboxReadRepository<TDbContext>(
                provider.GetRequiredService<TDbContext>(),
                provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredService<ISqlConnectionFactory>()));

        // Register service with the specific schema for this DbContext
        services.AddKeyedScoped<IOutboxService>(contextKey, (provider, key) => 
            new OutboxService(
                provider.GetRequiredService<IPublishEndpoint>(),
                provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredKeyedService<IOutboxReadRepository>(key),
                provider.GetRequiredKeyedService<IOutboxRepository>(key),
                schema));

        services.AddHostedService<OutboxHostedService<TDbContext>>();
        
        return services;
    }
}
