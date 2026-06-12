namespace Reporting.Application.Services;

/// <summary>
/// Resolves a caller-supplied report identifier to the entity Guid the data providers expect.
/// Callers pass a human-friendly number — a <c>MeetingNo</c> for Meeting-category reports,
/// otherwise an <c>AppraisalNumber</c>; a value already in Guid form is returned unchanged.
/// </summary>
public interface IReportEntityResolver
{
    /// <summary>
    /// Returns the entity Guid (as a string) for <paramref name="entityId"/>.
    /// Throws <see cref="Shared.Exceptions.NotFoundException"/> when the number resolves to no row.
    /// </summary>
    Task<string> ResolveAsync(string entityId, string category, CancellationToken cancellationToken);
}
