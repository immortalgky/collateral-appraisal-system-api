using Appraisal.Application.Features.Quotations.Shared;
using Dapper;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Application.Features.Quotations.SendShortlistToRm;

public class SendShortlistToRmCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    IIntegrationEventOutbox outbox,
    IQuotationActivityLogger activityLogger,
    IDateTimeProvider dateTimeProvider,
    ISqlConnectionFactory connectionFactory)
    : ICommandHandler<SendShortlistToRmCommand, SendShortlistToRmResult>
{
    public async Task<SendShortlistToRmResult> Handle(
        SendShortlistToRmCommand command,
        CancellationToken cancellationToken)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUser);

        var quotation = await quotationRepository.GetByIdAsync(command.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation '{command.QuotationRequestId}' not found");

        quotation.SendShortlistToRm(currentUser.UserId!.Value, dateTimeProvider.ApplicationNow);

        var adminRole = currentUser.IsInRole("Admin") ? "Admin" : "IntAdmin";
        activityLogger.Log(quotation.Id, null, null, QuotationActivityNames.ShortlistSentToRm, actionByRole: adminRole);

        quotationRepository.Update(quotation);

        var shortlistedIds = quotation.Quotations
            .Where(q => q.IsShortlisted)
            .Select(q => q.Id)
            .ToArray();

        var appraisalIds = quotation.Appraisals
            .Select(a => a.AppraisalId)
            .ToArray();

        outbox.Publish(new ShortlistSentToRmIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            RequestId = quotation.RequestId ?? Guid.Empty,
            RmUsername = quotation.RmUsername,
            ShortlistedCompanyQuotationIds = shortlistedIds,
            AppraisalIds = appraisalIds
        }, correlationId: quotation.Id.ToString());

        // v4: resume admin-review-submissions step in quotation child workflow
        outbox.Publish(new QuotationWorkflowResumeIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            ActivityId = "admin-review-submissions",
            DecisionTaken = "SendToRm",
            CompletedBy = currentUser.Username ?? currentUser.UserId?.ToString() ?? string.Empty
        }, correlationId: quotation.Id.ToString());

        // Email the RM a fee-comparison table so they can pick the winner. Domain data (customer
        // name, per-appraisal report numbers, per-company net fees) is resolved here; the RM email
        // and company display names are resolved in the Notification consumer via IUserLookupService.
        await PublishFeeNoticeEmailAsync(quotation, cancellationToken);

        return new SendShortlistToRmResult(
            quotation.Id,
            quotation.Status,
            quotation.ShortlistSentToRmAt!.Value);
    }

    private async Task PublishFeeNoticeEmailAsync(QuotationRequest quotation, CancellationToken ct)
    {
        var appraisalIds = quotation.Appraisals.Select(a => a.AppraisalId).ToList();
        var shortlisted = quotation.Quotations.Where(q => q.IsShortlisted).ToList();

        var reportNumbers = new Dictionary<Guid, string?>();
        var provinces = new Dictionary<Guid, string?>();
        var propertyTypeDesc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? customerName = null;

        if (appraisalIds.Count > 0)
        {
            var connection = connectionFactory.GetOpenConnection();

            // Property-type code (e.g. "LB") → Thai description ("ที่ดินและสิ่งปลูกสร้าง"),
            // via parameter.Parameters group 'PropertyType' (same as the RCAS report views).
            var ptRows = await connection.QueryAsync(
                "SELECT Code, Description FROM parameter.Parameters " +
                "WHERE [Group] = 'PropertyType' AND [Language] = 'TH' AND [IsActive] = 1");
            foreach (var r in ptRows)
                propertyTypeDesc[(string)r.Code] = (string)r.Description;

            // Report number + resolved province NAME per appraisal. Province is a geocode on
            // LandAppraisalDetails, resolved to Thai via parameter.TitleProvinces (Code→NameTh),
            // same as the report summary providers.
            var rows0 = await connection.QueryAsync(
                """
                SELECT a.Id AS AppraisalId,
                       a.AppraisalNumber,
                       tprov.NameTh AS ProvinceName
                FROM appraisal.Appraisals a
                OUTER APPLY (
                    SELECT TOP 1 lad.Province
                    FROM appraisal.LandAppraisalDetails lad
                    JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
                    WHERE ap.AppraisalId = a.Id AND lad.Province IS NOT NULL
                    ORDER BY ap.Id
                ) p
                LEFT JOIN parameter.TitleProvinces tprov ON tprov.Code = p.Province
                WHERE a.Id IN @Ids
                """,
                new { Ids = appraisalIds });
            foreach (var r in rows0)
            {
                reportNumbers[(Guid)r.AppraisalId] = (string?)r.AppraisalNumber;
                var name = (string?)r.ProvinceName;
                provinces[(Guid)r.AppraisalId] =
                    string.IsNullOrWhiteSpace(name) ? null : "จังหวัด" + name;
            }

            // Resolve the customer via the appraisal (1:1 with a request), mirroring vw_AppraisalList.
            // quotation.RequestId is null on the manual/bundle create path (QuotationRequest.Create),
            // so key off an appraisal id — which always carries a RequestId — instead.
            customerName = await connection.QueryFirstOrDefaultAsync<string?>(
                """
                SELECT TOP 1 rc.Name
                FROM appraisal.Appraisals a
                JOIN request.RequestCustomers rc ON rc.RequestId = a.RequestId
                WHERE a.Id = @AppraisalId
                """,
                new { AppraisalId = appraisalIds[0] });
        }

        // Property-type code comes from the quotation's own display items (denormalized at create
        // time), resolved to its Thai description; province name is the geocode resolved above.
        var itemByAppraisal = quotation.Items
            .GroupBy(i => i.AppraisalId)
            .ToDictionary(g => g.Key, g => g.First());

        var columns = appraisalIds
            .Select(id =>
            {
                itemByAppraisal.TryGetValue(id, out var item);
                var ptCode = item?.PropertyType;
                var ptText = string.IsNullOrWhiteSpace(ptCode)
                    ? null
                    : propertyTypeDesc.GetValueOrDefault(ptCode, ptCode);
                return new QuotationEmailAppraisalColumn(
                    id,
                    reportNumbers.GetValueOrDefault(id),
                    ptText,
                    provinces.GetValueOrDefault(id));
            })
            .ToList();

        var rows = shortlisted
            .Select(q => new QuotationEmailCompanyRow(
                q.CompanyId,
                q.Items
                    .Where(i => appraisalIds.Contains(i.AppraisalId))
                    .GroupBy(i => i.AppraisalId)
                    .ToDictionary(g => g.Key, g => g.Sum(i => i.NetAmount)),
                q.Items.Sum(i => i.NetAmount)))
            .ToList();

        outbox.Publish(new ShortlistSentToRmEmailIntegrationEvent
        {
            QuotationRequestId = quotation.Id,
            RmUsername = quotation.RmUsername,
            AdminUsername = currentUser.Username,
            CustomerName = customerName,
            Columns = columns,
            Rows = rows
        }, correlationId: quotation.Id.ToString());
    }
}
