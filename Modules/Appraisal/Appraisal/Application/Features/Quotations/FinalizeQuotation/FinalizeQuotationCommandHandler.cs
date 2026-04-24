using Appraisal.Application.Features.Quotations.Shared;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Quotations.FinalizeQuotation;

public class FinalizeQuotationCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    IIntegrationEventOutbox outbox)
    : ICommandHandler<FinalizeQuotationCommand, FinalizeQuotationResult>
{
    public async Task<FinalizeQuotationResult> Handle(
        FinalizeQuotationCommand command,
        CancellationToken cancellationToken)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUser);

        var quotation = await quotationRepository.GetByIdAsync(command.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation '{command.QuotationRequestId}' not found");

        quotation.Finalize(command.CompanyQuotationId, command.FinalPrice, command.Reason);
        quotationRepository.Update(quotation);

        var winningQuotation = quotation.Quotations.First(q => q.Id == command.CompanyQuotationId);

        // v2: collect all appraisal IDs from the join table
        var appraisalIds = quotation.Appraisals
            .Select(a => a.AppraisalId)
            .ToArray();

        // Fall back to Items if the join table is empty (shouldn't happen after v2 migration)
        if (appraisalIds.Length == 0)
            appraisalIds = quotation.Items.Select(i => i.AppraisalId).Distinct().ToArray();

        outbox.Publish(new QuotationFinalizedIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            AppraisalIds = appraisalIds,
            RequestId = quotation.RequestId ?? Guid.Empty,
            WorkflowInstanceId = quotation.WorkflowInstanceId ?? Guid.Empty,
            TaskExecutionId = quotation.TaskExecutionId ?? Guid.Empty,
            WinningCompanyId = winningQuotation.CompanyId,
            WinningQuotationId = command.CompanyQuotationId,
            FinalFeeAmount = command.FinalPrice,
            RmUserId = quotation.RmUserId
        }, correlationId: quotation.Id.ToString());

        // v4: resume admin-finalize step in quotation child workflow (Finalize path → finalized-end)
        outbox.Publish(new QuotationWorkflowResumeIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            ActivityId = "admin-finalize",
            DecisionTaken = "Finalize",
            CompletedBy = currentUser.UserId?.ToString() ?? string.Empty
        }, correlationId: quotation.Id.ToString());

        return new FinalizeQuotationResult(
            quotation.Id,
            command.CompanyQuotationId,
            winningQuotation.CompanyId,
            command.FinalPrice,
            quotation.Status);
    }
}
