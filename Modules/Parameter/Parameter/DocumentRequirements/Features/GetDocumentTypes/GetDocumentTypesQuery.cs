namespace Parameter.DocumentRequirements.Features.GetDocumentTypes;

public record GetDocumentTypesQuery(bool IncludeInactive = false) : IQuery<GetDocumentTypesResult>;
