namespace Reporting.Application.Services;

/// <summary>
/// Loads raw HTML template content by templateId.
/// v1 = file-backed (<see cref="Reporting.Infrastructure.Templates.FileTemplateStore"/>).
/// A DB-backed implementation can be swapped in without changing the pipeline.
/// </summary>
public interface ITemplateStore
{
    /// <summary>
    /// Returns the raw HTML template string.
    /// Throws <see cref="NotFoundException"/> if not found.
    /// </summary>
    Task<string> GetTemplateAsync(string templateId, CancellationToken cancellationToken);
}
