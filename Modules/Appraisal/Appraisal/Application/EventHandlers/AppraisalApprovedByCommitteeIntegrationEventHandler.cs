using Appraisal.Domain.Appraisals;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Appraisal.Application.EventHandlers;

/// <summary>
/// Handles AppraisalApprovedByCommitteeIntegrationEvent by stamping CompletedAt and
/// ApprovedByCommittee on the Appraisal aggregate.
/// Published by the Workflow module's EmitAppraisalApprovedByCommitteeStep pipeline step
/// when pending-approval completes with decision == "approve".
/// </summary>
public class AppraisalApprovedByCommitteeIntegrationEventHandler(
    ILogger<AppraisalApprovedByCommitteeIntegrationEventHandler> logger,
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork)
    : IConsumer<AppraisalApprovedByCommitteeIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AppraisalApprovedByCommitteeIntegrationEvent> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for AppraisalId: {AppraisalId} CommitteeCode: {CommitteeCode}",
            nameof(AppraisalApprovedByCommitteeIntegrationEvent),
            message.AppraisalId,
            message.CommitteeCode);

        try
        {
            var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(
                message.AppraisalId, context.CancellationToken);

            if (appraisal is null)
            {
                logger.LogWarning(
                    "Appraisal {AppraisalId} not found when handling {IntegrationEvent}",
                    message.AppraisalId,
                    nameof(AppraisalApprovedByCommitteeIntegrationEvent));
                return;
            }

            appraisal.MarkApprovedByCommittee(message.CommitteeCode, message.ApprovedAt);

            await unitOfWork.SaveChangesAsync(context.CancellationToken);

            logger.LogInformation(
                "Successfully stamped committee approval for AppraisalId {AppraisalId} CommitteeCode {CommitteeCode}",
                message.AppraisalId,
                message.CommitteeCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Error processing {IntegrationEvent} for AppraisalId: {AppraisalId}",
                nameof(AppraisalApprovedByCommitteeIntegrationEvent),
                message.AppraisalId);

            throw;
        }
    }
}
