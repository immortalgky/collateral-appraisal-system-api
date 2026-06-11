namespace Collateral.CollateralMasters.Reappraisal;

/// <summary>
/// Staged reappraisal candidate ingested from the AS400 COLLATREV extract.
///
/// Each row corresponds to one 649-char Detail record in the monthly
/// AS400_COLLATREV_YYYYMMDD.txt file (Collateral Review Interface).
///
/// Lifecycle: Pending → Consumed (initiate called) | Deleted (staff deleted it).
/// </summary>
public class ReappraisalCandidate
{
    // ── Ingestion metadata ────────────────────────────────────────────────────

    public Guid Id { get; private set; }

    /// <summary>The source filename (e.g. AS400_COLLATREV_20260501.txt).</summary>
    public string SourceFileName { get; private set; } = default!;

    /// <summary>Date parsed from the filename (YYYYMMDD portion).</summary>
    public DateOnly SourceFileDate { get; private set; }

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

    /// <summary>AS400-provided due date for reappraisal — the list "Appraisal Date" (pos 3–10, DDMMYYYY).</summary>
    public DateOnly ReviewDate { get; private set; }

    /// <summary>Bank collateral ID (dec19, pos 11–29).</summary>
    public string CollateralId { get; private set; } = default!;

    /// <summary>
    /// AS400 "CCSURV" / Appraisal Report Number = our appraisal.Appraisals.AppraisalNumber.
    /// This is the key link to the in-system prior appraisal (pos 30–39).
    /// FSD calls it "Old Appraisal Report No".
    /// </summary>
    public string SurveyNumber { get; private set; } = default!;

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

    /// <summary>Effective appraisal date (pos 642–649, DDMMYYYY).</summary>
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

    /// <summary>Marks this candidate as initiated into reappraisal requests.</summary>
    public void MarkConsumed()
    {
        Status = ReappraisalCandidateStatus.Consumed;
    }

    /// <summary>Soft-deletes this candidate from the list.</summary>
    public void MarkDeleted()
    {
        Status = ReappraisalCandidateStatus.Deleted;
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
    /// Updates the RowHash (and any changed field values) when a re-ingested file
    /// has different content for the same (SourceFileDate, CollateralId, SurveyNumber).
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

        // Reset to Pending so staff can act on the refreshed row.
        if (Status == ReappraisalCandidateStatus.Deleted)
            Status = ReappraisalCandidateStatus.Pending;
    }
}
