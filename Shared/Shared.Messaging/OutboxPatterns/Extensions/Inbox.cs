using System.Reflection;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Data;
using Shared.Messaging.OutboxPatterns.Wrappers;
using Shared.Messaging.OutboxPatterns.Repository;
using Shared.Messaging.OutboxPatterns.Services;


namespace Shared.Messaging.OutboxPatterns.Extensions;

public static class Inbox
{
    public static IServiceCollection AddInbox<TDbContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly assembly,
        string schema
    ) where TDbContext : DbContext
    {
        var dbContextName = typeof(TDbContext).Name;
        
        services.AddKeyedScoped<IInboxRepository>(dbContextName, (provider, key) =>
            new InboxRepository<TDbContext>(
                provider.GetRequiredService<TDbContext>(),
                provider.GetRequiredService<ISqlConnectionFactory>(),
                provider.GetRequiredService<IConfiguration>(),
                schema));

        services.AddKeyedScoped<IInboxReadRepository>(dbContextName, (provider, key) =>
            new InboxReadRepository<TDbContext>(
                provider.GetRequiredService<TDbContext>(),
                provider.GetRequiredService<ISqlConnectionFactory>(),
                schema));

        services.AddKeyedScoped<IInboxService>(dbContextName, (provider, key) =>
            new InboxService(
                provider.GetRequiredService<IConfiguration>(),
                provider.GetRequiredKeyedService<IInboxReadRepository>(key),
                provider.GetRequiredKeyedService<IInboxRepository>(key)
            ));

        services.AddInboxJobs<TDbContext>(configuration);

        // TODO: Implement AddInboxCleanupJob<TDbContext>(configuration) if needed, or remove this line if not required.
        // services.AddInboxCleanupJob<TDbContext>(configuration);

        // Discover and register all consumers in the assembly
        var consumerTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .Where(t => t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>)))
            .ToList();

        foreach (var consumerType in consumerTypes)
        {
            // Register the concrete handler type (not mapped to IConsumer<T> directly)
            services.AddScoped(consumerType);

            var consumerInterfaces = consumerType.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IConsumer<>));

            foreach (var consumerInterface in consumerInterfaces)
            {
                var messageType = consumerInterface.GetGenericArguments()[0];

                // Create a unique wrapper type per consumer class: ConsumeWrapper<TMessage, TConsumer>
                var wrapperType = typeof(ConsumeWrapper<,>).MakeGenericType(messageType, consumerType);

                // Register the wrapper implementation which takes the concrete consumer as the inner
                services.AddScoped(wrapperType, sp =>
                {
                    var inner = sp.GetRequiredService(consumerType);
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger(wrapperType);
                    return Activator.CreateInstance(wrapperType, inner, sp, schema, logger)!;
                });

                // Track wrapper types so MassTransit can register the wrappers (so wrapper's Consume runs)
                ConsumerWrapperRegistry.Register(wrapperType);

                // Map the IConsumer<TMessage> to the wrapper type so resolving IConsumer<TMessage>
                // returns the wrapper. Because wrapperType is unique per consumerType, multiple
                // registrations for the same TMessage are possible.
                services.AddScoped(consumerInterface, sp => sp.GetRequiredService(wrapperType));
            }
        }

        return services;
    }
}