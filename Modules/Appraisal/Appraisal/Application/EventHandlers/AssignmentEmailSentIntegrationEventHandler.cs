using Appraisal.Domain.Appraisals;
using MassTransit;
using Shared.Messaging.Events;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// The Notification module sent an assignment email (#620): its row becomes Sent. Idempotent without an inbox, since
/// only a Pending row changes.
/// </summary>
public class AssignmentEmailSentIntegrationEventHandler(IAssignmentEmailRepository emails)
    : IConsumer<AssignmentEmailSentIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AssignmentEmailSentIntegrationEvent> context)
    {
        var email = await emails.GetByIdAsync(context.Message.EmailId, context.CancellationToken);
        if (email is null) return;

        email.MarkSent();
        await emails.SaveChangesAsync(context.CancellationToken);
    }
}
