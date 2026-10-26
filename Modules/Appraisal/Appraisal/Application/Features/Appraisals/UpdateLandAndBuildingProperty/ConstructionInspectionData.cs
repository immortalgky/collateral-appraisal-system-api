namespace Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;

/// <summary>
/// Shared data type for construction inspection input (used by both Building and LandAndBuilding endpoints).
/// null = no-op (don't touch construction), provided = create or update.
/// </summary>
public record ConstructionInspectionData(
    bool IsFullDetail,
    decimal TotalValue,
    // Summary mode
    string? SummaryDetail = null,
    decimal? SummaryPreviousProgressPct = null,
    decimal? SummaryPreviousValue = null,
    decimal? SummaryCurrentProgressPct = null,
    decimal? SummaryCurrentValue = null,
    // Free-text note printed on the construction summary report - applies to BOTH modes
    string? Remark = null,
    // Document reference (summary mode)
    Guid? DocumentId = null,
    string? FileName = null,
    string? FilePath = null,
    string? FileExtension = null,
    string? MimeType = null,
    long? FileSizeBytes = null,
    // Full detail work items
    List<ConstructionWorkDetailData>? WorkDetails = null,
    // Inspector's override of the current value (both modes); null = use the computed one.
    // The Summary*Value fields above are never trusted.
    decimal? CurrentValueOverride = null
);

public record ConstructionWorkDetailData(
    Guid? Id,
    Guid ConstructionWorkGroupId,
    Guid? ConstructionWorkItemId,
    string WorkItemName,
    int DisplayOrder,
    decimal ProportionPct,
    decimal PreviousProgressPct,
    decimal CurrentProgressPct
);
