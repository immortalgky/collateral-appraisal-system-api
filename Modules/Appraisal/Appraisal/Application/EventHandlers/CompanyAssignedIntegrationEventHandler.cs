using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;

namespace Appraisal.Application.EventHandlers;

public class CompanyAssignedIntegrationEventHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork,
    ILogger<CompanyAssignedIntegrationEventHandler> logger
) : IConsumer<CompanyAssignedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<CompanyAssignedIntegrationEvent> context)
    {
        var message = context.Message;

        logger.LogInformation(
            "Integration Event received: {IntegrationEvent} for AppraisalId: {AppraisalId}, CompanyId: {CompanyId}",
            nameof(CompanyAssignedIntegrationEvent), message.AppraisalId, message.CompanyId);

        var appraisal = await appraisalRepository.GetByIdWithAllDataAsync(
            message.AppraisalId, context.CancellationToken);

        if (appraisal is null)
        {
            logger.LogWarning(
                "Appraisal {AppraisalId} not found for company assignment", message.AppraisalId);
            return;
        }

        // Find the current active assignment (latest non-cancelled, non-rejected)
        var assignment = appraisal.Assignments
            .Where(a => a.AssignmentStatus.Code != "Rejected" && a.AssignmentStatus.Code != "Cancelled")
            .OrderByDescending(a => a.AssignedAt)
            .FirstOrDefault();

        if (assignment is null)
        {
            logger.LogWarning(
                "No active assignment found for Appraisal {AppraisalId}", message.AppraisalId);
            return;
        }

        assignment.Assign(
            assignmentType: "External",
            assigneeCompanyId: message.CompanyId.ToString(),
            assignmentMethod: message.AssignmentMethod,
            assignedBy: "System");

        await appraisalRepository.UpdateAsync(appraisal, context.CancellationToken);
        await unitOfWork.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation(
            "Updated AppraisalAssignment for AppraisalId {AppraisalId}: CompanyId={CompanyId}, Method={Method}",
            message.AppraisalId, message.CompanyId, message.AssignmentMethod);
    }
}
