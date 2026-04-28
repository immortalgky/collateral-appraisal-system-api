using Shared.Time;

namespace Appraisal.Application.Features.Quotations.AutoDeclineCompanyQuotation;

/// <summary>
/// Marks a company's quotation as Declined when the quotation DueDate has passed and the company
/// did not submit or explicitly decline.
///
/// Idempotent: if the CompanyQuotation is already in a terminal status (Declined, Accepted,
/// Withdrawn) or the invitation is already Expired/Declined, the handler exits without changes.
/// </summary>
public class AutoDeclineCompanyQuotationCommandHandler(
    IQuotationRepository quotationRepository,
    IDateTimeProvider dateTimeProvider,
    ILogger<AutoDeclineCompanyQuotationCommandHandler> logger)
    : ICommandHandler<AutoDeclineCompanyQuotationCommand, Unit>
{
    private static readonly string[] TerminalStatuses = ["Accepted", "Withdrawn", "Declined"];

    public async Task<Unit> Handle(
        AutoDeclineCompanyQuotationCommand command,
        CancellationToken cancellationToken)
    {
        var quotation = await quotationRepository.GetByIdAsync(command.QuotationRequestId, cancellationToken);
        if (quotation is null)
        {
            logger.LogWarning(
                "AutoDeclineCompanyQuotation: QuotationRequest {QuotationRequestId} not found — skipping",
                command.QuotationRequestId);
            return Unit.Value;
        }

        var invitation = quotation.Invitations.FirstOrDefault(i => i.CompanyId == command.CompanyId);
        if (invitation is null)
        {
            logger.LogWarning(
                "AutoDeclineCompanyQuotation: Company {CompanyId} not invited to quotation {QuotationRequestId} — skipping",
                command.CompanyId, command.QuotationRequestId);
            return Unit.Value;
        }

        // Handle the CompanyQuotation record
        var existing = quotation.Quotations.FirstOrDefault(q => q.CompanyId == command.CompanyId);
        if (existing is not null)
        {
            if (TerminalStatuses.Contains(existing.Status))
            {
                logger.LogDebug(
                    "AutoDeclineCompanyQuotation: CompanyQuotation for company {CompanyId} on quotation {QuotationRequestId} is already {Status} — skipping",
                    command.CompanyId, command.QuotationRequestId, existing.Status);
                return Unit.Value;
            }

            existing.Decline(command.Reason, "SYSTEM", dateTimeProvider.ApplicationNow);
            logger.LogInformation(
                "AutoDeclineCompanyQuotation: declined existing CompanyQuotation for company {CompanyId} on quotation {QuotationRequestId}",
                command.CompanyId, command.QuotationRequestId);
        }
        else
        {
            // Company never submitted — create a Declined record
            var declined = CompanyQuotation.CreateDeclined(
                quotationRequestId: command.QuotationRequestId,
                invitationId: invitation.Id,
                companyId: command.CompanyId,
                quotationNumber: $"EXPIRED-{command.CompanyId:N}",
                reason: command.Reason,
                declinedBy: "SYSTEM",
                declinedAt: dateTimeProvider.ApplicationNow);

            quotation.AddQuotation(declined);
            logger.LogInformation(
                "AutoDeclineCompanyQuotation: created Declined CompanyQuotation for company {CompanyId} on quotation {QuotationRequestId}",
                command.CompanyId, command.QuotationRequestId);
        }

        // Mark the invitation as Expired (not Declined — system expiry is semantically distinct)
        invitation.MarkExpired();

        quotationRepository.Update(quotation);

        return Unit.Value;
    }
}
