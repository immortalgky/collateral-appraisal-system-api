namespace Appraisal.Application.Features.DocumentRequirements.GetDocumentChecklist;

public record GetDocumentChecklistResponse(
    IReadOnlyList<DocumentChecklistItemDto> ApplicationDocuments,
    IReadOnlyList<CollateralDocumentGroupDto> CollateralGroups);
