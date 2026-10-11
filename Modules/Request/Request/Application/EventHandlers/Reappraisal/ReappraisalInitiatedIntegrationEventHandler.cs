using Dapper;
using MassTransit;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Events;
using Shared.Messaging.Filters;

namespace Request.Application.EventHandlers.Reappraisal;

/// <summary>
/// Creates one reappraisal <c>Request</c> for each
/// <see cref="ReappraisalInitiatedIntegrationEvent"/> published by the Collateral module, and
/// leaves it in the request list — it does NOT submit. Staff review it (and, for a legacy AS400 book
/// with nothing to copy, fill in the property) and submit it themselves.
///
/// Idempotency key: GroupNumber + (PrevAppraisalId ?? SurveyNumber).
/// <see cref="InboxGuard{TDbContext}"/> deduplicates retried/concurrent delivery.
///
/// The handler:
///   1. Fetches what the request starts with: the prior request snapshot (LoanDetail / Address /
///      Contact / Customers / Properties / Titles / Documents) keyed on PrevAppraisalId — or, for a
///      block-project unit, the project and the matched unit in CAS (never the project's request).
///   2. Builds <see cref="CreateRequestData"/> and calls <see cref="ICreateRequestService.CreateRequestAsync"/>.
///   3. Stamps the batch GroupNumber, the book and AS400's collateral id (system fields; the group
///      reaches Appraisal.GroupTag whenever staff submit) — and, for a unit, the unit's prior value.
///   4. Leaves it Draft for staff to complete (appointment; owner/deed for a unit; property for a
///      legacy book) and submit.
/// </summary>
public class ReappraisalInitiatedIntegrationEventHandler(
    RequestDbContext dbContext,
    ICreateRequestService createRequestService,
    IRequestUnitOfWork unitOfWork,
    ISqlConnectionFactory connectionFactory,
    ISender mediator,
    InboxGuard<RequestDbContext> inboxGuard,
    ILogger<ReappraisalInitiatedIntegrationEventHandler> logger)
    : IConsumer<ReappraisalInitiatedIntegrationEvent>
{
    // FeePaymentType "07" = Bank Absorb.
    private const string DefaultFeePaymentType = "07";
    private const string DefaultFeeRemark = "Periodical Reappraisal";
    private const string ReappraisalPurposeCode = "03";

    // RunOnceAsync, not TryClaim + MarkAsProcessed: a throw after a bare claim left it "Processing", so the
    // bus retry skipped and acked the message and the book silently got no request.
    public Task Consume(ConsumeContext<ReappraisalInitiatedIntegrationEvent> context) =>
        inboxGuard.RunOnceAsync(context.MessageId, GetType().Name, _ => HandleAsync(context), context.CancellationToken);

    private async Task HandleAsync(ConsumeContext<ReappraisalInitiatedIntegrationEvent> context)
    {
        var msg = context.Message;
        var ct  = context.CancellationToken;

        // Initiate sends the normalised book and a real PrevAppraisalId; a message published by the
        // previous Initiate (still in the outbox at deploy) may carry AS400's raw 'B'-prefixed number and
        // Guid.Empty. Normalised here so it keys the same as every other book check.
        var book   = NormalizeBook(msg.SurveyNumber);
        var prevId = msg.PrevAppraisalId == Guid.Empty ? null : msg.PrevAppraisalId;

        logger.LogInformation(
            "[REAPPRAISAL-CONSUMER] Handling event for GroupNumber={GroupNumber} Source={Source} PrevAppraisalId={PrevId}",
            msg.GroupNumber, msg.Source, prevId);

        // ── One waiting request per book ──────────────────────────────────────
        // Initiate does not consume the candidate (that happens on submit), so two Initiates of the
        // same book inside the outbox delay publish it under two group numbers, and the inbox key
        // cannot tell them apart. The waiting request is what marks the book taken: if one exists,
        // this message is a duplicate.
        // ponytail: check-then-create, so two consumer instances handling both messages in the same
        // instant can still both create. Initiate's confirm button is disabled while it runs, which
        // leaves two users initiating the same book within seconds; close it with an app lock per
        // book if that is ever seen.
        // Keyed by the book alone, like Initiate: one request covers every collateral of the book.
        // Every request Initiate creates must carry its book: it is what the waiting / in-progress checks
        // and consumption on submit key on. A message without one cannot be tracked, so it is dropped.
        if (book is null)
        {
            logger.LogError(
                "[REAPPRAISAL-CONSUMER] Message for GroupNumber={GroupNumber} carries no book number; no request created",
                msg.GroupNumber);
            return;
        }

        // A block-project unit is reviewed per AS400 collateral: another unit of the same project
        // being under review does not make this one a duplicate.
        var unitCollateralId = msg.IsBlockUnit ? msg.CollateralId : null;
        if (await IsBookUnderReviewAsync(book, unitCollateralId, ct))
        {
            logger.LogWarning(
                "[REAPPRAISAL-CONSUMER] Book {Book} of collateral {CollateralId} is already under review (waiting request or open reappraisal); skipping GroupNumber={GroupNumber}",
                book, msg.CollateralId, msg.GroupNumber);
            return;
        }

        // ── Fetch prior request snapshot ──────────────────────────────────────
        // A block-project unit does not copy the project's request (every title and document of the
        // whole project): it is filled from the project and the matched unit in CAS instead.
        PriorRequestSnapshot? snapshot = null;
        UnitSource? unit = null;
        if (prevId.HasValue && msg.IsBlockUnit)
            unit = await FetchUnitSourceAsync(prevId.Value, msg.ProjectUnitId, ct);
        else if (prevId.HasValue)
            snapshot = await FetchPriorRequestSnapshotAsync(prevId.Value, ct);

        // ── Build CreateRequestData ────────────────────────────────────────────
        var customers = BuildCustomers(snapshot);
        var detail = new RequestDetailDto(
            HasAppraisalBook: false,
            LoanDetail: BuildLoanDetailDto(snapshot),
            PrevAppraisalId: prevId,
            Address: unit?.Address ?? BuildAddressDto(snapshot),
            Contact: BuildContactDto(snapshot),
            Appointment: null,
            Fee: new FeeDto(FeePaymentType: DefaultFeePaymentType, FeeNotes: DefaultFeeRemark, AbsorbedAmount: null));

        // Documents follow the same carry-forward rule as the request page: the prior appraisal's defaultUse
        // files, stamped PREV. A block-project unit copies none (it never copied the whole project's), and a
        // book with no prior in CAS, or one that is not Completed, carries none — the draft is still created.
        var carried = unit is null
            ? await CarriedDocuments.TryFetchAsync(mediator, prevId, logger, ct)
            : null;
        var placeholders = snapshot is null
            ? []
            : await CarriedDocuments.LoadPlaceholdersAsync(connectionFactory, snapshot.RequestId, ct);
        var (carriedDocuments, carriedTitles) = CarriedDocuments.Build(carried, snapshot?.Titles, placeholders);

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
            Titles: unit is not null ? [unit.Title] : carriedTitles,
            Documents: carriedDocuments,
            Comments: null);

        // ── Create + persist (no submit) ──────────────────────────────────────
        var (request, _) = await createRequestService.CreateRequestAsync(createData, ct);

        // Kept on the request: staff submit later, and Submit hands it to Appraisal.GroupTag. The book and
        // AS400's collateral id are system fields — not ExternalCaseKey/ExternalSystem, which name a case
        // in an external system and drive the LOS webhooks.
        request.MarkAsPeriodicalReappraisal(msg.GroupNumber, book, msg.CollateralId);

        // A unit's prior value is its own price in the project appraisal, not the whole project's.
        if (unit is not null)
            request.SetReappraisalPriorValue(unit.Price, unit.ValuationDate);

        // A prior book that is not an appraisal in CAS (legacy AS400 "99A…"): only its number and the
        // value/date from the bank's listing (appraisal.AS400ReportListing), when it has one. Set through the domain, never through the create DTO, so no other
        // caller of CreateRequestService can plant one.
        if (!prevId.HasValue && !string.IsNullOrWhiteSpace(msg.PrevAppraisalNumber))
            request.SetLegacyPriorBook(msg.PrevAppraisalNumber, msg.PrevAppraisalValue, msg.PrevAppraisalDate);

        // Left as Draft: staff complete it before sending — at least the appointment, which nothing here
        // can supply, and for a legacy AS400 book the property too (there is no prior request to copy).

        // Persist via the Request UoW — NOT plain dbContext.SaveChangesAsync: RequestNumber is
        // generated ONLY inside RequestUnitOfWork.SaveChangesAsync (it stamps Added requests).
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "[REAPPRAISAL-CONSUMER] Created Request {RequestId} ({RequestNumber}, {Status}) for GroupNumber={GroupNumber}; awaiting manual submit",
            request.Id, request.RequestNumber?.ToString(), request.Status.Code, msg.GroupNumber);
    }

    /// <summary>
    /// The book is already under review: a request for it still waiting, or an open reappraisal of it
    /// (a message delayed past the first request's submit would otherwise slip through). Same rule as
    /// Initiate — appraisal.vw_ReappraisalsByBook and request.vw_WaitingReappraisalRequests.
    /// </summary>
    private async Task<bool> IsBookUnderReviewAsync(string bookNumber, string? unitCollateralId, CancellationToken _)
    {
        const string sql = """
            SELECT CASE WHEN EXISTS (
                       SELECT 1 FROM request.vw_WaitingReappraisalRequests w
                       WHERE w.BookNumber = @BookNumber
                         AND (@CollateralId IS NULL OR w.CollateralId = @CollateralId))
                     OR EXISTS (
                       SELECT 1 FROM appraisal.vw_ReappraisalsByBook rb
                       WHERE rb.BookNumber = @BookNumber
                         AND rb.Status NOT IN ('Completed', 'Cancelled')
                         AND (@CollateralId IS NULL OR rb.CollateralId = @CollateralId))
                   THEN 1 ELSE 0 END
            """;
        return await connectionFactory.GetOpenConnection()
            .ExecuteScalarAsync<bool>(sql, new { BookNumber = bookNumber, CollateralId = unitCollateralId });
    }

    // ── Block-project unit ────────────────────────────────────────────────────

    /// <summary>
    /// What a block-project unit's request starts with, from CAS only: the project (type, name,
    /// address) and, when one was matched, the unit (tower/floor/room or house/plot, areas) with its
    /// appraised price in the project appraisal. Owner, deed and documents are left to staff — the draft is
    /// completed on the request page. Null when the appraisal is not a project.
    /// </summary>
    private async Task<UnitSource?> FetchUnitSourceAsync(Guid projectAppraisalId, Guid? projectUnitId, CancellationToken _)
    {
        const string projectSql = """
            SELECT TOP 1 p.Id, p.ProjectType, p.ProjectName, p.HouseNumber, p.Road, p.Soi,
                         p.SubDistrict, p.District, p.Province, p.Postcode, va.ValuationDate
            FROM appraisal.Projects p
            LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = p.AppraisalId
            WHERE p.AppraisalId = @AppraisalId
            """;
        const string unitSql = """
            SELECT u.TowerName, u.Floor, u.RoomNumber, u.CondoRegistrationNumber, u.UsableArea,
                   u.HouseNumber, u.LandArea, m.BuildingType, pr.TotalAppraisalValueRounded AS Price
            FROM appraisal.ProjectUnits u
            LEFT JOIN appraisal.ProjectModels m ON m.Id = u.ProjectModelId
            LEFT JOIN appraisal.ProjectUnitPrices pr ON pr.ProjectUnitId = u.Id
            WHERE u.Id = @UnitId AND u.ProjectId = @ProjectId
            """;

        var conn = connectionFactory.GetOpenConnection();
        var p = await conn.QueryFirstOrDefaultAsync<ProjectRow>(projectSql, new { AppraisalId = projectAppraisalId });
        if (p is null) return null;
        var u = projectUnitId is { } unitId
            ? await conn.QueryFirstOrDefaultAsync<UnitRow>(unitSql, new { UnitId = unitId, ProjectId = p.Id })
            : null;

        var address = new AddressDto(
            u?.HouseNumber ?? p.HouseNumber, p.ProjectName, null, p.Soi, p.Road,
            p.SubDistrict, p.District, p.Province, p.Postcode);

        // The ordinary collateral types, not the project ones: the unit is reviewed as one condo room,
        // one house, or one plot.
        var title = p.ProjectType switch
        {
            "U" => new RequestTitleDto
            {
                CollateralType = "08",
                CondoName = p.ProjectName,
                BuildingNumber = u?.TowerName,
                FloorNumber = u?.Floor?.ToString(),
                RoomNumber = u?.RoomNumber,
                CondoRegistrationNumber = u?.CondoRegistrationNumber,
                UsableArea = u?.UsableArea,
                TitleAddress = address,
                DopaAddress = address,
                Documents = [],
            },
            "L" => new RequestTitleDto
            {
                CollateralType = "01",
                AreaSquareWa = u?.LandArea,
                TitleAddress = address,
                DopaAddress = address,
                Documents = [],
            },
            _ => new RequestTitleDto
            {
                CollateralType = "02",
                BuildingType = u?.BuildingType,
                UsableArea = u?.UsableArea,
                AreaSquareWa = u?.LandArea,
                TitleAddress = address,
                DopaAddress = address,
                Documents = [],
            },
        };

        return new UnitSource(address, title, u?.Price, p.ValuationDate);
    }

    private sealed record UnitSource(AddressDto Address, RequestTitleDto Title, decimal? Price, DateTime? ValuationDate);

    private sealed class ProjectRow
    {
        public Guid Id { get; set; }
        public string? ProjectType { get; set; }
        public string? ProjectName { get; set; }
        public string? HouseNumber { get; set; }
        public string? Road { get; set; }
        public string? Soi { get; set; }
        public string? SubDistrict { get; set; }
        public string? District { get; set; }
        public string? Province { get; set; }
        public string? Postcode { get; set; }
        public DateTime? ValuationDate { get; set; }
    }

    private sealed class UnitRow
    {
        public string? TowerName { get; set; }
        public int? Floor { get; set; }
        public string? RoomNumber { get; set; }
        public string? CondoRegistrationNumber { get; set; }
        public decimal? UsableArea { get; set; }
        public string? HouseNumber { get; set; }
        public decimal? LandArea { get; set; }
        public string? BuildingType { get; set; }
        public decimal? Price { get; set; }
    }

    /// <summary>The book as CAS stores it — same rule as Collateral's As400AppraisalNumber.Normalize.</summary>
    private static string? NormalizeBook(string? surveyNumber)
    {
        if (string.IsNullOrWhiteSpace(surveyNumber)) return null;
        var s = surveyNumber.Trim(' ').ToUpperInvariant();
        return s.Length > 1 && s[0] == 'B' ? s[1..] : s;
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
            SELECT RequestId, PropertyType, BuildingType, BuildingTypeOther, SellingPrice
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

        // Load titles via EF Core — owned collections require it. Documents are not copied (see CarriedDocuments).
        var titles = await dbContext.RequestTitles
            .Where(t => t.RequestId == row.RequestId)
            .InDisplayOrder()
            .ToListAsync(cancellationToken);

        var titleDtos = titles.Select(t => t.ToDto()).ToList();

        return new PriorRequestSnapshot(
            row.AppraisalId, row.RequestId, row.HasAppraisalBook,
            row.BankingSegment, row.LoanApplicationNumber, row.FacilityLimit,
            row.AdditionalFacilityLimit, row.PreviousFacilityLimit, row.TotalSellingPrice,
            row.HouseNumber, row.ProjectName, row.Moo, row.Soi, row.Road,
            row.SubDistrict, row.District, row.Province, row.Postcode,
            row.ContactPersonName, row.ContactPersonPhone, row.DealerCode,
            customers, properties, titleDtos);
    }

    // ── Data builders ─────────────────────────────────────────────────────────

    // Customers come from the prior request in CAS only — never from the AS400 file (decided
    // 2026-10-01): a book with no prior request starts with none, and staff add them.
    private static List<RequestCustomerDto>? BuildCustomers(PriorRequestSnapshot? snap) =>
        snap?.Customers is { Count: > 0 }
            ? snap.Customers.Select(c => new RequestCustomerDto(c.Name ?? string.Empty, c.ContactNumber)).ToList()
            : null;

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
                .Select(p => new RequestPropertyDto(p.PropertyType, p.BuildingType, p.BuildingTypeOther, p.SellingPrice))
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
    private sealed record PriorRequestPropertyRow(Guid RequestId, string? PropertyType, string? BuildingType, string? BuildingTypeOther, decimal? SellingPrice);

    private sealed record PriorRequestSnapshot(
        Guid AppraisalId, Guid RequestId, bool HasAppraisalBook,
        string? BankingSegment, string? LoanApplicationNumber, decimal? FacilityLimit,
        decimal? AdditionalFacilityLimit, decimal? PreviousFacilityLimit, decimal? TotalSellingPrice,
        string? HouseNumber, string? ProjectName, string? Moo, string? Soi, string? Road,
        string? SubDistrict, string? District, string? Province, string? Postcode,
        string? ContactPersonName, string? ContactPersonPhone, string? DealerCode,
        List<PriorRequestCustomerRow> Customers,
        List<PriorRequestPropertyRow> Properties,
        List<RequestTitleDto> Titles);
}
