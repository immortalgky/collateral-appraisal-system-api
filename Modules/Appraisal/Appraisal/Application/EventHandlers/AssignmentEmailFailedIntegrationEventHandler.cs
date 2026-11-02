using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using MassTransit;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// The Notification module could not send an assignment email (#624): its row becomes Failed with the reason.
/// Idempotent without an inbox, since only a Queued row changes. One conditional UPDATE (Status = Queued), so a Sent
/// confirmation that saved first on its own endpoint is never overwritten by a stale failure report.
/// </summary>
public class AssignmentEmailFailedIntegrationEventHandler(IAssignmentEmailRepository emails, IDateTimeProvider clock)
    : IConsumer<AssignmentEmailFailedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AssignmentEmailFailedIntegrationEvent> context)
    {
        await emails.MarkFailedIfQueuedAsync(
            context.Message.EmailId, context.Message.Reason, clock.ApplicationNow, context.CancellationToken);
    }
}
