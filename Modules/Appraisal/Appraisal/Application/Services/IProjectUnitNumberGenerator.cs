namespace Appraisal.Application.Services;

/// <summary>
/// Issues unit numbers for block-project units.
/// </summary>
public interface IProjectUnitNumberGenerator
{
    /// <summary>
    /// Reserves <paramref name="count"/> consecutive unit numbers for the given Buddhist year and
    /// returns them in issue order.
    /// </summary>
    Task<IReadOnlyList<string>> GenerateAsync(
        int thaiYear,
        int count,
        CancellationToken cancellationToken = default);
}
