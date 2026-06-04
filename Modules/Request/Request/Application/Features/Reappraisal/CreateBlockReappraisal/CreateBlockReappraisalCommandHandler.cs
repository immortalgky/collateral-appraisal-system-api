using Dapper;
using Microsoft.Extensions.Logging;

namespace Request.Application.Features.Reappraisal.CreateBlockReappraisal;

/// <summary>
/// Handles <see cref="CreateBlockReappraisalCommand"/>.
///
/// Single-project counterpart to <see cref="InitiateReappraisalCommandHandler"/>.
/// Reuses the same Layer-1 dedupe SQL and prior-request snapshot loader; the only
/// difference is that the working set is always exactly one PrevAppraisalId.
///
/// Snapshot copy logic:
///   Copies LoanDetail / Address / Contact / Customers / Titles / Documents from the
///   prior Request (resolved via the given PrevAppraisalId). Appointment and Fee are
///   left with a default (Bank Absorb) — staff fill them in downstream.
///
/// Cross-module safety:
///   Saves only the Request module's DbContext. The Collateral module's BlockReappraisalDue
///   row is marked Consumed by the Collateral endpoint handler in its own DbContext save,
///   after this command succeeds.
/// </summary>
public class CreateBlockReappraisalCommandHandler(
    ICreateRequestService createRequestService,
    IReappraisalGroupNumberGenerator groupNumberGenerator,
    ISqlConnectionFactory connectionFactory,
    RequestDbContext dbContext,
    ILogger<CreateBlockReappraisalCommandHandler> logger
) : ICommandHandler<CreateBlockReappraisalCommand, CreateBlockReappraisalResult>
{
    // Block reappraisal purpose code (confirmed by business).
    private const string ReappraisalPurposeCode = "09";

    // Block reappraisal channel (confirmed): SIBS — drives the forced-assignment hop in the
    // appraisal workflow, matching the AS400 reappraisal path.
    private const string ReappraisalChannel = "SIBS";

    // Default fee: Bank Absorb (same as InitiateReappraisal).
    private const string DefaultFeePaymentType = "07";
    private const string DefaultFeeRemark = "Block Project Reappraisal";

    public async Task<CreateBlockReappraisalResult> Handle(
        CreateBlockReappraisalCommand command,
        CancellationToken cancellationToken)
    {
        // ── Step 1: Generate group number ────────────────────────────────────
        var groupNumber = await groupNumberGenerator.GenerateAsync(cancellationToken);

        // ── Step 2: Layer-1 dedupe — reject if already in-flight ─────────────
        var inFlightIds = await FindInFlightAppraisalIdsAsync(
            [command.PrevAppraisalId], cancellationToken);

        if (inFlightIds.Contains(command.PrevAppraisalId))
        {
            logger.LogInformation(
                "[BLOCK-REAPPRAISAL] Skipped PrevAppraisalId {Id} — already in-flight",
                command.PrevAppraisalId);

            return new CreateBlockReappraisalResult(
                CreatedRequestId: null,
                RequestNumber: null,
                GroupNumber: groupNumber,
                Skipped: true,
                SkipReason: "AlreadyInFlight");
        }

        // ── Step 3: Load prior request snapshot ───────────────────────────────
        var snapshots = await FetchPriorRequestSnapshotsAsync(
            [command.PrevAppraisalId], cancellationToken);

        snapshots.TryGetValue(command.PrevAppraisalId, out var snapshot);

        // ── Step 4: Build CreateRequestData ───────────────────────────────────
        var customers = snapshot?.Customers is { Count: > 0 }
            ? snapshot.Customers
                .Select(c => new RequestCustomerDto(c.Name ?? string.Empty, c.ContactNumber))
                .ToList()
            : null;

        var detail = new RequestDetailDto(
            HasAppraisalBook: false,          // prior appraisal book is stale
            LoanDetail: BuildLoanDetailDto(snapshot),
            PrevAppraisalId: command.PrevAppraisalId,
            Address: BuildAddressDto(snapshot),
            Contact: BuildContactDto(snapshot),
            Appointment: null,                // intentionally blank — set by staff downstream
            Fee: BuildDefaultFeeDto());

        var properties = snapshot?.Properties is { Count: > 0 }
            ? snapshot.Properties
                .Select(p => new RequestPropertyDto(p.PropertyType, p.BuildingType, p.SellingPrice))
                .ToList()
            : null;

        var createData = new CreateRequestData(
            Purpose: ReappraisalPurposeCode,
            Channel: ReappraisalChannel,
            Requestor: command.Requestor,
            Creator: command.Creator,
            Priority: "Normal",
            IsPma: false,
            Detail: detail,
            Customers: customers,
            Properties: properties,
            Titles: snapshot?.Titles,
            Documents: snapshot?.Documents,
            Comments: null);

        // ── Step 5: Create → save → submit (mirrors InitiateReappraisal pattern) ─
        var now = DateTime.UtcNow;
        var (request, _) = await createRequestService.CreateRequestAsync(createData, cancellationToken);

        // Persist BEFORE Submit so RequestSubmittedEventHandler's DB query sees the committed row.
        await dbContext.SaveChangesAsync(cancellationToken);

        // groupNumber flows through RequestSubmittedEvent → AppraisalCreationRequestedIntegrationEvent
        // → Appraisal.SetGroupTag. It is NOT persisted on Request.
        request.Submit(now, groupNumber);

        logger.LogInformation(
            "[BLOCK-REAPPRAISAL] Created request {RequestId} ({RequestNumber}) for PrevAppraisalId {PrevId}, group {GroupNumber}",
            request.Id, request.RequestNumber?.ToString(), command.PrevAppraisalId, groupNumber);

        return new CreateBlockReappraisalResult(
            CreatedRequestId: request.Id,
            RequestNumber: request.RequestNumber?.ToString(),
            GroupNumber: groupNumber,
            Skipped: false,
            SkipReason: null);
    }

    // ── Dapper helpers (mirrors InitiateReappraisalCommandHandler) ────────────

    /// <summary>
    /// Layer-1 dedupe: returns AppraisalIds that already have a non-terminal reappraisal in-flight.
    /// Same SQL as <see cref="InitiateReappraisalCommandHandler"/>.
    /// </summary>
    private async Task<HashSet<Guid>> FindInFlightAppraisalIdsAsync(
        IReadOnlyList<Guid> appraisalIds,
        CancellationToken _)
    {
        if (appraisalIds.Count == 0) return [];

        const string sql = """
            SELECT DISTINCT a.PrevAppraisalId
            FROM appraisal.Appraisals a
            WHERE a.Status NOT IN ('Completed', 'Cancelled')
              AND a.IsDeleted = 0
              AND a.PrevAppraisalId IN @AppraisalIds
            """;

        var rows = await connectionFactory.GetOpenConnection()
            .QueryAsync<Guid>(sql, new { AppraisalIds = appraisalIds });

        return rows.ToHashSet();
    }

    /// <summary>
    /// Batch-fetches the prior Request snapshot (LoanDetail / Address / Contact / Customers /
    /// Titles / Documents) for the given AppraisalIds. Same SQL as InitiateReappraisalCommandHandler.
    /// </summary>
    private async Task<Dictionary<Guid, PriorRequestSnapshot>> FetchPriorRequestSnapshotsAsync(
        IReadOnlyList<Guid> appraisalIds,
        CancellationToken cancellationToken)
    {
        if (appraisalIds.Count == 0) return new Dictionary<Guid, PriorRequestSnapshot>();

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
            WHERE a.Id IN @AppraisalIds
              AND r.IsDeleted = 0
            """;

        const string customersSql = """
            SELECT RequestId, Name, ContactNumber
            FROM request.RequestCustomers
            WHERE RequestId IN @RequestIds
            ORDER BY RequestId, Id
            """;

        const string propertiesSql = """
            SELECT RequestId, PropertyType, BuildingType, SellingPrice
            FROM request.RequestProperties
            WHERE RequestId IN @RequestIds
            ORDER BY RequestId, Id
            """;

        var conn = connectionFactory.GetOpenConnection();
        var rows = (await conn.QueryAsync<PriorRequestRow>(snapshotSql, new { AppraisalIds = appraisalIds })).ToList();

        var requestIds = rows.Select(r => r.RequestId).Distinct().ToList();

        var customers = requestIds.Count == 0
            ? new List<PriorRequestCustomerRow>()
            : (await conn.QueryAsync<PriorRequestCustomerRow>(customersSql, new { RequestIds = requestIds })).ToList();

        var properties = requestIds.Count == 0
            ? new List<PriorRequestPropertyRow>()
            : (await conn.QueryAsync<PriorRequestPropertyRow>(propertiesSql, new { RequestIds = requestIds })).ToList();

        // Titles and request-level documents must be loaded via EF Core (owned collections / TPH hierarchy).
        var titlesByRequest = new Dictionary<Guid, List<RequestTitleDto>>();
        var documentsByRequest = new Dictionary<Guid, List<RequestDocumentDto>>();
        if (requestIds.Count > 0)
        {
            var titles = await dbContext.RequestTitles
                .Where(t => requestIds.Contains(t.RequestId))
                .ToListAsync(cancellationToken);

            foreach (var title in titles)
            {
                var dto = title.ToDto();
                if (!titlesByRequest.TryGetValue(title.RequestId, out var list))
                {
                    list = [];
                    titlesByRequest[title.RequestId] = list;
                }
                list.Add(dto);
            }

            var requestsWithDocs = await dbContext.Requests
                .Include(r => r.Documents)
                .Where(r => requestIds.Contains(r.Id))
                .ToListAsync(cancellationToken);

            foreach (var req in requestsWithDocs)
            {
                documentsByRequest[req.Id] = req.Documents
                    .Select(d => d.ToDto())
                    .ToList();
            }
        }

        var customersByRequest  = customers.GroupBy(c => c.RequestId).ToDictionary(g => g.Key, g => g.ToList());
        var propertiesByRequest = properties.GroupBy(p => p.RequestId).ToDictionary(g => g.Key, g => g.ToList());

        return rows.ToDictionary(
            r => r.AppraisalId,
            r => new PriorRequestSnapshot(
                r.AppraisalId, r.RequestId, r.HasAppraisalBook,
                r.BankingSegment, r.LoanApplicationNumber, r.FacilityLimit,
                r.AdditionalFacilityLimit, r.PreviousFacilityLimit, r.TotalSellingPrice,
                r.HouseNumber, r.ProjectName, r.Moo, r.Soi, r.Road,
                r.SubDistrict, r.District, r.Province, r.Postcode,
                r.ContactPersonName, r.ContactPersonPhone, r.DealerCode,
                customersByRequest.TryGetValue(r.RequestId, out var cList) ? cList : [],
                propertiesByRequest.TryGetValue(r.RequestId, out var pList) ? pList : [],
                titlesByRequest.TryGetValue(r.RequestId, out var tList) ? tList : [],
                documentsByRequest.TryGetValue(r.RequestId, out var dList) ? dList : []));
    }

    // ── DTO builders ──────────────────────────────────────────────────────────

    private static LoanDetailDto? BuildLoanDetailDto(PriorRequestSnapshot? snap) =>
        snap is null ? null : new LoanDetailDto(
            snap.BankingSegment,
            snap.LoanApplicationNumber,
            snap.FacilityLimit,
            snap.AdditionalFacilityLimit,
            snap.PreviousFacilityLimit,
            snap.TotalSellingPrice);

    private static AddressDto? BuildAddressDto(PriorRequestSnapshot? snap) =>
        snap is null ? null : new AddressDto(
            snap.HouseNumber, snap.ProjectName, snap.Moo, snap.Soi, snap.Road,
            snap.SubDistrict, snap.District, snap.Province, snap.Postcode);

    private static ContactDto? BuildContactDto(PriorRequestSnapshot? snap) =>
        snap is null ? null : new ContactDto(
            snap.ContactPersonName, snap.ContactPersonPhone, snap.DealerCode);

    private static FeeDto BuildDefaultFeeDto() =>
        new(FeePaymentType: DefaultFeePaymentType, FeeNotes: DefaultFeeRemark, AbsorbedAmount: null);

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
