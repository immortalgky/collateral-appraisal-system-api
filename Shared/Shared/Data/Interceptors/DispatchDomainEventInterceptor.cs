using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shared.DDD;
using Shared.OutboxPatterns.Models;
using System.Text.Json;

namespace Shared.Data.Interceptors;

public class DispatchDomainEventInterceptor(IMediator mediator) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        DispatchDomainEvents(eventData.Context).GetAwaiter().GetResult();
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await DispatchDomainEvents(eventData.Context);

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchDomainEvents(DbContext? context)
    {
        if (context == null) return;

        var aggregates = context.ChangeTracker
            .Entries<IAggregate>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity);

        var domainEvents = aggregates
            .SelectMany(e => e.DomainEvents)
            .ToList();

        var externalizable = domainEvents
            .Where(de => de.GetType().GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IExternalized<>)))
            .ToList();

        aggregates.ToList().ForEach(e => e.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
            await mediator.Publish(domainEvent);

        foreach (var domainEvent in externalizable)
        {
            // Use reflection to call ToIntegrationEvent()
            var toIntegrationEventMethod = domainEvent.GetType()
                .GetMethods()
                .FirstOrDefault(m => m.Name == "ToIntegrationEvent" && m.GetParameters().Length == 0);
                
            if (toIntegrationEventMethod != null)
            {
                var integrationEvent = toIntegrationEventMethod.Invoke(domainEvent, null);
                
                if (integrationEvent != null)
                {
                    var eventType = integrationEvent.GetType();
                    var eventTypeName = $"{eventType.FullName}, {eventType.Assembly.GetName().Name}";

                    var outboxMessage = OutboxMessage.Create(
                        domainEvent.EventId,
                        domainEvent.OccurredOn,
                        JsonSerializer.Serialize(integrationEvent),
                        eventTypeName
                    );
                    await context.Set<OutboxMessage>().AddAsync(outboxMessage);
                }
            }
        }
    }
}