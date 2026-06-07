using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;

namespace Reporting.Infrastructure.Templates;

/// <summary>
/// Resolves Scriban <c>{{ include 'name' }}</c> directives to reusable partial
/// templates under <c>Templates/</c> (next to the executable).
///
/// Usage in a template: <c>{{ include 'partials/approver-block' }}</c> loads
/// <c>Templates/partials/approver-block.html</c>. A bare name like
/// <c>{{ include 'approver-block' }}</c> resolves to <c>Templates/approver-block.html</c>.
///
/// This is the mechanism that lets composite reports (External / Internal reports)
/// share the ~15 common sections instead of duplicating HTML. The <c>.html</c>
/// extension is appended automatically when the include name has none.
///
/// Security: names are sanitised to <c>[a-zA-Z0-9-_/]</c> and the resolved path is
/// verified to stay under <c>Templates/</c> (no <c>..</c> traversal) — mirroring the
/// guard in <see cref="FileTemplateStore"/>.
/// </summary>
internal sealed class FilePartialTemplateLoader : ITemplateLoader
{
    private static readonly string TemplatesRoot =
        Path.Combine(AppContext.BaseDirectory, "Templates");

    public string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName) || !IsValidName(templateName))
            throw new InvalidOperationException($"Invalid include template name: '{templateName}'");

        var relative = templateName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? templateName
            : templateName + ".html";

        var fullPath = Path.GetFullPath(Path.Combine(TemplatesRoot, relative));

        // Defence-in-depth: ensure the resolved path is still under Templates/.
        var rootWithSep = TemplatesRoot.EndsWith(Path.DirectorySeparatorChar)
            ? TemplatesRoot
            : TemplatesRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSep, StringComparison.Ordinal))
            throw new InvalidOperationException($"Include path escapes Templates root: '{templateName}'");

        return fullPath;
    }

    public string Load(TemplateContext context, SourceSpan callerSpan, string templatePath)
    {
        if (!File.Exists(templatePath))
            throw new InvalidOperationException($"Include partial not found: '{templatePath}'");
        return File.ReadAllText(templatePath);
    }

    public async ValueTask<string?> LoadAsync(
        TemplateContext context, SourceSpan callerSpan, string templatePath)
    {
        if (!File.Exists(templatePath))
            throw new InvalidOperationException($"Include partial not found: '{templatePath}'");
        return await File.ReadAllTextAsync(templatePath);
    }

    // Allow alphanumerics, dash, underscore, dot and forward-slash (sub-folders).
    // Reject any ".." segment to block traversal regardless of slashes.
    private static bool IsValidName(string name)
    {
        if (name.Contains("..", StringComparison.Ordinal))
            return false;
        return name.All(c =>
            char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '/' || c == '.');
    }
}
