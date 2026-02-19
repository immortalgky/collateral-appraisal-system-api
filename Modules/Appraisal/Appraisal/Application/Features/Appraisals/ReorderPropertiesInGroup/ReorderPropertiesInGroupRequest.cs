namespace Appraisal.Application.Features.Appraisals.ReorderPropertiesInGroup;

public record ReorderPropertiesInGroupRequest(
    List<Guid> OrderedPropertyIds
);
