namespace Appraisal.Contracts.Appraisals.Dto;

public record ConstructionInspectionDto(
    Guid Id,
    Guid AppraisalPropertyId,
    bool IsFullDetail,
    decimal TotalValue,

    // Summary Mode
    string? SummaryDetail,
    decimal? SummaryPreviousProgressPct,
    decimal? SummaryPreviousValue,
    decimal? SummaryCurrentProgressPct,
    decimal? SummaryCurrentValue,
    string? Remark,

    // Document reference
    Guid? DocumentId,
    string? FileName,
    string? FilePath,
    string? FileExtension,
    string? MimeType,
    long? FileSizeBytes,

    // Full Detail Mode
    IReadOnlyList<ConstructionWorkDetailDto>? WorkDetails
);
