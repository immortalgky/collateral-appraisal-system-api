using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using MassTransit;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// The Notification module sent an assignment email (#620): its row becomes Sent. Idempotent without an inbox, since
/// only a Queued row changes.
/// </summary>
public class AssignmentEmailSentIntegrationEventHandler(
    IAssignmentEmailRepository emails,
    IAppraisalUnitOfWork unitOfWork,
    IDateTimeProvider clock)
    : IConsumer<AssignmentEmailSentIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AssignmentEmailSentIntegrationEvent> context)
    {
        var email = await emails.GetByIdAsync(context.Message.EmailId, context.CancellationToken);
        if (email is null) return;

        // Notification publishes without an outbox, so the event carries no time: stamped on arrival.
        email.MarkSent(clock.ApplicationNow);
        await unitOfWork.SaveChangesAsync(context.CancellationToken);
    }
}
