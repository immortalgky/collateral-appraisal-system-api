using Reporting.Application.Services;
using Reporting.Infrastructure.Templates;
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
        var renderedHtml = await RenderRawAsync(templateHtml, model, cancellationToken);

        // ── Extract attachment slot map from model ────────────────────────────
        var attachmentsBySlot = ExtractAttachmentsBySlot(model);

        // ── Split on <!-- SLOT: name --> markers ──────────────────────────────
        return SplitIntoSegments(renderedHtml, attachmentsBySlot);
    }

    public async Task<string> RenderRawAsync(
        string templateHtml,
        object model,
        CancellationToken cancellationToken)
    {
        // ── Build Scriban script context ─────────────────────────────────────
        var template = Template.Parse(templateHtml);

        if (template.HasErrors)
        {
            var errors = string.Join("; ", template.Messages.Select(m => m.ToString()));
            logger.LogError("Scriban template parse errors: {Errors}", errors);
            throw new InvalidOperationException($"Template parse failed: {errors}");
        }

        // Materialise the model into a ScriptObject graph with ALL string values
        // HTML-encoded. Scriban does NOT auto-escape, and the rendered HTML is loaded
        // by real headless Chromium — so DB-sourced free text (customer/owner names,
        // committee comments) must be encoded here, at the single chokepoint, to prevent
        // stored-XSS / layout corruption. Numbers/dates/bools pass through unchanged so
        // math.format / thai.date keep working.
        var scriptObject = new ScriptObject();
        scriptObject.Add("model", ToScriptValue(model));
        // `thai` helper object: thai.baht_text / thai.date / thai.date_short.
        scriptObject.Add("thai", ThaiScribanFunctions.Create());

        var context = new TemplateContext
        {
            MemberRenamer = StandardMemberRenamer.Rename,
            // Enables {{ include 'partials/...' }} — lets composite reports share sections.
            TemplateLoader = new FilePartialTemplateLoader()
        };
        // Pin formatting culture so math.format separators are deterministic across
        // servers regardless of the host/request locale (invariant = "1,234,567.89").
        context.PushCulture(System.Globalization.CultureInfo.InvariantCulture);
        context.PushGlobal(scriptObject);

        return await template.RenderAsync(context);
    }

    /// <summary>
    /// Recursively converts a model object graph into Scriban values, HTML-encoding every
    /// string. POCO properties are renamed to snake_case; numbers/dates/bools/Guids pass
    /// through so template formatting (math.format, thai.date) still works. Lists become
    /// ScriptArray (so <c>.size</c> and for-loops work); dictionaries become ScriptObject.
    /// </summary>
    private static object? ToScriptValue(object? value)
    {
        if (value is null)
            return null;
        if (value is string s)
            return System.Net.WebUtility.HtmlEncode(s);
        if (value is bool or char
            or sbyte or byte or short or ushort or int or uint or long or ulong
            or float or double or decimal
            or DateTime or DateTimeOffset or TimeSpan or Guid or Enum)
            return value;

        if (value is System.Collections.IDictionary dict)
        {
            var obj = new ScriptObject();
            foreach (System.Collections.DictionaryEntry e in dict)
                obj[Convert.ToString(e.Key) ?? string.Empty] = ToScriptValue(e.Value);
            return obj;
        }

        if (value is System.Collections.IEnumerable seq)
        {
            var arr = new ScriptArray();
            foreach (var item in seq)
                arr.Add(ToScriptValue(item));
            return arr;
        }

        // Plain POCO — expose each readable property under its snake_case name.
        var script = new ScriptObject();
        foreach (var prop in value.GetType().GetProperties(
                     System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
                continue;
            script[StandardMemberRenamer.Rename(prop)] = ToScriptValue(prop.GetValue(value));
        }
        return script;
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

        // The assembler renders EVERY HtmlSegment as its own standalone PDF, so each one must be
        // a complete document. Splitting on SLOT markers leaves only the first chunk carrying the
        // original <head> — every later fragment would render with NO stylesheet, so an appendix
        // image would fall back to its intrinsic pixel size and overflow (and be clipped by) the
        // page. Re-attach the document shell to those fragments.
        var (prelude, epilogue) = ExtractDocumentShell(renderedHtml);
        var isFirstHtmlChunk = true;

        void AddHtml(string html)
        {
            if (!HasRenderableContent(html))
                return;

            // The first chunk already opens the document; later ones need the shell re-applied.
            segments.Add(new HtmlSegment(
                isFirstHtmlChunk ? html + epilogue : prelude + html + epilogue));
            isFirstHtmlChunk = false;
        }

        while (true)
        {
            var slotStart = remaining.IndexOf(SlotPrefix, StringComparison.Ordinal);
            if (slotStart < 0)
            {
                // No more slot markers — everything remaining is an HTML fragment
                AddHtml(remaining.ToString());
                break;
            }

            // Emit the HTML up to the marker
            AddHtml(remaining[..slotStart].ToString());

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
    /// Splits the rendered document into the shell that must wrap every standalone fragment:
    /// everything up to and including the opening <c>&lt;body&gt;</c> tag (doctype, &lt;html&gt;,
    /// and critically the &lt;head&gt; with the stylesheet), plus the matching closing tags.
    /// Returns empty strings when the template has no &lt;body&gt; (a bare fragment template),
    /// in which case fragments are emitted unchanged.
    /// </summary>
    private static (string Prelude, string Epilogue) ExtractDocumentShell(string renderedHtml)
    {
        var bodyStart = renderedHtml.IndexOf("<body", StringComparison.OrdinalIgnoreCase);
        if (bodyStart < 0)
            return (string.Empty, string.Empty);

        var bodyTagEnd = renderedHtml.IndexOf('>', bodyStart);
        if (bodyTagEnd < 0)
            return (string.Empty, string.Empty);

        return (renderedHtml[..(bodyTagEnd + 1)], "</body></html>");
    }

    /// <summary>
    /// True if the HTML fragment has visible content worth rendering to a PDF page.
    /// Fragments that are only whitespace or bare closing/structural tags (e.g. the
    /// trailing <c>&lt;/body&gt;&lt;/html&gt;</c> after the last SLOT marker) are skipped
    /// so they don't produce an empty trailing page.
    /// </summary>
    // Static compiled Regex instances with a 1-second match timeout (ReDoS hardening — S6444).
    private static readonly System.Text.RegularExpressions.Regex _mediaTagRegex =
        new(@"<(img|svg|canvas)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled,
            TimeSpan.FromSeconds(1));

    private static readonly System.Text.RegularExpressions.Regex _htmlTagRegex =
        new(@"<[^>]*>",
            System.Text.RegularExpressions.RegexOptions.Compiled,
            TimeSpan.FromSeconds(1));

    private static bool HasRenderableContent(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return false;

        // Media tags carry visible content with no text (e.g. an image-only appendix page).
        if (_mediaTagRegex.IsMatch(html))
            return true;

        // Strip tags; if nothing but whitespace remains, there's no visible text.
        var withoutTags = _htmlTagRegex.Replace(html, string.Empty);
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
