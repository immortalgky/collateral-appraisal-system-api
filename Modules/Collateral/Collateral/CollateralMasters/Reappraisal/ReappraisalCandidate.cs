namespace Collateral.CollateralMasters.Reappraisal;

/// <summary>
/// Staged reappraisal candidate ingested from the AS400 COLLATREV extract.
///
/// Each row corresponds to one 649-char Detail record in the monthly
/// AS400_COLLATREV_YYYYMMDD.txt file (Collateral Review Interface).
///
/// One row per BOOK — (<see cref="CollateralId"/>, <see cref="NormalizedSurveyNumber"/>) — across every
/// monthly file. AS400 does not know what CAS has already reviewed, so it keeps sending the same books
/// month after month; each later file refreshes the row instead of adding another one.
///
/// Lifecycle:
///   Pending  → Consumed  when the reappraisal request is submitted.
///   Pending  → Deleted   staff chose "not reviewing this round"; shown on its own tab.
///   Deleted  → Pending   staff restored it.
///   Consumed → Pending   a later file still lists the book but the reappraisal it produced was
///                        cancelled, so nothing is reviewing it any more.
/// </summary>
public class ReappraisalCandidate
{
    // ── Ingestion metadata ────────────────────────────────────────────────────

    public Guid Id { get; private set; }

    /// <summary>The source filename (e.g. AS400_COLLATREV_20260501.txt).</summary>
    public string SourceFileName { get; private set; } = default!;

    /// <summary>Date of the file this book FIRST appeared in (YYYYMMDD portion of its name).</summary>
    public DateOnly SourceFileDate { get; private set; }

    /// <summary>
    /// Date of the latest file that listed this book. A Pending or Deleted book missing from the latest
    /// file is no longer due and is hidden. Not advanced for Consumed books — a book already reviewed is
    /// skipped outright when AS400 repeats it. NULL on rows ingested before this column existed; readers
    /// fall back to <see cref="SourceFileDate"/>.
    /// </summary>
    public DateOnly? LastSeenFileDate { get; private set; }

    /// <summary>EffectiveDate from the Header record (DDMMYYYY pos 2–9).</summary>
    public DateOnly EffectiveDate { get; private set; }

    /// <summary>When this row was written to the DB.</summary>
    public DateTime IngestedAt { get; private set; }

    /// <summary>SHA-256 hex of the raw 649-char line. Used to skip unchanged rows on re-ingest.</summary>
    public string RowHash { get; private set; } = default!;

    public ReappraisalCandidateStatus Status { get; private set; }

    // ── Fixed-width Detail fields (pos 2–649) ────────────────────────────────

    /// <summary>ReviewType: 1 = Normal, 2 = Before Stage 3, 3 = Stage 3 (pos 2).</summary>
    public string ReviewType { get; private set; } = default!;

    /// <summary>AS400 review date on the normal 5-year cycle (pos 3–10, DDMMYYYY). Not the due date —
    /// that is <see cref="EffectiveDateAppraisal"/> (vw_ReappraisalCandidates.DueDate).</summary>
    public DateOnly ReviewDate { get; private set; }

    /// <summary>Bank collateral ID (dec19, pos 11–29).</summary>
    public string CollateralId { get; private set; } = default!;

    /// <summary>
    /// AS400 "CCSURV" / Appraisal Report Number = our appraisal.Appraisals.AppraisalNumber.
    /// This is the key link to the in-system prior appraisal (pos 30–39).
    /// FSD calls it "Old Appraisal Report No".
    /// </summary>
    public string SurveyNumber { get; private set; } = default!;

    /// <summary>
    /// <see cref="SurveyNumber"/> as CAS stores it (<see cref="As400AppraisalNumber.Normalize"/>). The key
    /// every lookup uses; <see cref="SurveyNumber"/> keeps the raw value for audit. NULL only on rows
    /// ingested before this column existed and not yet backfilled.
    /// </summary>
    public string? NormalizedSurveyNumber { get; private set; }

    /// <summary>
    /// The book is a block-project appraisal — the number AS400 sent (with or without 'B', or a unit
    /// ticket) names one UNIT of the project. Such a review is per collateral, not per book: other units
    /// of the same project are reviewed separately. A ticket's <see cref="NormalizedSurveyNumber"/> is
    /// the project's appraisal number; the ticket stays in <see cref="SurveyNumber"/> to find the unit.
    /// </summary>
    public bool IsBlockUnit { get; private set; }

    /// <summary>Collateral type code e.g. "11A" (pos 40–42).</summary>
    public string CollateralCode { get; private set; } = default!;

    /// <summary>Collateral category e.g. "RE" (pos 43–47).</summary>
    public string CollateralCategory { get; private set; } = default!;

    /// <summary>Collateral name (pos 48–87, 40 chars).</summary>
    public string? CollateralName { get; private set; }

    /// <summary>Collateral address (pos 88–207, 120 chars).</summary>
    public string? CollateralAddress { get; private set; }

    /// <summary>CIF number / customer ID (dec19, pos 208–226).</summary>
    public string CifNumber { get; private set; } = default!;

    /// <summary>CIF name / customer name (pos 227–246, 20 chars).</summary>
    public string? CifName { get; private set; }

    /// <summary>AO (Account Officer) code (pos 247–256).</summary>
    public string? AoCode { get; private set; }

    /// <summary>AO name (pos 257–276, 20 chars).</summary>
    public string? AoName { get; private set; }

    /// <summary>Title number (pos 277–296).</summary>
    public string? TitleNumber { get; private set; }

    /// <summary>Last appraised value (dec15,2; pos 297–311).</summary>
    public decimal? CurrentValue { get; private set; }

    /// <summary>Last valuation date (DDMMYYYY, pos 312–319).</summary>
    public DateOnly? ValuationDate { get; private set; }

    /// <summary>Internal/External flag: "I" or "E" (pos 320).</summary>
    public string? InternalExternal { get; private set; }

    /// <summary>Business size code (pos 321).</summary>
    public string? BusinessSize { get; private set; }

    /// <summary>Business size description (pos 322–341, 20 chars).</summary>
    public string? BusinessSizeDesc { get; private set; }

    /// <summary>Mortgage amount (dec15,2; pos 342–356).</summary>
    public decimal? MortgageAmount { get; private set; }

    /// <summary>Past due days (dec5; pos 357–361).</summary>
    public int? PastDueDay { get; private set; }

    /// <summary>Loan application number (dec19; pos 362–380).</summary>
    public string? ApplicationNumber { get; private set; }

    /// <summary>Facility code (pos 381–383).</summary>
    public string? FacilityCode { get; private set; }

    /// <summary>Facility sequence (dec19; pos 384–402).</summary>
    public string? FacilitySequence { get; private set; }

    /// <summary>CP number (pos 403–418).</summary>
    public string? CpNumber { get; private set; }

    /// <summary>CAR code (pos 419–421).</summary>
    public string? CarCode { get; private set; }

    /// <summary>Facility limit (dec15,2; pos 422–436).</summary>
    public decimal? FacilityLimit { get; private set; }

    /// <summary>Flag: ageing price less than 4 years (pos 437).</summary>
    public string? FlagLessAge4Y { get; private set; }

    /// <summary>Flag: ageing price greater than 4 years (pos 438).</summary>
    public string? FlagGreaterAge4Y { get; private set; }

    /// <summary>Count ageing date string e.g. "4/9" (pos 439–448).</summary>
    public string? CountAgeingDate { get; private set; }

    /// <summary>Collateral description (pos 449–498).</summary>
    public string? CollateralDescription { get; private set; }

    /// <summary>External valuer name (pos 499–538).</summary>
    public string? ExternalValuerName { get; private set; }

    /// <summary>Internal valuer name (pos 539–578).</summary>
    public string? InternalValuerName { get; private set; }

    /// <summary>"Y"/"N" — SLL outstanding over 100 M (pos 579).</summary>
    public string? SllOver100M { get; private set; }

    /// <summary>SLL description (pos 580–629).</summary>
    public string? SllDescription { get; private set; }

    // ── Trailing extension fields (pos 630–649) ───────────────────────────────────

    /// <summary>CIF stage indicator e.g. "1"/"2"/"3" (pos 630).</summary>
    public string? Stage { get; private set; }

    /// <summary>Banking segment (e.g. RB / IBG) (pos 631–640, 10 chars).</summary>
    public string? IBGRetail { get; private set; }

    /// <summary>Review group code 1/2/3 matching ReviewType (pos 641).</summary>
    public string? Group { get; private set; }

    /// <summary>Review due date AS400 sets per book (pos 642–649, DDMMYYYY): sooner than ReviewDate when the book
    /// falls into a stage (Stage 2/3: 3 years). The due date the list counts down to; none when not sent.</summary>
    public DateOnly? EffectiveDateAppraisal { get; private set; }

    // ── Enrichment (populated post-ingest via SurveyNumber→AppraisalNumber join) ─

    /// <summary>Latitude from in-system appraisal detail coords. NULL when no match.</summary>
    public decimal? Latitude { get; private set; }

    /// <summary>Longitude from in-system appraisal detail coords. NULL when no match.</summary>
    public decimal? Longitude { get; private set; }

    // GeoPoint is a persisted computed column (geography::Point) added by raw-SQL migration.
    // It is NOT mapped as an EF property (no NetTopologySuite dependency here) — used
    // only in Dapper spatial queries via STDistance on the DB side.

    private ReappraisalCandidate() { /* EF Core */ }

    public static ReappraisalCandidate Create(
        string sourceFileName,
        DateOnly sourceFileDate,
        DateOnly effectiveDate,
        DateTime ingestedAt,
        string rowHash,
        string reviewType,
        DateOnly reviewDate,
        string collateralId,
        string surveyNumber,
        string collateralCode,
        string collateralCategory,
        string? collateralName,
        string? collateralAddress,
        string cifNumber,
        string? cifName,
        string? aoCode,
        string? aoName,
        string? titleNumber,
        decimal? currentValue,
        DateOnly? valuationDate,
        string? internalExternal,
        string? businessSize,
        string? businessSizeDesc,
        decimal? mortgageAmount,
        int? pastDueDay,
        string? applicationNumber,
        string? facilityCode,
        string? facilitySequence,
        string? cpNumber,
        string? carCode,
        decimal? facilityLimit,
        string? flagLessAge4Y,
        string? flagGreaterAge4Y,
        string? countAgeingDate,
        string? collateralDescription,
        string? externalValuerName,
        string? internalValuerName,
        string? sllOver100M,
        string? sllDescription,
        string? stage,
        string? ibgRetail,
        string? @group,
        DateOnly? effectiveDateAppraisal)
    {
        return new ReappraisalCandidate
        {
            Id = Guid.CreateVersion7(),
            SourceFileName = sourceFileName,
            SourceFileDate = sourceFileDate,
            EffectiveDate = effectiveDate,
            IngestedAt = ingestedAt,
            RowHash = rowHash,
            Status = ReappraisalCandidateStatus.Pending,
            LastSeenFileDate = sourceFileDate,
            NormalizedSurveyNumber = As400AppraisalNumber.Normalize(surveyNumber),
            ReviewType = reviewType,
            ReviewDate = reviewDate,
            CollateralId = collateralId,
            SurveyNumber = surveyNumber,
            CollateralCode = collateralCode,
            CollateralCategory = collateralCategory,
            CollateralName = collateralName,
            CollateralAddress = collateralAddress,
            CifNumber = cifNumber,
            CifName = cifName,
            AoCode = aoCode,
            AoName = aoName,
            TitleNumber = titleNumber,
            CurrentValue = currentValue,
            ValuationDate = valuationDate,
            InternalExternal = internalExternal,
            BusinessSize = businessSize,
            BusinessSizeDesc = businessSizeDesc,
            MortgageAmount = mortgageAmount,
            PastDueDay = pastDueDay,
            ApplicationNumber = applicationNumber,
            FacilityCode = facilityCode,
            FacilitySequence = facilitySequence,
            CpNumber = cpNumber,
            CarCode = carCode,
            FacilityLimit = facilityLimit,
            FlagLessAge4Y = flagLessAge4Y,
            FlagGreaterAge4Y = flagGreaterAge4Y,
            CountAgeingDate = countAgeingDate,
            CollateralDescription = collateralDescription,
            ExternalValuerName = externalValuerName,
            InternalValuerName = internalValuerName,
            SllOver100M = sllOver100M,
            SllDescription = sllDescription,
            Stage = stage,
            IBGRetail = ibgRetail,
            Group = @group,
            EffectiveDateAppraisal = effectiveDateAppraisal
        };
    }

    /// <summary>Marks this book as reviewed — its reappraisal request has been submitted.</summary>
    public void MarkConsumed()
    {
        Status = ReappraisalCandidateStatus.Consumed;
    }

    /// <summary>"Not reviewing this round" — moves the book to its own tab until staff restore it.</summary>
    public void MarkDeleted()
    {
        Status = ReappraisalCandidateStatus.Deleted;
    }

    /// <summary>
    /// Back to the to-do list: staff restored a book they had skipped, or the reappraisal a consumed
    /// book produced was cancelled.
    /// </summary>
    public void MarkPending()
    {
        Status = ReappraisalCandidateStatus.Pending;
    }

    /// <summary>
    /// The book as resolved by the ingestor (a ticket becomes its project's appraisal number), and the
    /// number exactly as this file sent it — a unit sent as "B…" one month may come as its ticket the
    /// next, and the ticket is what finds the unit (collateral.vw_ReappraisalCandidateUnits).
    /// </summary>
    public void SetBook(string bookNumber, bool isBlockUnit, string surveyNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(surveyNumber);
        NormalizedSurveyNumber = bookNumber;
        IsBlockUnit = isBlockUnit;
        SurveyNumber = surveyNumber;
    }

    /// <summary>Records that a file dated <paramref name="fileDate"/> listed this book.</summary>
    public void MarkSeen(DateOnly fileDate)
    {
        if (LastSeenFileDate is null || fileDate > LastSeenFileDate)
            LastSeenFileDate = fileDate;
    }

    /// <summary>
    /// Enriches the candidate with geo-coordinates resolved from the in-system appraisal
    /// matched by SurveyNumber → AppraisalNumber. Called by the ingestion job post-parse.
    /// </summary>
    public void SetCoordinates(decimal latitude, decimal longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>
    /// Refreshes the book with a later file's values. Status is left alone — a skipped book stays
    /// skipped however many times AS400 repeats it.
    /// </summary>
    public void UpdateFrom(
        string rowHash,
        DateOnly effectiveDate,
        string reviewType,
        DateOnly reviewDate,
        string collateralCode,
        string collateralCategory,
        string? collateralName,
        string? collateralAddress,
        string? cifName,
        string? aoCode,
        string? aoName,
        string? titleNumber,
        decimal? currentValue,
        DateOnly? valuationDate,
        string? internalExternal,
        string? businessSize,
        string? businessSizeDesc,
        decimal? mortgageAmount,
        int? pastDueDay,
        string? applicationNumber,
        string? facilityCode,
        string? facilitySequence,
        string? cpNumber,
        string? carCode,
        decimal? facilityLimit,
        string? flagLessAge4Y,
        string? flagGreaterAge4Y,
        string? countAgeingDate,
        string? collateralDescription,
        string? externalValuerName,
        string? internalValuerName,
        string? sllOver100M,
        string? sllDescription,
        string? stage,
        string? ibgRetail,
        string? @group,
        DateOnly? effectiveDateAppraisal)
    {
        RowHash = rowHash;
        EffectiveDate = effectiveDate;
        ReviewType = reviewType;
        ReviewDate = reviewDate;
        CollateralCode = collateralCode;
        CollateralCategory = collateralCategory;
        CollateralName = collateralName;
        CollateralAddress = collateralAddress;
        CifName = cifName;
        AoCode = aoCode;
        AoName = aoName;
        TitleNumber = titleNumber;
        CurrentValue = currentValue;
        ValuationDate = valuationDate;
        InternalExternal = internalExternal;
        BusinessSize = businessSize;
        BusinessSizeDesc = businessSizeDesc;
        MortgageAmount = mortgageAmount;
        PastDueDay = pastDueDay;
        ApplicationNumber = applicationNumber;
        FacilityCode = facilityCode;
        FacilitySequence = facilitySequence;
        CpNumber = cpNumber;
        CarCode = carCode;
        FacilityLimit = facilityLimit;
        FlagLessAge4Y = flagLessAge4Y;
        FlagGreaterAge4Y = flagGreaterAge4Y;
        CountAgeingDate = countAgeingDate;
        CollateralDescription = collateralDescription;
        ExternalValuerName = externalValuerName;
        InternalValuerName = internalValuerName;
        SllOver100M = sllOver100M;
        SllDescription = sllDescription;
        Stage = stage;
        IBGRetail = ibgRetail;
        Group = @group;
        EffectiveDateAppraisal = effectiveDateAppraisal;
        NormalizedSurveyNumber ??= As400AppraisalNumber.Normalize(SurveyNumber);
    }
}
