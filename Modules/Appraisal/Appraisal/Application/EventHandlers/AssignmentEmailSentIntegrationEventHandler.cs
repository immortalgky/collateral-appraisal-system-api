using Appraisal.Domain.Appraisals;
using MassTransit;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// The Notification module sent an assignment email (#620): its row becomes Sent. Idempotent without an inbox, since
/// only a Queued or Failed row changes.
/// </summary>
public class AssignmentEmailSentIntegrationEventHandler(
    IAssignmentEmailRepository emails,
    IDateTimeProvider clock)
    : IConsumer<AssignmentEmailSentIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AssignmentEmailSentIntegrationEvent> context)
    {
        // Notification publishes without an outbox, so the event carries no time: stamped on arrival. One conditional
        // UPDATE (Queued, or Failed for a redrive), so it never races a failure report into a lost update.
        await emails.MarkSentIfQueuedOrFailedAsync(context.Message.EmailId, clock.ApplicationNow, context.CancellationToken);
    }
}
