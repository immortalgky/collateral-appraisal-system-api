namespace Appraisal.Application.Features.SupportingDataMaintenance.BulkUploadSupportingDetails;

/// <summary>
/// Describes a validation problem found in one row of the bulk-upload Excel file.
/// Returned in the 400 response body when the all-or-nothing validation fails.
/// </summary>
public record RowParseError(
    int RowNumber,       // Excel row number (2-based; row 1 is the header)
    string? Column,      // Header name of the column that failed (null = whole-row problem)
    string Message       // Human-readable reason
);
