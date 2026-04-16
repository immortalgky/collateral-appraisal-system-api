namespace Common.Application.Features.Dashboard.Notes;

/// <summary>
/// Shared projection used in responses for all Notes endpoints.
/// </summary>
public sealed record NoteDto(
    Guid Id,
    string Content,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
