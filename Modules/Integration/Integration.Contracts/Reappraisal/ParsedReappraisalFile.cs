namespace Integration.Contracts.Reappraisal;

/// <summary>Result of parsing a single COLLATREV file.</summary>
public record ParsedReappraisalFile(
    DateOnly EffectiveDate,
    List<ParsedDetailRecord> Details,
    // 650-char rows whose position 650 held something other than Y / N / blank (read as no flag).
    int UnrecognisedAutoUpdateRows = 0
);

/// <summary>Parsed values from a single Detail ('D') record line.</summary>
public record ParsedDetailRecord(
    string RowHash,
    string ReviewType,
    DateOnly ReviewDate,
    string CollateralId,
    string SurveyNumber,
    string CollateralCode,
    string CollateralCategory,
    string? CollateralName,
    string? CollateralAddress,
    string CifNumber,
    string? CifName,
    string? AoCode,
    string? AoName,
    string? TitleNumber,
    decimal? CurrentValue,
    DateOnly? ValuationDate,
    string? InternalExternal,
    string? BusinessSize,
    string? BusinessSizeDesc,
    decimal? MortgageAmount,
    int? PastDueDay,
    string? ApplicationNumber,
    string? FacilityCode,
    string? FacilitySequence,
    string? CpNumber,
    string? CarCode,
    decimal? FacilityLimit,
    string? FlagLessAge4Y,
    string? FlagGreaterAge4Y,
    string? CountAgeingDate,
    string? CollateralDescription,
    string? ExternalValuerName,
    string? InternalValuerName,
    string? SllOver100M,
    string? SllDescription,
    string? Stage,
    string? IBGRetail,
    string? Group,
    DateOnly? EffectiveDateAppraisal,
    string? AutoUpdate,
    // True for a 650-char row, which has position 650: a null AutoUpdate then means AS400 sent no usable
    // flag. False for a 649-char row, which has no such position at all.
    bool HasAutoUpdatePosition
);
