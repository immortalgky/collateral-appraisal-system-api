using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using MassTransit;
using Shared.Messaging.Events;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// The Notification module could not send an assignment email (#624): its row becomes Failed with the reason.
/// Idempotent without an inbox, since only a Queued row changes.
/// </summary>
public class AssignmentEmailFailedIntegrationEventHandler(IAssignmentEmailRepository emails, IAppraisalUnitOfWork unitOfWork)
    : IConsumer<AssignmentEmailFailedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AssignmentEmailFailedIntegrationEvent> context)
    {
        var email = await emails.GetByIdAsync(context.Message.EmailId, context.CancellationToken);
        if (email is null) return;

        email.MarkFailed(context.Message.Reason);
        await unitOfWork.SaveChangesAsync(context.CancellationToken);
    }
}
