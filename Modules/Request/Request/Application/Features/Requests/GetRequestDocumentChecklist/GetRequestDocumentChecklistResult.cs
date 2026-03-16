namespace Request.Application.Features.Requests.GetRequestDocumentChecklist;

public record GetRequestDocumentChecklistResult(
    IReadOnlyList<ApplicationDocumentChecklistItem> ApplicationDocuments,
    IReadOnlyList<TitleDocumentChecklistGroup> TitleDocuments,
    bool IsComplete,
    int MissingRequiredCount);

public record ApplicationDocumentChecklistItem(
    string Code,
    string Name,
    string? Category,
    bool IsRequired,
    bool IsUploaded,
    string? Notes);

public record TitleDocumentChecklistGroup(
    Guid TitleId,
    string? CollateralType,
    string? OwnerName,
    IReadOnlyList<TitleDocumentChecklistItem> Documents);

public record TitleDocumentChecklistItem(
    string Code,
    string Name,
    string? Category,
    bool IsRequired,
    bool IsUploaded,
    string? Notes);
