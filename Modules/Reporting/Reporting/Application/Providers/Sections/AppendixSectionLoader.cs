using System.Data;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the appendix section (ภาคผนวก) — FSD §2.1.2.12+.
///
/// Data sources (Dapper read-only, no EF Core tracking):
///
///   appraisal.AppraisalAppendices   (AppraisalId, AppendixTypeId, SortOrder, LayoutColumns)
///   appraisal.AppendixTypes         (Id, Code, Name)
///   appraisal.AppendixDocuments     (AppraisalAppendixId, GalleryPhotoId, DisplaySequence)
///   appraisal.AppraisalGallery      (Id, DocumentId, FilePath, MimeType, Caption)
///
/// Columns confirmed against AppendixConfiguration.cs and AppraisalGalleryConfiguration.cs.
///
/// Rules:
///   - Rows with MimeType LIKE 'image/%' → AppendixImage in the matching AppendixGroup.
///   - Rows with MimeType = 'application/pdf' → DocumentId collected into PdfDocumentIds
///     so the existing <!-- SLOT: appendix --> mechanism merges them.
///   - Groups with no images are omitted from the returned section.
///   - Returns (null, empty) when the appraisal has no appendix rows at all.
/// </summary>
internal static class AppendixSectionLoader
{
    // Code→Thai display name map (source: FSD §2.1.2.12+ spec)
    private static readonly IReadOnlyDictionary<string, string> TypeNameMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BRIEF_MAP"]      = "แผนที่สังเขป",
            ["DETAILED_MAP"]   = "แผนที่ละเอียด",
            ["EARTH_MAP"]      = "แผนที่ Earth",
            ["LAND_MAP"]       = "ระวางแดง",
            ["CITY_PLAN"]      = "ผังเมือง",
            ["STATUTORY_PLAN"] = "ผังตามกฎหมาย",
            ["LAND_PLAN"]      = "ผังที่ดิน",
            ["BUILDING_LAYOUT"]= "ผังสิ่งปลูกสร้าง",
            ["BLUEPRINT"]      = "แบบแปลน",
            ["PHOTO_SPOT"]     = "รูปถ่ายกับจุดถ่ายภาพ",
            ["REG_INDEX"]      = "สารบัญจดทะเบียน",
        };

    /// <summary>
    /// Loads appendix data for the given <paramref name="appraisalId"/>.
    /// </summary>
    /// <param name="connection">An open Dapper <see cref="IDbConnection"/>.</param>
    /// <param name="appraisalId">The appraisal to load the appendix for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A tuple of the <see cref="AppendixSection"/> (null when no appendix rows exist)
    /// and the list of DocumentIds for PDF-type entries to merge via the appendix SLOT.
    /// </returns>
    public static async Task<(AppendixSection? Section, IReadOnlyList<Guid> PdfDocumentIds)> LoadAsync(
        IDbConnection connection,
        Guid appraisalId,
        CancellationToken ct = default)
    {
        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        // Single join query: all appendix documents for the appraisal, ordered for grouping.
        // Columns confirmed:
        //   AppendixTypes.Code / Name — AppendixTypeConfiguration: HasMaxLength, no HasColumnName override
        //   AppraisalAppendices.SortOrder / LayoutColumns — AppendixConfiguration
        //   AppendixDocuments.DisplaySequence / GalleryPhotoId — AppendixDocumentConfiguration
        //   AppraisalGallery.DocumentId / FilePath / MimeType / Caption — AppraisalGalleryConfiguration
        const string sql = """
            SELECT
                at.Code            AS AppendixTypeCode,
                at.Name            AS AppendixTypeName,
                aa.SortOrder,
                aa.LayoutColumns,
                ad.DisplaySequence,
                g.DocumentId,
                g.FilePath,
                g.MimeType,
                g.Caption
            FROM appraisal.AppraisalAppendices aa
            JOIN appraisal.AppendixTypes at         ON at.Id = aa.AppendixTypeId
            JOIN appraisal.AppendixDocuments ad      ON ad.AppraisalAppendixId = aa.Id
            JOIN appraisal.AppraisalGallery g        ON g.Id = ad.GalleryPhotoId
            WHERE aa.AppraisalId = @AppraisalId
            ORDER BY aa.SortOrder, ad.DisplaySequence
            """;

        var rows = (await connection.QueryAsync<AppendixRow>(sql, p)).ToList();

        if (rows.Count == 0)
            return (null, Array.Empty<Guid>());

        // Separate images from PDFs
        var pdfDocumentIds = new List<Guid>();

        // Group by appendix type (preserving SortOrder from the query result)
        // Use a list of (key, items) to maintain SortOrder order without re-sorting.
        var groupOrder = new List<string>();           // ordered type codes
        var groupMeta  = new Dictionary<string, (string TypeNameThai, int LayoutColumns)>(StringComparer.OrdinalIgnoreCase);
        var groupImages = new Dictionary<string, List<AppendixImage>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var key = row.AppendixTypeCode ?? row.AppendixTypeName ?? string.Empty;

            // Resolve Thai name
            if (!groupMeta.ContainsKey(key))
            {
                var thaiName = TypeNameMap.TryGetValue(row.AppendixTypeCode ?? string.Empty, out var mapped)
                    ? mapped
                    : row.AppendixTypeName;

                var cols = row.LayoutColumns > 0 ? row.LayoutColumns : 2;

                groupOrder.Add(key);
                groupMeta[key] = (thaiName ?? key, cols);
                groupImages[key] = [];
            }

            var mime = row.MimeType ?? string.Empty;

            if (mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                // Image entry: build a valid file:// URI only when FilePath is non-empty.
                // Use Uri so spaces/special chars in the path are percent-encoded (a raw
                // "file://" + path with spaces yields a broken URL → missing image).
                var imgSrc = ToFileUri(row.FilePath);

                groupImages[key].Add(new AppendixImage
                {
                    ImgSrc  = imgSrc,
                    Caption = string.IsNullOrWhiteSpace(row.Caption) ? null : row.Caption,
                });
            }
            else if (string.Equals(mime, "application/pdf", StringComparison.OrdinalIgnoreCase))
            {
                pdfDocumentIds.Add(row.DocumentId);
            }
            // Other mime types are silently skipped
        }

        // Build groups that have at least one image
        var groups = groupOrder
            .Where(k => groupImages[k].Count > 0)
            .Select(k =>
            {
                var (typeName, cols) = groupMeta[k];
                return new AppendixGroup
                {
                    TypeNameThai  = typeName,
                    LayoutColumns = cols,
                    Images        = groupImages[k].AsReadOnly(),
                };
            })
            .ToList();

        // If there are no image groups AND no PDFs, treat as empty
        if (groups.Count == 0 && pdfDocumentIds.Count == 0)
            return (null, Array.Empty<Guid>());

        var section = groups.Count > 0
            ? new AppendixSection { Groups = groups }
            : null;

        return (section, pdfDocumentIds);
    }

    /// <summary>
    /// Builds a percent-encoded file:// URI from a stored physical path, or null when the
    /// path is blank. Falls back to a manual "file://" + encoded path if Uri can't parse
    /// (e.g. a relative path), so spaces/Thai chars never produce a broken &lt;img&gt; src.
    /// </summary>
    private static string? ToFileUri(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        try
        {
            return new Uri(filePath).AbsoluteUri;
        }
        catch (UriFormatException)
        {
            return "file://" + Uri.EscapeDataString(filePath).Replace("%2F", "/").Replace("%5C", "/");
        }
    }

    // ── Private flat DTO for Dapper mapping ───────────────────────────────────────

    private sealed class AppendixRow
    {
        public string? AppendixTypeCode { get; init; }
        public string? AppendixTypeName { get; init; }
        public int     SortOrder        { get; init; }
        public int     LayoutColumns    { get; init; }
        public int     DisplaySequence  { get; init; }
        public Guid    DocumentId       { get; init; }
        public string? FilePath         { get; init; }
        public string? MimeType         { get; init; }
        public string? Caption          { get; init; }
    }
}
