namespace Parameter.DocumentRequirements.Features.GetDocumentChecklist;

public record GetDocumentChecklistResponse(
    IReadOnlyList<DocumentChecklistItemDto> ApplicationDocuments,
    IReadOnlyList<PropertyTypeDocumentGroupDto> PropertyTypeGroups);
