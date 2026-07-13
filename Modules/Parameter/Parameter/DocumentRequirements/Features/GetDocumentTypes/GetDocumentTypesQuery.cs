namespace Parameter.DocumentRequirements.Features.GetDocumentTypes;

public record GetDocumentTypesQuery(bool IncludeInactive = false, string? Category = null) : IQuery<GetDocumentTypesResult>;
