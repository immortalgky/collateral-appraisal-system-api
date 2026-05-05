namespace Appraisal.Application.Features.ConstructionInspections.GetWorkDetailsForPrefill;

/// <summary>
/// Response for GET /appraisal/construction-inspections/{inspectionId}/work-details.
/// The FE uses this to seed PreviousProgressPct on the new inspection's work details,
/// matched by ConstructionWorkItemId (template FK) then WorkItemName as fallback.
/// </summary>
public record GetWorkDetailsForPrefillResult(
    Guid InspectionId,
    bool IsFullDetail,
    decimal OverallCurrentProgressPercent,
    // Full-detail mode — ordered by DisplayOrder
    IReadOnlyList<WorkDetailPrefillItem>? WorkDetails,
    // Summary mode
    WorkDetailSummaryPrefill? Summary
);

public record WorkDetailPrefillItem(
    Guid WorkDetailId,
    Guid ConstructionWorkGroupId,
    Guid? ConstructionWorkItemId,
    string WorkItemName,
    int DisplayOrder,
    decimal ProportionPct,
    decimal CurrentProgressPct,
    decimal ConstructionValue
);

public record WorkDetailSummaryPrefill(
    decimal? SummaryCurrentProgressPct,
    decimal? SummaryCurrentValue,
    string? SummaryDetail,
    string? Remark
);
