namespace Parameter.DocumentRequirements.Features.ReorderDocumentTypes;

public record DocumentTypeOrderItem(Guid Id, int SortOrder);

public record ReorderDocumentTypesCommand(IReadOnlyList<DocumentTypeOrderItem> Items)
    : ICommand<ReorderDocumentTypesResult>;

public record ReorderDocumentTypesResult(bool Success);
