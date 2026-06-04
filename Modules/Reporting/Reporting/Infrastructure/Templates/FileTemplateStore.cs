using Reporting.Application.Services;

namespace Reporting.Infrastructure.Templates;

/// <summary>
/// Loads templates from the <c>Templates/</c> directory next to the executable.
/// Files must be named <c>{templateId}.html</c>.
/// To switch to a DB-backed store: implement <see cref="ITemplateStore"/> and
/// swap the DI registration in <see cref="ReportingModule"/>.
/// </summary>
internal sealed class FileTemplateStore(ILogger<FileTemplateStore> logger) : ITemplateStore
{
    // Lazily resolved; AppContext.BaseDirectory is correct for IIS-hosted .NET 9 apps.
    private static readonly string TemplatesRoot =
        Path.Combine(AppContext.BaseDirectory, "Templates");

    public Task<string> GetTemplateAsync(string templateId, CancellationToken cancellationToken)
    {
        // Sanitise: only allow alphanumeric, dash, underscore to prevent path traversal.
        if (string.IsNullOrWhiteSpace(templateId) || !IsValidId(templateId))
            throw new NotFoundException("ReportTemplate", templateId);

        var path = Path.Combine(TemplatesRoot, $"{templateId}.html");

        if (!File.Exists(path))
        {
            logger.LogWarning("Report template not found: {Path}", path);
            throw new NotFoundException("ReportTemplate", templateId);
        }

        return File.ReadAllTextAsync(path, cancellationToken);
    }

    private static bool IsValidId(string id) =>
        id.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');
}
