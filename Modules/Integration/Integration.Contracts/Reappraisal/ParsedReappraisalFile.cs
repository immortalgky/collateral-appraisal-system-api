namespace Integration.Contracts.Reappraisal;

/// <summary>Result of parsing a single COLLATREV file.</summary>
public record ParsedReappraisalFile(
    DateOnly EffectiveDate,
    List<ParsedDetailRecord> Details
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
    DateOnly? EffectiveDateAppraisal
);
