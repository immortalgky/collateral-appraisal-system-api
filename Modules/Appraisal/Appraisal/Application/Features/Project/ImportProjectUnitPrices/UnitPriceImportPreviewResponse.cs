namespace Appraisal.Application.Features.Project.ImportProjectUnitPrices;

/// <summary>Stable error codes of the price-list import (blocking unless noted).</summary>
public static class UnitPriceImportErrorCodes
{
    public const string MissingHeader = "MissingHeader";
    public const string InvalidNumber = "InvalidNumber";
    public const string UnitNotFound = "UnitNotFound";
    public const string AmbiguousMatch = "AmbiguousMatch";
    public const string DuplicateUnit = "DuplicateUnit";
    public const string MissingAppraisalValue = "MissingAppraisalValue";
    public const string MissingPrice = "MissingPrice";
}

/// <summary>A blocking problem. RowNumber is the 1-based Excel row (1 = the header row).</summary>
public record UnitPriceImportError(int RowNumber, string? Key, string Code, string Message);

/// <summary>A compare-only column whose file value differs from the unit in the system. Never written.</summary>
public record UnitPriceAttributeDiff(int RowNumber, string Key, string Column, string? SystemValue, string? FileValue);

public record UnitMissingFromFile(Guid ProjectUnitId, string? Key);

public record UnitPriceImportRow(
    int RowNumber, string Key, Guid? ProjectUnitId, decimal? PreviousAppraisalValue, decimal? NewAppraisalValue);

public record UnitPriceImportTotals(decimal AppraisalValue, decimal ForceSellingPrice, decimal CoverageAmount);

public record UnitPriceSheetTotals(decimal? AppraisalValue, decimal? ForceSellingPrice, decimal? CoverageAmount);

/// <summary>Result of parsing + matching a price list. Returned by both preview and import.</summary>
public record UnitPriceImportPreviewResponse(
    string FileName,
    string ProjectType,
    int RowCount,
    int MatchedCount,
    int MatchedByPrimaryKey,
    int MatchedBySecondaryKey,
    int UnitsInProject,
    IReadOnlyList<UnitPriceImportError> Errors,
    IReadOnlyList<UnitPriceAttributeDiff> AttributeDiffs,
    IReadOnlyList<UnitMissingFromFile> UnitsMissingFromFile,
    IReadOnlyList<UnitPriceImportRow> Rows,
    UnitPriceImportTotals Totals,
    UnitPriceSheetTotals? SheetTotals,
    bool? SheetTotalsMatch,
    bool CanImport);
