using Appraisal.Application.Features.Assignments.AssignmentEmails;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Appraisal.Application.EventHandlers;

// Co-located on the partitioned "appraisal-sync" endpoint (Program.cs).
[ExcludeFromConfigureEndpoints]
public class InternalFollowupAssignedIntegrationEventHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork,
    ILogger<InternalFollowupAssignedIntegrationEventHandler> logger,
    InboxGuard<AppraisalDbContext> inboxGuard,
    AssignmentEmailDispatcher emailDispatcher
) : IConsumer<InternalFollowupAssignedIntegrationEvent>
{
    public Task Consume(ConsumeContext<InternalFollowupAssignedIntegrationEvent> context) =>
        inboxGuard.RunOnceAsync(context.MessageId, GetType().Name, _ => HandleAsync(context), context.CancellationToken);

    private async Task HandleAsync(ConsumeContext<InternalFollowupAssignedIntegrationEvent> context)
    {
        var message = context.Message;
        var ct = context.CancellationToken;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for AppraisalId: {AppraisalId}, InternalAppraiserId: {InternalAppraiserId}",
            nameof(InternalFollowupAssignedIntegrationEvent), message.AppraisalId, message.InternalAppraiserId);

        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(message.AppraisalId, ct);

        if (appraisal is null)
        {
            logger.LogWarning(
                "Appraisal {AppraisalId} not found for internal followup assignment", message.AppraisalId);
            return;
        }

        // The same current-row election as the other assignment consumers (AppraisalAssignmentElection).
        var assignment = appraisal.Assignments.Current();

        if (assignment is null)
        {
            logger.LogWarning(
                "No active assignment found for Appraisal {AppraisalId} when attaching internal followup",
                message.AppraisalId);
            return;
        }

        // no_match publishes no officer: the row keeps what it had, and only the company's email goes on.
        if (message.InternalAppraiserId is { } officer)
            assignment.AssignInternalFollowup(officer, message.InternalFollowupAssignmentMethod);

        // EXT (#622): the company is on the row (CompanyAssigned is staged first and this partition keeps that order; a
        // dead-lettered CompanyAssigned redriven later leaves its template row Pending, accepted) and the officer is known,
        // so the email chosen for this engagement goes out, committed with this save through the outbox. It never holds
        // up the follow-up.
        try
        {
            await emailDispatcher.QueueExtAsync(assignment, appraisal.AppraisalNumber, message.InternalAppraiserId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Assignment email for AppraisalId {AppraisalId} was not queued; the follow-up goes ahead without it",
                message.AppraisalId);
        }

        // Kept on purpose: assignment events only touch AppraisalAssignment rows, so this stamps Appraisals.UpdatedAt
        // (read by GetAppraisalStatus and vw_MisCasReport).
        await appraisalRepository.UpdateAsync(appraisal, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Internal followup for AppraisalId {AppraisalId}: StaffId={StaffId} (none = no_match, row unchanged), Method={Method}",
            message.AppraisalId, message.InternalAppraiserId, message.InternalFollowupAssignmentMethod);
    }
}
