using Dapper;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;
using Shared.Time;

namespace Request.Application.EventHandlers.Reappraisal;

/// <summary>
/// Creates and submits one reappraisal <c>Request</c> for each
/// <see cref="ReappraisalInitiatedIntegrationEvent"/> published by the Collateral module.
///
/// Idempotency key: GroupNumber + (PrevAppraisalId ?? SurveyNumber).
/// <see cref="InboxGuard{TDbContext}"/> deduplicates retried/concurrent delivery.
///
/// The handler:
///   1. Fetches the prior request snapshot (LoanDetail / Address / Contact / Customers /
///      Properties / Titles / Documents) from the DB, keyed on PrevAppraisalId.
///   2. Builds <see cref="CreateRequestData"/> (same shape as the old synchronous handler).
///   3. Calls <see cref="ICreateRequestService.CreateRequestAsync"/> (creates + saves).
///   4. Sets external reference (CollateralId or SurveyNumber → "SIBS").
///   5. Calls <c>request.Submit</c> with the GroupNumber as the group tag.
/// </summary>
public class ReappraisalInitiatedIntegrationEventHandler(
    RequestDbContext dbContext,
    ICreateRequestService createRequestService,
    IRequestUnitOfWork unitOfWork,
    ISqlConnectionFactory connectionFactory,
    IDateTimeProvider dateTimeProvider,
    InboxGuard<RequestDbContext> inboxGuard,
    ILogger<ReappraisalInitiatedIntegrationEventHandler> logger)
    : IConsumer<ReappraisalInitiatedIntegrationEvent>
{
    // FeePaymentType "07" = Bank Absorb.
    private const string DefaultFeePaymentType = "07";
    private const string DefaultFeeRemark = "Periodical Reappraisal";
    private const string ReappraisalPurposeCode = "03";

    public async Task Consume(ConsumeContext<ReappraisalInitiatedIntegrationEvent> context)
    {
        if (await inboxGuard.TryClaimAsync(context.MessageId, GetType().Name, context.CancellationToken))
            return;

        var msg = context.Message;
        var ct  = context.CancellationToken;

        logger.LogInformation(
            "[REAPPRAISAL-CONSUMER] Handling event for GroupNumber={GroupNumber} Source={Source} PrevAppraisalId={PrevId}",
            msg.GroupNumber, msg.Source, msg.PrevAppraisalId);

        // ── Fetch prior request snapshot ──────────────────────────────────────
        PriorRequestSnapshot? snapshot = null;
        if (msg.PrevAppraisalId.HasValue)
            snapshot = await FetchPriorRequestSnapshotAsync(msg.PrevAppraisalId.Value, ct);

        // ── Build CreateRequestData ────────────────────────────────────────────
        var customers = BuildCustomers(msg, snapshot);
        var detail = new RequestDetailDto(
            HasAppraisalBook: false,
            LoanDetail: BuildLoanDetailDto(snapshot),
            PrevAppraisalId: msg.PrevAppraisalId,
            Address: BuildAddressDto(snapshot),
            Contact: BuildContactDto(snapshot),
            Appointment: null,
            Fee: new FeeDto(FeePaymentType: DefaultFeePaymentType, FeeNotes: DefaultFeeRemark, AbsorbedAmount: null));

        var createData = new CreateRequestData(
            Purpose: ReappraisalPurposeCode,
            Channel: "SIBS",
            Requestor: msg.Requestor,
            Creator: msg.Creator,
            Priority: "Normal",
            IsPma: false,
            Detail: detail,
            Customers: customers,
            Properties: BuildProperties(snapshot),
            Titles: snapshot?.Titles,
            Documents: snapshot?.Documents,
            Comments: null);

        // ── Create + persist + submit ─────────────────────────────────────────
        var now = dateTimeProvider.ApplicationNow;
        var (request, _) = await createRequestService.CreateRequestAsync(createData, ct);

        // Set external reference (CollateralId when from candidate, SurveyNumber otherwise).
        var externalKey = msg.CollateralId ?? msg.SurveyNumber;
        if (!string.IsNullOrWhiteSpace(externalKey))
            request.SetExternalReference(externalKey, "SIBS");

        // Persist via the Request UoW — NOT plain dbContext.SaveChangesAsync: RequestNumber is
        // generated ONLY inside RequestUnitOfWork.SaveChangesAsync (it stamps Added requests).
        // This also commits the created state so RequestSubmittedEventHandler's title/document
        // query (next save) sees it.
        await unitOfWork.SaveChangesAsync(ct);

        // Submit, then save AGAIN: Submit only raises RequestSubmittedEvent in-memory; it is
        // dispatched by the DispatchDomainEventInterceptor on THIS SaveChanges, which drives
        // AppraisalCreationRequestedIntegrationEvent → the workflow. GroupNumber rides the event
        // to Appraisal.SetGroupTag; it is not persisted on Request.
        request.Submit(now, groupTag: msg.GroupNumber, entrySource: "SIBS");
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "[REAPPRAISAL-CONSUMER] Created + submitted Request {RequestId} ({RequestNumber}) for GroupNumber={GroupNumber}",
            request.Id, request.RequestNumber?.ToString(), msg.GroupNumber);

        await inboxGuard.MarkAsProcessedAsync(context.MessageId, GetType().Name, ct);
    }

    // ── Snapshot fetch ────────────────────────────────────────────────────────

    private async Task<PriorRequestSnapshot?> FetchPriorRequestSnapshotAsync(
        Guid prevAppraisalId,
        CancellationToken cancellationToken)
    {
        const string snapshotSql = """
            SELECT a.Id AS AppraisalId, r.Id AS RequestId,
                   rd.HasAppraisalBook,
                   rd.BankingSegment, rd.LoanApplicationNumber, rd.FacilityLimit,
                   rd.AdditionalFacilityLimit, rd.PreviousFacilityLimit, rd.TotalSellingPrice,
                   rd.HouseNumber, rd.ProjectName, rd.Moo, rd.Soi, rd.Road,
                   rd.SubDistrict, rd.District, rd.Province, rd.Postcode,
                   rd.ContactPersonName, rd.ContactPersonPhone, rd.DealerCode
            FROM appraisal.Appraisals a
            JOIN request.Requests       r  ON r.Id  = a.RequestId
            JOIN request.RequestDetails rd ON rd.RequestId = r.Id
            WHERE a.Id = @AppraisalId
              AND r.IsDeleted = 0
            """;

        const string customersSql = """
            SELECT RequestId, Name, ContactNumber
            FROM request.RequestCustomers
            WHERE RequestId = @RequestId
            ORDER BY Id
            """;

        const string propertiesSql = """
            SELECT RequestId, PropertyType, BuildingType, SellingPrice
            FROM request.RequestProperties
            WHERE RequestId = @RequestId
            ORDER BY Id
            """;

        var conn = connectionFactory.GetOpenConnection();
        var row = await conn.QueryFirstOrDefaultAsync<PriorRequestRow>(
            snapshotSql, new { AppraisalId = prevAppraisalId });

        if (row is null)
        {
            logger.LogWarning(
                "[REAPPRAISAL-CONSUMER] No prior Request found for PrevAppraisalId={Id} — creating minimal request",
                prevAppraisalId);
            return null;
        }

        var customers = (await conn.QueryAsync<PriorRequestCustomerRow>(
            customersSql, new { row.RequestId })).ToList();
        var properties = (await conn.QueryAsync<PriorRequestPropertyRow>(
            propertiesSql, new { row.RequestId })).ToList();

        // Load titles and documents via EF Core — owned collections require it.
        var titles = await dbContext.RequestTitles
            .Where(t => t.RequestId == row.RequestId)
            .ToListAsync(cancellationToken);

        var titleDtos = titles.Select(t => t.ToDto()).ToList();

        var requestWithDocs = await dbContext.Requests
            .Include(r => r.Documents)
            .FirstOrDefaultAsync(r => r.Id == row.RequestId, cancellationToken);

        var documentDtos = requestWithDocs?.Documents
            .Select(d => d.ToDto())
            .ToList() ?? [];

        return new PriorRequestSnapshot(
            row.AppraisalId, row.RequestId, row.HasAppraisalBook,
            row.BankingSegment, row.LoanApplicationNumber, row.FacilityLimit,
            row.AdditionalFacilityLimit, row.PreviousFacilityLimit, row.TotalSellingPrice,
            row.HouseNumber, row.ProjectName, row.Moo, row.Soi, row.Road,
            row.SubDistrict, row.District, row.Province, row.Postcode,
            row.ContactPersonName, row.ContactPersonPhone, row.DealerCode,
            customers, properties, titleDtos, documentDtos);
    }

    // ── Data builders ─────────────────────────────────────────────────────────

    private static List<RequestCustomerDto>? BuildCustomers(
        ReappraisalInitiatedIntegrationEvent msg,
        PriorRequestSnapshot? snap)
    {
        if (snap?.Customers is { Count: > 0 })
            return snap.Customers
                .Select(c => new RequestCustomerDto(c.Name ?? string.Empty, c.ContactNumber))
                .ToList();

        if (!string.IsNullOrWhiteSpace(msg.CifName))
            return [new RequestCustomerDto(msg.CifName, null)];

        return null;
    }

    private static LoanDetailDto? BuildLoanDetailDto(PriorRequestSnapshot? snap) =>
        snap is null ? null : new LoanDetailDto(
            snap.BankingSegment, snap.LoanApplicationNumber, snap.FacilityLimit,
            snap.AdditionalFacilityLimit, snap.PreviousFacilityLimit, snap.TotalSellingPrice);

    private static AddressDto? BuildAddressDto(PriorRequestSnapshot? snap) =>
        snap is null ? null : new AddressDto(
            snap.HouseNumber, snap.ProjectName, snap.Moo, snap.Soi, snap.Road,
            snap.SubDistrict, snap.District, snap.Province, snap.Postcode);

    private static ContactDto? BuildContactDto(PriorRequestSnapshot? snap) =>
        snap is null ? null : new ContactDto(
            snap.ContactPersonName, snap.ContactPersonPhone, snap.DealerCode);

    private static List<RequestPropertyDto>? BuildProperties(PriorRequestSnapshot? snap) =>
        snap?.Properties is { Count: > 0 }
            ? snap.Properties
                .Select(p => new RequestPropertyDto(p.PropertyType, p.BuildingType, p.SellingPrice))
                .ToList()
            : null;

    // ── Private record types ───────────────────────────────────────────────────

    private sealed record PriorRequestRow(
        Guid AppraisalId, Guid RequestId, bool HasAppraisalBook,
        string? BankingSegment, string? LoanApplicationNumber, decimal? FacilityLimit,
        decimal? AdditionalFacilityLimit, decimal? PreviousFacilityLimit, decimal? TotalSellingPrice,
        string? HouseNumber, string? ProjectName, string? Moo, string? Soi, string? Road,
        string? SubDistrict, string? District, string? Province, string? Postcode,
        string? ContactPersonName, string? ContactPersonPhone, string? DealerCode);

    private sealed record PriorRequestCustomerRow(Guid RequestId, string? Name, string? ContactNumber);
    private sealed record PriorRequestPropertyRow(Guid RequestId, string? PropertyType, string? BuildingType, decimal? SellingPrice);

    private sealed record PriorRequestSnapshot(
        Guid AppraisalId, Guid RequestId, bool HasAppraisalBook,
        string? BankingSegment, string? LoanApplicationNumber, decimal? FacilityLimit,
        decimal? AdditionalFacilityLimit, decimal? PreviousFacilityLimit, decimal? TotalSellingPrice,
        string? HouseNumber, string? ProjectName, string? Moo, string? Soi, string? Road,
        string? SubDistrict, string? District, string? Province, string? Postcode,
        string? ContactPersonName, string? ContactPersonPhone, string? DealerCode,
        List<PriorRequestCustomerRow> Customers,
        List<PriorRequestPropertyRow> Properties,
        List<RequestTitleDto> Titles,
        List<RequestDocumentDto> Documents);
}
