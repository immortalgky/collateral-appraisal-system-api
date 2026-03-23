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
    string? Remark = null,
    // Document reference (summary mode)
    Guid? DocumentId = null,
    string? FileName = null,
    string? FilePath = null,
    string? FileExtension = null,
    string? MimeType = null,
    long? FileSizeBytes = null,
    // Full detail work items
    List<ConstructionWorkDetailData>? WorkDetails = null
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
