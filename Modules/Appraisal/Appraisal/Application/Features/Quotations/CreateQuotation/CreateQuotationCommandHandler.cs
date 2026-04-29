using Appraisal.Application.Features.Quotations.Shared;
using Dapper;
using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.CreateQuotation;

public class CreateQuotationCommandHandler(
    IQuotationRepository quotationRepository,
    IAppraisalRepository appraisalRepository,
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser,
    IQuotationActivityLogger activityLogger)
    : ICommandHandler<CreateQuotationCommand, CreateQuotationResult>
{
    public async Task<CreateQuotationResult> Handle(CreateQuotationCommand command, CancellationToken cancellationToken)
    {
        var quotation = QuotationRequest.Create(
            command.DueDate,
            command.RequestedBy,
            command.Description);

        if (!string.IsNullOrWhiteSpace(command.SpecialRequirements))
            quotation.SetSpecialRequirements(command.SpecialRequirements);

        // Attach appraisals atomically at create time — same domain method as AddAppraisalToDraft.
        // Also call AddItem per appraisal so QuotationRequestItem rows exist and the FE
        // can render MaxAppraisalDays, PropertyType, and PropertyLocation from the table.
        if (command.Appraisals is { Count: > 0 })
        {
            var appraisalIds = command.Appraisals.Select(a => a.AppraisalId).ToList();
            var summaries = await appraisalRepository.GetSummariesAsync(appraisalIds, cancellationToken);
            var summaryById = summaries.ToDictionary(s => s.AppraisalId);

            foreach (var entry in command.Appraisals)
            {
                if (!summaryById.TryGetValue(entry.AppraisalId, out var summary))
                    throw new BadRequestException($"Appraisal '{entry.AppraisalId}' not found.");

                quotation.AddAppraisal(entry.AppraisalId, addedBy: command.RequestedBy);

                quotation.AddItem(
                    appraisalId: entry.AppraisalId,
                    appraisalNumber: summary.AppraisalNumber ?? string.Empty,
                    propertyType: summary.PropertyType ?? "Unknown",
                    propertyLocation: summary.PropertyLocation,
                    estimatedValue: summary.EstimatedValue,
                    maxAppraisalDays: entry.MaxAppraisalDays);
            }

            // Stamp RM info: each Appraisal is 1:1 with a Request, so we always have an RM.
            // Resolve from the first appraisal's Request (any appraisal works since the typical
            // bundle shares one Request; cross-request bundles still get a sensible RM stamp).
            var firstRequestId = summaries
                .Where(s => s.RequestId.HasValue)
                .Select(s => s.RequestId!.Value)
                .FirstOrDefault();

            if (firstRequestId != Guid.Empty)
            {
                var (rmUserId, rmUsername) = await ResolveRmAsync(firstRequestId, cancellationToken);
                quotation.SetRmInfo(rmUserId, rmUsername);
            }
        }

        // Invite companies atomically at create time — same domain method as EditDraftQuotation
        if (command.InvitedCompanyIds is { Count: > 0 })
        {
            foreach (var companyId in command.InvitedCompanyIds)
                quotation.InviteCompany(companyId);
        }

        await quotationRepository.AddAsync(quotation, cancellationToken);

        var adminRole = currentUser.IsInRole("Admin") ? "Admin" : "IntAdmin";
        activityLogger.Log(quotation.Id, null, null, QuotationActivityNames.QuotationCreated, actionByRole: adminRole);

        return new CreateQuotationResult(quotation.Id);
    }

    /// <summary>
    /// Resolves the RM username from the Request's Requestor column.
    /// Mirrors the logic in StartQuotationFromTaskCommandHandler.ResolveRmAsync.
    /// Returns (null, null) on failure — quotation is still created without RM linkage.
    /// </summary>
    private async Task<(Guid? RmUserId, string? RmUsername)> ResolveRmAsync(
        Guid requestId, CancellationToken cancellationToken)
    {
        try
        {
            using var connection = connectionFactory.GetOpenConnection();
            var rmUsername = await connection.QuerySingleOrDefaultAsync<string?>(
                "SELECT Requestor FROM [request].[Requests] WHERE Id = @RequestId",
                new { RequestId = requestId });

            return (null, string.IsNullOrWhiteSpace(rmUsername) ? null : rmUsername);
        }
        catch
        {
            return (null, null);
        }
    }
}