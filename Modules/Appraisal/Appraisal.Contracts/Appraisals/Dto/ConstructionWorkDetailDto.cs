namespace Appraisal.Contracts.Appraisals.Dto;

public record ConstructionWorkDetailDto(
    Guid Id,
    Guid ConstructionWorkGroupId,
    Guid? ConstructionWorkItemId,
    string WorkItemName,
    int DisplayOrder,

    // User-entered
    decimal ConstructionValue,
    decimal PreviousProgressPct,
    decimal CurrentProgressPct,

    // Server-computed
    decimal ProportionPct,
    decimal CurrentProportionPct,
    decimal PreviousPropertyValue,
    decimal CurrentPropertyValue
);
