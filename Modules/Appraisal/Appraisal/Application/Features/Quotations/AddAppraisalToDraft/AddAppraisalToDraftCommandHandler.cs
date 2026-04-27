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

        var adminUsername = currentUser.Username
            ?? throw new UnauthorizedAccessException("Cannot resolve current user username from token");
        var adminUserId = currentUser.UserId; // for integration event only

        // Only the quotation owner can add appraisals
        if (quotation.RequestedBy != adminUsername)
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

        quotation.AddAppraisal(command.AppraisalId, addedBy: adminUsername);

        // Add display item for admin review panel
        quotation.AddItem(
            appraisalId: command.AppraisalId,
            appraisalNumber: command.AppraisalNumber,
            propertyType: command.PropertyType,
            propertyLocation: command.PropertyLocation,
            estimatedValue: command.EstimatedValue,
            maxAppraisalDays: command.MaxAppraisalDays);

        quotationRepository.Update(quotation);

        outbox.Publish(new AppraisalAddedToQuotationIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            AppraisalId = command.AppraisalId,
            AdminUserId = adminUserId ?? Guid.Empty
        }, correlationId: quotation.Id.ToString());

        return new AddAppraisalToDraftResult(quotation.Id, quotation.TotalAppraisals);
    }
}
