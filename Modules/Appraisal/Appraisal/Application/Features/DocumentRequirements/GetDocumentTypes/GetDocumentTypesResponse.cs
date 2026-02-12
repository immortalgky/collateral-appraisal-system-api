namespace Appraisal.Application.Features.DocumentRequirements.GetDocumentTypes;

public record GetDocumentTypesResponse(IReadOnlyList<DocumentTypeDto> DocumentTypes);
