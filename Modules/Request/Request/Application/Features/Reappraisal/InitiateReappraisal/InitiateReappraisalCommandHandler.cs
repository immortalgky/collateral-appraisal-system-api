using Dapper;
using Microsoft.Extensions.Logging;

namespace Request.Application.Features.Reappraisal.InitiateReappraisal;

/// <summary>
/// Handles <see cref="InitiateReappraisalCommand"/>.
///
/// Merged working list:
///   (A) CandidateIds     → load Pending candidates, resolve AppraisalId via SurveyNumber,
///                          create Request from candidate data, MarkConsumed.
///   (B) NearbyAppraisalIds → for each: look up matching Pending candidate by
///                            AppraisalNumber=SurveyNumber (attach if found so Layer 2 fires);
///                            else create Request with minimal data from vw_AppraisalList.
///
/// Layer 1 — server-side dedupe (before creating anything):
///   One Dapper IN-query on request.RequestDetails checks whether any resolved AppraisalId
///   already has a non-terminal reappraisal Request.  Matches are removed from the working
///   list and added to the Skipped output.
///
/// Layer 2 — cascade MarkConsumed:
///   After successfully creating a Request from a NearbyAppraisalId pick, if a matching
///   Pending ReappraisalCandidate exists (SurveyNumber = AppraisalNumber) it is MarkConsumed.
///
/// All operations share the <see cref="IRequestUnitOfWork"/> transaction via
/// <see cref="ITransactionalCommand{T}"/>. SaveChanges is called once by TransactionalBehavior.
/// </summary>
public class InitiateReappraisalCommandHandler(
    ICreateRequestService createRequestService,
    IReappraisalGroupNumberGenerator groupNumberGenerator,
    ISqlConnectionFactory connectionFactory,
    RequestDbContext dbContext,
    ILogger<InitiateReappraisalCommandHandler> logger
) : ICommandHandler<InitiateReappraisalCommand, InitiateReappraisalResult>
{
    // TODO(confirm): reappraisal purpose code — placeholder "03". Confirm with business.
    private const string ReappraisalPurposeCode = "03";

    public async Task<InitiateReappraisalResult> Handle(
        InitiateReappraisalCommand command,
        CancellationToken cancellationToken)
    {
        var hasCandidates = command.CandidateIds.Count > 0;
        var hasNearby     = command.NearbyAppraisalIds.Count > 0;

        if (!hasCandidates && !hasNearby)
            throw new ArgumentException(
                "At least one CandidateId or NearbyAppraisalId must be provided.", nameof(command));

        // ── Step 1: Generate shared group number ──────────────────────────────
        var groupNumber = await groupNumberGenerator.GenerateAsync(cancellationToken);

        // ── Step 2: Load Pending candidates (CandidateIds path) ──────────────
        var candidates = hasCandidates
            ? await dbContext.ReappraisalCandidates
                .Where(c => command.CandidateIds.Contains(c.Id)
                            && c.Status == ReappraisalCandidateStatus.Pending)
                .ToListAsync(cancellationToken)
            : new List<ReappraisalCandidate>();

        if (hasCandidates && candidates.Count == 0)
            throw new InvalidOperationException("No Pending candidates found for the provided CandidateIds.");

        if (hasCandidates && candidates.Count != command.CandidateIds.Count)
        {
            var found   = candidates.Select(c => c.Id).ToHashSet();
            var missing = command.CandidateIds.Where(id => !found.Contains(id)).ToList();
            logger.LogWarning(
                "[REAPPRAISAL-INITIATE] {Count} candidates not found or not Pending: {Ids}",
                missing.Count, string.Join(", ", missing));
        }

        // ── Step 3: Resolve SurveyNumber → AppraisalId for all candidates ─────
        var surveyNumbers         = candidates.Select(c => c.SurveyNumber).Distinct().ToList();
        var appraisalIdBySurvey   = await ResolvePrevAppraisalIdsAsync(surveyNumbers, cancellationToken);

        // ── Step 4: Build working list (AppraisalId?, AppraisalNumber?, Candidate?) ─
        var workingItems = candidates
            .Select(c =>
            {
                appraisalIdBySurvey.TryGetValue(c.SurveyNumber, out var appraisalId);
                return new WorkingItem(appraisalId, c.SurveyNumber, c);
            })
            .ToList();

        // NearbyAppraisalIds path: fetch appraisal numbers, find any matching Pending candidate
        if (hasNearby)
        {
            var nearbyRows = await FetchAppraisalNumbersAsync(command.NearbyAppraisalIds, cancellationToken);

            // Index already-loaded candidates by SurveyNumber for fast lookup
            var candidateBySurvey = candidates
                .GroupBy(c => c.SurveyNumber, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var row in nearbyRows)
            {
                // Skip if already covered by a candidate in the working list
                if (workingItems.Any(w => w.AppraisalId == row.AppraisalId))
                    continue;

                // Try to attach a matching Pending candidate (Layer 2: will be MarkConsumed later)
                ReappraisalCandidate? matchedCandidate = null;
                if (!string.IsNullOrWhiteSpace(row.AppraisalNumber))
                {
                    if (!candidateBySurvey.TryGetValue(row.AppraisalNumber, out matchedCandidate))
                    {
                        matchedCandidate = await dbContext.ReappraisalCandidates
                            .FirstOrDefaultAsync(c => c.SurveyNumber == row.AppraisalNumber
                                                      && c.Status == ReappraisalCandidateStatus.Pending,
                                cancellationToken);
                    }
                }

                workingItems.Add(new WorkingItem(row.AppraisalId, row.AppraisalNumber, matchedCandidate));
            }
        }

        // ── Step 5: Layer 1 — server-side dedupe ──────────────────────────────
        var allAppraisalIds = workingItems
            .Where(w => w.AppraisalId.HasValue)
            .Select(w => w.AppraisalId!.Value)
            .Distinct()
            .ToList();

        var inFlightIds = await FindInFlightAppraisalIdsAsync(allAppraisalIds, cancellationToken);

        var skipped   = new List<SkippedReappraisalItem>();
        var toProcess = new List<WorkingItem>();

        foreach (var item in workingItems)
        {
            if (item.AppraisalId.HasValue && inFlightIds.Contains(item.AppraisalId.Value))
            {
                skipped.Add(new SkippedReappraisalItem(
                    item.AppraisalId.Value,
                    item.AppraisalNumber,
                    "AlreadyInFlight"));

                logger.LogInformation(
                    "[REAPPRAISAL-INITIATE] Skipped AppraisalId {AppraisalId} ({Number}) — already in-flight",
                    item.AppraisalId, item.AppraisalNumber);
            }
            else
            {
                toProcess.Add(item);
            }
        }

        if (toProcess.Count == 0)
        {
            logger.LogWarning(
                "[REAPPRAISAL-INITIATE] All items skipped — no requests created for group {GroupNumber}.",
                groupNumber);
            return new InitiateReappraisalResult(groupNumber, [], skipped);
        }

        // ── Step 6: Create requests ───────────────────────────────────────────
        // For each working item we copy LoanDetail / Address / Contact / Customers from the prior
        // Request (resolved via the matched in-system appraisal). Appointment and Fee are LEFT
        // BLANK on purpose — staff will fill them in downstream. Because that leaves required-field
        // gaps that `Request.Validate()` rejects, we deliberately bypass Validate and run the
        // CreateRequestAsync → SaveChanges → Submit sequence manually (Validate is form-validation,
        // not appropriate for a system-generated reappraisal request).
        var snapshotIds = toProcess
            .Where(i => i.AppraisalId.HasValue)
            .Select(i => i.AppraisalId!.Value)
            .Distinct()
            .ToList();
        var snapshots = await FetchPriorRequestSnapshotsAsync(snapshotIds, cancellationToken);

        var createdIds = new List<Guid>(toProcess.Count);

        foreach (var item in toProcess)
        {
            var now = DateTime.UtcNow;

            PriorRequestSnapshot? snapshot = null;
            if (item.AppraisalId.HasValue)
                snapshots.TryGetValue(item.AppraisalId.Value, out snapshot);

            CreateRequestData createData;
            string? externalCaseKey;

            if (item.Candidate is not null)
            {
                // ── (A) Candidate-based path ──────────────────────────────────
                createData      = BuildCreateRequestDataFromCandidate(
                    item.Candidate, item.AppraisalId, snapshot, command);
                externalCaseKey = item.Candidate.CollateralId;
            }
            else
            {
                // ── (B) Appraisal-only path ───────────────────────────────────
                if (!item.AppraisalId.HasValue)
                {
                    logger.LogWarning(
                        "[REAPPRAISAL-INITIATE] Skipping nearby item with no AppraisalId (AppraisalNumber={Number})",
                        item.AppraisalNumber);
                    continue;
                }

                createData      = BuildCreateRequestDataFromAppraisal(
                    item.AppraisalId.Value, snapshot, command);
                externalCaseKey = item.AppraisalNumber;
            }

            // Manual create + submit (NO Validate — Appointment/Fee deliberately blank).
            var (request, _) = await createRequestService.CreateRequestAsync(createData, cancellationToken);

            if (!string.IsNullOrWhiteSpace(externalCaseKey))
                request.SetExternalReference(externalCaseKey, "SIBS");

            if (item.Candidate is not null)
                item.Candidate.MarkConsumed();  // Layer 2 fires here for NearbyAppraisalId-attached candidates too

            // Persist BEFORE Submit so RequestSubmittedEventHandler's DB query sees the committed
            // row (titles/documents lookups). Same pattern as CreateAndSubmitRequestAsync.
            await dbContext.SaveChangesAsync(cancellationToken);

            // groupNumber is passed as a transient hint to Submit so it flows through
            // RequestSubmittedEvent → RequestSubmittedIntegrationEvent →
            // AppraisalCreationRequestedIntegrationEvent → Appraisal.SetGroupTag.
            // It is NOT persisted on Request; the canonical home is appraisal.Appraisals.GroupTag.
            request.Submit(now, groupNumber);

            createdIds.Add(request.Id);
        }

        logger.LogInformation(
            "[REAPPRAISAL-INITIATE] Group {GroupNumber}: created {Count} request(s), skipped {SkipCount}",
            groupNumber, createdIds.Count, skipped.Count);

        return new InitiateReappraisalResult(groupNumber, createdIds, skipped);
    }

    // ── Data builders ─────────────────────────────────────────────────────────

    // FeePaymentType is a code from the PatFeeType parameter catalog. "07" = Bank Absorb default.
    private const string DefaultFeePaymentType = "07";
    private const string DefaultFeeRemark = "Periodical Reappraisal";

    /// <summary>Builds CreateRequestData for the candidate-row path, copying LoanDetail/Address/Contact/
    /// Customers/Properties from the prior Request snapshot when available. HasAppraisalBook is
    /// always false for reappraisal (the prior book is stale). Appointment is left blank — to be
    /// set by staff downstream. Fee defaults to <see cref="DefaultFeePaymentType"/>.</summary>
    private static CreateRequestData BuildCreateRequestDataFromCandidate(
        ReappraisalCandidate candidate,
        Guid? prevAppraisalId,
        PriorRequestSnapshot? snap,
        InitiateReappraisalCommand command)
    {
        // Prefer the prior request's customer list; fall back to the candidate's CIF name if missing.
        var customers = snap?.Customers is { Count: > 0 }
            ? snap.Customers.Select(c => new RequestCustomerDto(c.Name ?? string.Empty, c.ContactNumber)).ToList()
            : string.IsNullOrWhiteSpace(candidate.CifName)
                ? null
                : new List<RequestCustomerDto> { new(candidate.CifName, null) };

        var detail = new RequestDetailDto(
            HasAppraisalBook: false,    // always false for reappraisal — prior book is stale by definition
            LoanDetail: BuildLoanDetailDto(snap),
            PrevAppraisalId: prevAppraisalId,
            Address: BuildAddressDto(snap),
            Contact: BuildContactDto(snap),
            Appointment: null,          // intentionally blank — to be set by staff downstream
            Fee: BuildDefaultFeeDto()   // default to Bank Absorb
        );

        return BuildCreateRequestData(detail, customers, BuildPropertiesDto(snap), snap?.Titles, snap?.Documents, command);
    }

    /// <summary>Builds CreateRequestData for the appraisal-only path (no candidate row).</summary>
    private static CreateRequestData BuildCreateRequestDataFromAppraisal(
        Guid appraisalId,
        PriorRequestSnapshot? snap,
        InitiateReappraisalCommand command)
    {
        var customers = snap?.Customers is { Count: > 0 }
            ? snap.Customers.Select(c => new RequestCustomerDto(c.Name ?? string.Empty, c.ContactNumber)).ToList()
            : null;

        var detail = new RequestDetailDto(
            HasAppraisalBook: false,
            LoanDetail: BuildLoanDetailDto(snap),
            PrevAppraisalId: appraisalId,
            Address: BuildAddressDto(snap),
            Contact: BuildContactDto(snap),
            Appointment: null,
            Fee: BuildDefaultFeeDto()
        );

        return BuildCreateRequestData(detail, customers, BuildPropertiesDto(snap), snap?.Titles, snap?.Documents, command);
    }

    private static CreateRequestData BuildCreateRequestData(
        RequestDetailDto detail,
        List<RequestCustomerDto>? customers,
        List<RequestPropertyDto>? properties,
        List<RequestTitleDto>? titles,
        List<RequestDocumentDto>? documents,
        InitiateReappraisalCommand command) =>
        new(
            Purpose: ReappraisalPurposeCode,
            Channel: "SIBS",
            Requestor: command.Requestor,
            Creator: command.Creator,
            Priority: "Normal",
            IsPma: false,
            Detail: detail,
            Customers: customers,
            Properties: properties,
            Titles: titles,
            Documents: documents,
            Comments: null);

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

    private static List<RequestPropertyDto>? BuildPropertiesDto(PriorRequestSnapshot? snap) =>
        snap?.Properties is { Count: > 0 }
            ? snap.Properties.Select(p => new RequestPropertyDto(p.PropertyType, p.BuildingType, p.SellingPrice)).ToList()
            : null;

    private static FeeDto BuildDefaultFeeDto() =>
        new(FeePaymentType: DefaultFeePaymentType, FeeNotes: DefaultFeeRemark, AbsorbedAmount: null);

    // ── Dapper helpers ─────────────────────────────────────────────────────────

    /// <summary>Batch: SurveyNumber → AppraisalId for the candidate path.</summary>
    private async Task<Dictionary<string, Guid>> ResolvePrevAppraisalIdsAsync(
        IReadOnlyList<string> surveyNumbers,
        CancellationToken _)
    {
        if (surveyNumbers.Count == 0) return new Dictionary<string, Guid>();

        const string sql = """
            SELECT a.Id AS Id, a.AppraisalNumber AS SurveyNumber
            FROM appraisal.Appraisals a
            WHERE a.AppraisalNumber IN @SurveyNumbers
            """;

        var rows = await connectionFactory.GetOpenConnection()
            .QueryAsync<PrevAppraisalRow>(sql, new { SurveyNumbers = surveyNumbers });

        return rows.ToDictionary(r => r.SurveyNumber, r => r.Id);
    }

    /// <summary>Batch: AppraisalId → (AppraisalId, AppraisalNumber) for the nearby path.</summary>
    private async Task<IReadOnlyList<NearbyAppraisalRow>> FetchAppraisalNumbersAsync(
        IReadOnlyList<Guid> appraisalIds,
        CancellationToken _)
    {
        if (appraisalIds.Count == 0) return [];

        const string sql = """
            SELECT a.Id AS AppraisalId, a.AppraisalNumber
            FROM appraisal.Appraisals a
            WHERE a.Id IN @AppraisalIds
            """;

        var rows = await connectionFactory.GetOpenConnection()
            .QueryAsync<NearbyAppraisalRow>(sql, new { AppraisalIds = appraisalIds });

        return rows.ToList();
    }

    /// <summary>
    /// Fetches minimal customer data for an appraisal-only request (no candidate row).
    /// Returns null if not found — request is still created with Customer=null.
    /// TODO(confirm): confirm CustomerName source is vw_AppraisalList.CustomerName.
    /// </summary>
    private async Task<AppraisalDetailRow?> FetchAppraisalDetailAsync(
        Guid appraisalId,
        CancellationToken _)
    {
        const string sql = """
            SELECT al.CustomerName
            FROM appraisal.vw_AppraisalList al
            WHERE al.Id = @AppraisalId
            """;

        return await connectionFactory.GetOpenConnection()
            .QueryFirstOrDefaultAsync<AppraisalDetailRow>(sql, new { AppraisalId = appraisalId });
    }

    /// <summary>
    /// Layer 1 dedupe: returns AppraisalIds that are already referenced by ANY non-terminal Appraisal
    /// (an `appraisal.Appraisals` row whose `PrevAppraisalId` equals the given id and whose Status is
    /// not terminal) — regardless of AppraisalType (ReAppraisal, Progressive/CI, Appeal). Source of
    /// truth is the Appraisal table — not RequestDetails — so we block any duplicate in-flight work
    /// on the same prior appraisal even after the Request has been submitted.
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
    /// Batch-fetch the prior Request's reusable detail (LoanDetail / Address / Contact / customers)
    /// for the given AppraisalIds. Keyed by AppraisalId so the per-item builder can find the
    /// matching snapshot. Returns an empty dict when no inputs.
    /// </summary>
    private async Task<Dictionary<Guid, PriorRequestSnapshot>> FetchPriorRequestSnapshotsAsync(
        IReadOnlyList<Guid> appraisalIds,
        CancellationToken _)
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

        // Load titles (with their owned TitleDocuments) and request-level documents via EF Core.
        // OwnsMany navigation must be loaded through the owning aggregate — Dapper cannot hydrate
        // the polymorphic TPH hierarchy or owned collections reliably.
        var titlesByRequest = new Dictionary<Guid, List<RequestTitleDto>>();
        var documentsByRequest = new Dictionary<Guid, List<RequestDocumentDto>>();
        if (requestIds.Count > 0)
        {
            var titles = await dbContext.RequestTitles
                .Where(t => requestIds.Contains(t.RequestId))
                .ToListAsync(_);

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
                .ToListAsync(_);

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

    // ── Private record types ───────────────────────────────────────────────────

    private sealed record PrevAppraisalRow(Guid Id, string SurveyNumber);
    private sealed record NearbyAppraisalRow(Guid AppraisalId, string? AppraisalNumber);
    private sealed record AppraisalDetailRow(string? CustomerName);

    /// <summary>Flat row from the snapshot SQL — assembled into PriorRequestSnapshot post-fetch.</summary>
    private sealed record PriorRequestRow(
        Guid AppraisalId, Guid RequestId, bool HasAppraisalBook,
        string? BankingSegment, string? LoanApplicationNumber, decimal? FacilityLimit,
        decimal? AdditionalFacilityLimit, decimal? PreviousFacilityLimit, decimal? TotalSellingPrice,
        string? HouseNumber, string? ProjectName, string? Moo, string? Soi, string? Road,
        string? SubDistrict, string? District, string? Province, string? Postcode,
        string? ContactPersonName, string? ContactPersonPhone, string? DealerCode);

    private sealed record PriorRequestCustomerRow(Guid RequestId, string? Name, string? ContactNumber);
    private sealed record PriorRequestPropertyRow(Guid RequestId, string? PropertyType, string? BuildingType, decimal? SellingPrice);

    /// <summary>Reusable snapshot of the prior Request, used to seed the new reappraisal Request.</summary>
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

    /// <summary>
    /// Internal working item pairing an optional AppraisalId with an optional Candidate.
    /// AppraisalNumber is used for logging and SkippedReappraisalItem.
    /// </summary>
    private sealed record WorkingItem(
        Guid? AppraisalId,
        string? AppraisalNumber,
        ReappraisalCandidate? Candidate);
}
