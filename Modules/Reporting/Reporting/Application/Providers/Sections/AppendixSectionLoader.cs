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
///   appraisal.AppendixDocuments     (AppraisalAppendixId, GalleryPhotoId, DocumentId, DisplaySequence)
///                                    — exactly one of GalleryPhotoId/DocumentId is set per row;
///                                    PDFs link straight to DocumentId and never enter the gallery
///   appraisal.AppraisalGallery      (Id, DocumentId, FilePath, MimeType, Caption) — LEFT JOINed,
///                                    absent for PDF rows
///   document.Documents              (Id, StoragePath, MimeType) — authoritative file path/mime,
///                                    coalesced over the often-null denormalized gallery columns
///
/// Columns confirmed against AppendixConfiguration.cs and AppraisalGalleryConfiguration.cs.
///
/// Rules:
///   - Rows with MimeType LIKE 'image/%' → AppendixImage in the matching AppendixGroup.
///   - Rows with MimeType = 'application/pdf' → DocumentId collected under that group's
///     SLOT name, so the PDF merges directly under its own appendix section (e.g. a PDF
///     uploaded under "Land Map" appears under the Land Map heading) rather than at the
///     end of the document.
///   - Groups with neither images nor PDFs are omitted from the returned section.
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
    /// and a map of per-group SLOT name → DocumentIds of that group's PDF entries, ready
    /// to assign to the model's AttachmentsBySlot so each group's PDFs merge under it.
    /// </returns>
    public static async Task<(AppendixSection? Section, IReadOnlyDictionary<string, IReadOnlyList<Guid>> PdfSlots)> LoadAsync(
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
        //   AppendixDocuments.DisplaySequence / GalleryPhotoId / DocumentId — AppendixDocumentConfiguration
        //   AppraisalGallery.DocumentId / FilePath / MimeType / Caption — AppraisalGalleryConfiguration
        // The denormalized AppraisalGallery.FilePath / MimeType are client-supplied on upload
        // and are frequently NULL (the upload form doesn't always forward them). The authoritative
        // on-disk path + mime live in document.Documents (same source the PDF merge resolves via
        // DapperAttachmentSource), so COALESCE onto the document row and use it as the primary value.
        // AppraisalGallery is LEFT JOINed (not JOINed) because PDF rows have GalleryPhotoId = NULL
        // and link straight to DocumentId instead — an inner join would silently drop them.
        const string sql = """
            SELECT
                at.Code            AS AppendixTypeCode,
                at.Name            AS AppendixTypeName,
                aa.SortOrder,
                aa.LayoutColumns,
                ad.DisplaySequence,
                COALESCE(ad.DocumentId, g.DocumentId)                 AS DocumentId,
                COALESCE(dd.StoragePath, dg.StoragePath, g.FilePath)  AS FilePath,
                -- document.Documents FIRST (matching FilePath above and GetAppraisalAppendices).
                -- AppraisalGallery.MimeType is browser-supplied on upload (`file.type || null`) and
                -- can be '' or application/octet-stream. Preferring it meant an image with a junk
                -- gallery mime matched neither the image/ nor the application/pdf branch below and
                -- was SILENTLY dropped from the book while still rendering fine in the UI.
                COALESCE(dd.MimeType, dg.MimeType, g.MimeType)        AS MimeType,
                g.Caption
            FROM appraisal.AppraisalAppendices aa
            JOIN appraisal.AppendixTypes at         ON at.Id = aa.AppendixTypeId
            JOIN appraisal.AppendixDocuments ad      ON ad.AppraisalAppendixId = aa.Id
            LEFT JOIN appraisal.AppraisalGallery g   ON g.Id = ad.GalleryPhotoId
            -- Two separate document joins rather than one on COALESCE(ad.DocumentId, g.DocumentId):
            -- wrapping the outer side in COALESCE is non-sargable and costs the clustered-index
            -- seek on document.Documents for EVERY book render. Exactly one of GalleryPhotoId /
            -- DocumentId is set per row (domain XOR invariant), so at most one of dd/dg ever
            -- matches and the COALESCE in the SELECT is equivalent to the old predicate.
            LEFT JOIN document.Documents dd          ON dd.Id = ad.DocumentId          -- PDF path
                                                    AND dd.IsActive = 1
                                                    AND dd.IsDeleted = 0
            LEFT JOIN document.Documents dg          ON dg.Id = g.DocumentId           -- image path
                                                    AND dg.IsActive = 1
                                                    AND dg.IsDeleted = 0
            WHERE aa.AppraisalId = @AppraisalId
            ORDER BY aa.SortOrder, ad.DisplaySequence
            """;

        var rows = (await connection.QueryAsync<AppendixRow>(sql, p)).ToList();

        var empty = (IReadOnlyDictionary<string, IReadOnlyList<Guid>>)
            new Dictionary<string, IReadOnlyList<Guid>>();

        // NOTE: do not early-return on empty appendix rows — Photos-tab topics (loaded below)
        // are appended even when the appraisal has no formal appendix documents.

        // Group by appendix type (preserving SortOrder from the query result).
        // Use a list of keys to maintain SortOrder order without re-sorting.
        var groupOrder = new List<string>();           // ordered type codes
        var groupMeta  = new Dictionary<string, (string TypeNameThai, int LayoutColumns)>(StringComparer.OrdinalIgnoreCase);
        var groupImages = new Dictionary<string, List<AppendixImage>>(StringComparer.OrdinalIgnoreCase);
        var groupPdfs   = new Dictionary<string, List<Guid>>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var key = row.AppendixTypeCode ?? row.AppendixTypeName ?? string.Empty;

            // Resolve Thai name
            if (!groupMeta.ContainsKey(key))
            {
                var thaiName = TypeNameMap.TryGetValue(row.AppendixTypeCode ?? string.Empty, out var mapped)
                    ? mapped
                    : row.AppendixTypeName;

                // UI offers 1/2/3 columns; clamp to that range (default 1 when unset) so the
                // template's .cols-N style always matches — same rule as the photo-topic path.
                var cols = row.LayoutColumns >= 1 ? Math.Min(row.LayoutColumns, 3) : 1;

                groupOrder.Add(key);
                groupMeta[key] = (thaiName ?? key, cols);
                groupImages[key] = [];
                groupPdfs[key] = [];
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
            else if (string.Equals(mime, "application/pdf", StringComparison.OrdinalIgnoreCase)
                     && row.DocumentId is not null)
            {
                groupPdfs[key].Add(row.DocumentId.Value);
            }
            // Other mime types — and orphan rows with no resolvable document — are silently skipped
        }

        // Build groups that have at least one image OR one PDF. Each surviving group gets a
        // stable, slot-safe SLOT name (appendix-{index}) so its PDFs merge under its heading.
        var groups = new List<AppendixGroup>();
        var pdfSlots = new Dictionary<string, IReadOnlyList<Guid>>();

        foreach (var key in groupOrder)
        {
            var images = groupImages[key];
            var pdfs   = groupPdfs[key];
            if (images.Count == 0 && pdfs.Count == 0)
                continue;

            var slotName = $"appendix-{groups.Count}";
            var (typeName, cols) = groupMeta[key];

            groups.Add(new AppendixGroup
            {
                TypeNameThai  = typeName,
                LayoutColumns = cols,
                Images        = images.AsReadOnly(),
                SlotName      = slotName,
            });

            if (pdfs.Count > 0)
                pdfSlots[slotName] = pdfs.AsReadOnly();
        }

        // Append the property "Photos" tab — one group per PhotoTopic, honouring its
        // configured 1/2/3-column alignment (PhotoTopics.DisplayColumns) — AFTER the
        // formal appendix groups, so photos always sit last in the appendix.
        await AppendPhotoTopicGroupsAsync(connection, appraisalId, groups, pdfSlots);

        if (groups.Count == 0)
            return (null, empty);

        return (new AppendixSection { Groups = groups }, pdfSlots);
    }

    /// <summary>
    /// Loads the property "Photos" tab — <c>appraisal.PhotoTopics</c> joined to its photos via
    /// <c>appraisal.GalleryPhotoTopicMappings</c> → <c>appraisal.AppraisalGallery</c> — and appends
    /// one <see cref="AppendixGroup"/> per topic to <paramref name="groups"/>. The grid column
    /// count comes from <c>PhotoTopics.DisplayColumns</c> (clamped 1–3). Photo paths/mimes resolve
    /// from <c>document.Documents</c> (coalesced over the often-null denormalized gallery columns),
    /// identical to the appendix path. Topics with no usable photo are skipped.
    /// </summary>
    private static async Task AppendPhotoTopicGroupsAsync(
        IDbConnection connection,
        Guid appraisalId,
        List<AppendixGroup> groups,
        Dictionary<string, IReadOnlyList<Guid>> pdfSlots)
    {
        const string sql = """
            SELECT
                t.Id              AS TopicId,
                t.TopicName,
                t.SortOrder,
                t.DisplayColumns,
                g.DocumentId,
                g.PhotoNumber,
                g.Caption,
                COALESCE(d.StoragePath, g.FilePath) AS FilePath,
                COALESCE(g.MimeType, d.MimeType)    AS MimeType
            FROM appraisal.PhotoTopics t
            LEFT JOIN appraisal.GalleryPhotoTopicMappings m ON m.PhotoTopicId = t.Id
            LEFT JOIN appraisal.AppraisalGallery g          ON g.Id = m.GalleryPhotoId
            LEFT JOIN document.Documents d                  ON d.Id = g.DocumentId
                                                           AND d.IsActive = 1
                                                           AND d.IsDeleted = 0
            WHERE t.AppraisalId = @AppraisalId
            ORDER BY t.SortOrder, g.PhotoNumber
            """;

        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        var rows = (await connection.QueryAsync<PhotoTopicRow>(sql, p)).ToList();
        if (rows.Count == 0)
            return;

        var topicOrder  = new List<Guid>();
        var topicMeta   = new Dictionary<Guid, (string Name, int Columns)>();
        var topicImages = new Dictionary<Guid, List<AppendixImage>>();

        foreach (var row in rows)
        {
            if (!topicMeta.ContainsKey(row.TopicId))
            {
                // UI offers 1/2/3 columns; clamp to that range (default 1 when unset).
                var cols = row.DisplayColumns >= 1 ? Math.Min(row.DisplayColumns, 3) : 1;
                topicOrder.Add(row.TopicId);
                topicMeta[row.TopicId] = (row.TopicName ?? string.Empty, cols);
                topicImages[row.TopicId] = [];
            }

            // LEFT JOINs yield a placeholder row for topics with no photos.
            if (row.DocumentId is null)
                continue;

            var mime = row.MimeType ?? string.Empty;
            if (!mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                continue; // Photos tab is image-only; ignore anything else defensively.

            topicImages[row.TopicId].Add(new AppendixImage
            {
                ImgSrc  = ToFileUri(row.FilePath),
                Caption = string.IsNullOrWhiteSpace(row.Caption) ? null : row.Caption,
            });
        }

        foreach (var topicId in topicOrder)
        {
            var images = topicImages[topicId];
            if (images.Count == 0)
                continue;

            var (name, cols) = topicMeta[topicId];
            groups.Add(new AppendixGroup
            {
                TypeNameThai  = name,
                LayoutColumns = cols,
                Images        = images.AsReadOnly(),
                SlotName      = $"appendix-{groups.Count}",
            });
        }
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
        // Nullable: AppraisalGallery is LEFT JOINed, so an AppendixDocument row whose
        // GalleryPhotoId points at a deleted gallery row yields COALESCE(NULL, NULL) = NULL.
        // Under the previous INNER JOIN such orphans were excluded from the result set; with
        // the LEFT JOIN they come back, and a non-nullable Guid here would make Dapper throw
        // and fail the WHOLE book render rather than skipping the one bad row.
        public Guid?   DocumentId       { get; init; }
        public string? FilePath         { get; init; }
        public string? MimeType         { get; init; }
        public string? Caption          { get; init; }
    }

    private sealed class PhotoTopicRow
    {
        public Guid    TopicId        { get; init; }
        public string? TopicName      { get; init; }
        public int     SortOrder      { get; init; }
        public int     DisplayColumns { get; init; }
        public Guid?   DocumentId     { get; init; }  // nullable: LEFT JOIN placeholder for empty topics
        public int     PhotoNumber    { get; init; }
        public string? Caption        { get; init; }
        public string? FilePath       { get; init; }
        public string? MimeType       { get; init; }
    }
}
