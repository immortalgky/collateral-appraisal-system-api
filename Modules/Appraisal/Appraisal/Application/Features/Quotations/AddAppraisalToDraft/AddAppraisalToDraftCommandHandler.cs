using Appraisal.Application.Features.Quotations.Shared;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;

namespace Appraisal.Application.Features.Quotations.AddAppraisalToDraft;

public class AddAppraisalToDraftCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    IIntegrationEventOutbox outbox)
    : ICommandHandler<AddAppraisalToDraftCommand, AddAppraisalToDraftResult>
{
    public async Task<AddAppraisalToDraftResult> Handle(
        AddAppraisalToDraftCommand command,
        CancellationToken cancellationToken)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUser);

        var quotation = await quotationRepository.GetByIdAsync(command.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation request '{command.QuotationRequestId}' not found.");

        var adminId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Cannot resolve current user ID from token");
        var adminName = currentUser.Username ?? adminId.ToString();

        // Only the quotation owner can add appraisals
        if (quotation.RequestedBy != adminId)
            throw new UnauthorizedAccessException("You can only modify your own Draft quotation.");

        if (quotation.Status != "Draft")
            throw new BadRequestException($"Cannot add appraisal to quotation in status '{quotation.Status}'.");

        // Active-quotation uniqueness: appraisal must not be in another non-terminal quotation
        var alreadyActive = await quotationRepository.HasActiveQuotationForAppraisalAsync(
            command.AppraisalId,
            excludeQuotationRequestId: command.QuotationRequestId,
            cancellationToken);

        if (alreadyActive)
            throw new ConflictException(
                $"Appraisal '{command.AppraisalId}' is already part of another active quotation request.");

        quotation.AddAppraisal(command.AppraisalId, addedBy: adminName);

        // Add display item for admin review panel
        quotation.AddItem(
            appraisalId: command.AppraisalId,
            appraisalNumber: command.AppraisalNumber,
            propertyType: command.PropertyType,
            propertyLocation: command.PropertyLocation,
            estimatedValue: command.EstimatedValue);

        quotationRepository.Update(quotation);

        outbox.Publish(new AppraisalAddedToQuotationIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            AppraisalId = command.AppraisalId,
            AdminUserId = adminId
        }, correlationId: quotation.Id.ToString());

        return new AddAppraisalToDraftResult(quotation.Id, quotation.TotalAppraisals);
    }
}
