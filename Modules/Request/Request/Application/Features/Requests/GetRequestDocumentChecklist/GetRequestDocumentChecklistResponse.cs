namespace Request.Application.Features.Requests.GetRequestDocumentChecklist;

public record GetRequestDocumentChecklistResponse(
    IReadOnlyList<ApplicationDocumentChecklistItem> ApplicationDocuments,
    IReadOnlyList<TitleDocumentChecklistGroup> TitleDocuments,
    bool IsComplete,
    int MissingRequiredCount);
