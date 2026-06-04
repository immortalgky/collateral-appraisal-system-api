using Reporting.Application.Services;
using Scriban;
using Scriban.Runtime;

namespace Reporting.Infrastructure.Rendering;

/// <summary>
/// Renders a Scriban HTML template against a model and splits the result at
/// <c>&lt;!-- SLOT: name --&gt;</c> markers into ordered <see cref="RenderSegment"/>s.
///
/// How slot splitting works:
///   1. The template is rendered entirely by Scriban first (substitutions, loops, etc.)
///   2. The rendered HTML string is then split on <c>&lt;!-- SLOT: {name} --&gt;</c>
///      tokens (case-sensitive).
///   3. Each HTML chunk BEFORE a SLOT marker becomes an <see cref="HtmlSegment"/>.
///   4. Each SLOT marker becomes an <see cref="AttachmentSlotSegment"/> whose
///      DocumentIds are looked up from the model's AttachmentsBySlot dictionary.
///   5. If no SLOT markers exist, one <see cref="HtmlSegment"/> is returned.
///
/// CSS page-break: the template itself should inject <c>page-break-before: always</c>
/// on any element that must start a new PDF page (already included in the v1 template).
/// </summary>
internal sealed class ScribanTemplateRenderer(
    ILogger<ScribanTemplateRenderer> logger) : ITemplateRenderer
{
    // Regex-free split: we look for literal <!-- SLOT: xxx --> tokens.
    private const string SlotPrefix = "<!-- SLOT: ";
    private const string SlotSuffix = " -->";

    public async Task<IReadOnlyList<RenderSegment>> RenderAsync(
        string templateHtml,
        object model,
        CancellationToken cancellationToken)
    {
        // ── 1. Build Scriban script context ─────────────────────────────────────
        var template = Template.Parse(templateHtml);

        if (template.HasErrors)
        {
            var errors = string.Join("; ", template.Messages.Select(m => m.ToString()));
            logger.LogError("Scriban template parse errors: {Errors}", errors);
            throw new InvalidOperationException($"Template parse failed: {errors}");
        }

        // Wrap model in a ScriptObject so Scriban can walk its properties.
        // We build a nested ScriptObject for `model` and add it to the global context.
        var modelObject = new ScriptObject();
        modelObject.Import(model, renamer: StandardMemberRenamer.Rename);

        var scriptObject = new ScriptObject();
        scriptObject.Add("model", modelObject);

        var context = new TemplateContext { MemberRenamer = StandardMemberRenamer.Rename };
        context.PushGlobal(scriptObject);

        var renderedHtml = await template.RenderAsync(context);

        // ── 2. Extract attachment slot map from model ────────────────────────────
        var attachmentsBySlot = ExtractAttachmentsBySlot(model);

        // ── 3. Split on <!-- SLOT: name --> markers ──────────────────────────────
        return SplitIntoSegments(renderedHtml, attachmentsBySlot);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<Guid>> ExtractAttachmentsBySlot(object model)
    {
        // Use reflection to find a property named AttachmentsBySlot on the model.
        var prop = model.GetType().GetProperty("AttachmentsBySlot");
        if (prop?.GetValue(model) is IReadOnlyDictionary<string, IReadOnlyList<Guid>> dict)
            return dict;
        return new Dictionary<string, IReadOnlyList<Guid>>();
    }

    private static IReadOnlyList<RenderSegment> SplitIntoSegments(
        string renderedHtml,
        IReadOnlyDictionary<string, IReadOnlyList<Guid>> attachmentsBySlot)
    {
        var segments = new List<RenderSegment>();
        var remaining = renderedHtml.AsSpan();

        while (true)
        {
            var slotStart = remaining.IndexOf(SlotPrefix, StringComparison.Ordinal);
            if (slotStart < 0)
            {
                // No more slot markers — everything remaining is an HTML fragment
                var html = remaining.ToString();
                if (HasRenderableContent(html))
                    segments.Add(new HtmlSegment(html));
                break;
            }

            // Emit the HTML up to the marker
            var htmlBefore = remaining[..slotStart].ToString();
            if (HasRenderableContent(htmlBefore))
                segments.Add(new HtmlSegment(htmlBefore));

            // Parse the slot name
            remaining = remaining[(slotStart + SlotPrefix.Length)..];
            var slotEnd = remaining.IndexOf(SlotSuffix, StringComparison.Ordinal);
            if (slotEnd < 0)
                break; // Malformed marker — stop processing

            var slotName = remaining[..slotEnd].ToString().Trim();
            remaining = remaining[(slotEnd + SlotSuffix.Length)..];

            var documentIds = attachmentsBySlot.TryGetValue(slotName, out var ids)
                ? ids
                : Array.Empty<Guid>();

            segments.Add(new AttachmentSlotSegment(slotName, documentIds));
        }

        // Always return at least one segment so the assembler has something to work with
        if (segments.Count == 0)
            segments.Add(new HtmlSegment("<html><body></body></html>"));

        return segments.AsReadOnly();
    }

    /// <summary>
    /// True if the HTML fragment has visible content worth rendering to a PDF page.
    /// Fragments that are only whitespace or bare closing/structural tags (e.g. the
    /// trailing <c>&lt;/body&gt;&lt;/html&gt;</c> after the last SLOT marker) are skipped
    /// so they don't produce an empty trailing page.
    /// </summary>
    private static bool HasRenderableContent(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return false;

        // Strip tags; if nothing but whitespace remains, there's no visible text.
        var withoutTags = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", string.Empty);
        return !string.IsNullOrWhiteSpace(withoutTags);
    }
}

/// <summary>
/// Renames C# PascalCase members to snake_case for Scriban template access.
/// e.g. CustomerName → customer_name, LoanAmount → loan_amount.
/// </summary>
file static class StandardMemberRenamer
{
    public static string Rename(System.Reflection.MemberInfo member)
    {
        // Convert PascalCase to snake_case
        var name = member.Name;
        var sb = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0)
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
