using System.Text.RegularExpressions;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Reporting.Application.Services;

namespace Reporting.Infrastructure.PdfAssembly;

/// <summary>
/// Book assembly: the template cuts the book into parts (<see cref="BookPart"/>); each part is rendered to its own PDF,
/// so the number of pages of every part is known exactly, and from that:
///   • page labels — front matter I, II …; body 1, 2 … from the first body part on, attachments counted;
///   • the contents list — a <c>[[PAGE:id]]</c> token in a part is replaced by the label of the page where the part
///     with that id starts (the contents part is rendered twice: once to learn its own length, once with the numbers);
///   • the running header/footer — drawn on an overlay (<see cref="BookChrome"/>) and stamped onto every numbered page.
/// Raw parts (the cover, the bank's summary form) carry no chrome and no number. Attached PDFs are merged untouched and
/// counted in the numbering, so pages after them stay continuous; they are not stamped (their own layout is not ours).
/// </summary>
internal sealed partial class PdfSharpAssembler
{
    private const string TokenPlaceholder = "00";
    private const int MaxLayoutPasses = 3;

    private static readonly Regex PageToken = new(
        @"\[\[PAGE:(?<id>[A-Za-z0-9_-]+)\]\]",
        RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private sealed class BookItem
    {
        public BookPart? Part;               // html item; null part = raw
        public string? Html;                 // html item, tokens unresolved
        public bool HasTokens;
        public byte[]? Pdf;                  // html item, rendered
        public List<byte[]>? Attachments;    // slot item
        public string? Caption;              // slot item: the title block (caption + report number) stamped on every page of the attached file
        public int Pages;
        public string?[] Labels = [];        // one per page; null = unnumbered
        public bool Stamped;                 // header/footer drawn on its pages
    }

    private async Task<byte[]> AssembleBookAsync(
        IReadOnlyList<RenderSegment> segments,
        CancellationToken cancellationToken)
    {
        var budget = new MergeBudget();
        var items = new List<BookItem>();
        foreach (var segment in segments)
        {
            switch (segment)
            {
                case HtmlSegment html:
                    items.Add(new BookItem { Part = html.Part, Html = html.Html, HasTokens = html.Html.Contains("[[PAGE:", StringComparison.Ordinal) });
                    break;
                case AttachmentSlotSegment slot:
                    // LoadSlotPdfsAsync already dropped the attachments whose pages cannot be imported (before charging the budget),
                    // so the page numbering is laid out from what will really be imported.
                    var accepted = await LoadSlotPdfsAsync(slot, budget, cancellationToken);
                    if (accepted.Count > 0)
                        items.Add(new BookItem { Attachments = accepted.Select(a => a.Bytes).ToList(), Pages = accepted.Sum(a => a.Pages), Caption = slot.Caption });
                    break;
            }
        }

        // Every html part to its own PDF. The browser pool bounds how many run at once.
        await Task.WhenAll(items.Where(i => i.Html is not null).Select(async i =>
        {
            i.Pdf = await pdfRenderer.RenderAsync(ResolveTokens(i.Html!, null), cancellationToken);
            i.Pages = CountPages(i.Pdf);
        }));

        // Page labels, then the parts that print them (the contents) with real numbers. The contents length does not
        // depend on the numbers, so this settles on the second pass; the cap only guards a part that does.
        var ids = Layout(items);
        for (var pass = 0; pass < MaxLayoutPasses && items.Any(i => i.HasTokens); pass++)
        {
            var changed = false;
            foreach (var item in items.Where(i => i.HasTokens))
            {
                item.Pdf = await pdfRenderer.RenderAsync(ResolveTokens(item.Html!, ids), cancellationToken);
                var pages = CountPages(item.Pdf);
                changed |= pages != item.Pages;
                item.Pages = pages;
            }
            if (!changed)
                break;
            ids = Layout(items);
        }

        using var output = new PdfDocument();
        var stamps = new List<(int PageIndex, string Label, string? Caption, bool NoHeader)>();
        var keepOpen = new List<IDisposable>(); // the captioned first pages are drawn from these until Save
        foreach (var item in items)
        {
            var first = output.PageCount;
            foreach (var pdf in item.Attachments ?? [item.Pdf!])
            {
                if (item.Caption is not null)
                    ImportPagesAboveTitleBlock(output, pdf, keepOpen);
                else if (item.Attachments is not null)
                    ImportAttachmentPages(output, pdf); // a bad attachment is skipped with a warning, as in the plain path
                else
                    ImportPdfPages(output, pdf);
            }

            if (item.Caption is not null)
                for (var k = first; k < output.PageCount; k++)
                    stamps.Add((k, string.Empty, item.Caption, false)); // title block only: attached pages keep their own layout
            else if (item.Stamped)
                for (var k = 0; k < item.Pages && first + k < output.PageCount; k++)
                    if (item.Labels[k] is { } label)
                        stamps.Add((first + k, label, null, item.Part?.Id == BookChrome.OwnHeaderPartId));
        }

        if (output.PageCount == 0)
            output.AddPage();

        // Kept open until Save: the overlay's pages are copied into the output as they are drawn.
        MemoryStream? overlayStream = null;
        try
        {
            if (stamps.Count > 0)
            {
                var reportNo = BookChrome.ReadReportNumber(items.Where(i => i.Html is not null).Select(i => i.Html!));
                var overlayHtml = BookChrome.OverlayHtml(
                    stamps.Select(s => new BookChrome.OverlayPage(s.Label, IsLandscape(output.Pages[s.PageIndex]), s.Caption, s.NoHeader)).ToList(),
                    reportNo);
                overlayStream = new MemoryStream(await pdfRenderer.RenderAsync(overlayHtml, cancellationToken));
                using var form = XPdfForm.FromStream(overlayStream);
                for (var i = 0; i < stamps.Count && i < form.PageCount; i++)
                {
                    var page = output.Pages[stamps[i].PageIndex];
                    using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);
                    form.PageIndex = i;
                    // The overlay page is rendered at its own size (A4, portrait or landscape). Draw it at that size, never
                    // stretched, anchored at the bottom centre: the title block sits in the band reserved above the bottom
                    // edge. A page smaller than the overlay shrinks it uniformly instead of clipping it.
                    var scale = Math.Min(1, Math.Min(page.Width.Point / form.PointWidth, page.Height.Point / form.PointHeight));
                    var ow = form.PointWidth * scale;
                    var oh = form.PointHeight * scale;
                    gfx.DrawImage(form, (page.Width.Point - ow) / 2, page.Height.Point - oh, ow, oh);
                }
                if (form.PageCount < stamps.Count)
                    logger.LogWarning("Book chrome overlay has {Overlay} pages for {Stamps} numbered pages", form.PageCount, stamps.Count);
            }

            using var ms = new MemoryStream();
            output.Save(ms);
            return ms.ToArray();
        }
        finally
        {
            overlayStream?.Dispose();
            foreach (var d in keepOpen)
                d.Dispose();
        }
    }

    private const double PointsPerMm = 72 / 25.4;

    /// <summary>
    /// Every page of a captioned attachment is redrawn in the area above the title block that <see cref="BookChrome"/>
    /// stamps near the bottom edge (attached pages carry no running header/footer): side margins
    /// <see cref="BookChrome.SideMarginMm"/>, 5 mm at the top, the block plus a 2 mm gap at the bottom. Scaled to the
    /// largest size that fits (aspect ratio kept, never enlarged), centred across, aligned to the top. A file the form
    /// importer cannot read falls back to the plain merge (the block then overprints its bottom edge). A page with a /Rotate is merged as it is; a /CropBox is applied by clipping.
    /// </summary>
    private void ImportPagesAboveTitleBlock(PdfDocument output, byte[] pdfBytes, List<IDisposable> keepOpen)
    {
        var before = output.PageCount;
        try
        {
            var stream = new MemoryStream(pdfBytes);
            keepOpen.Add(stream);
            var form = XPdfForm.FromStream(stream);
            keepOpen.Add(form);

            // The source page's /Rotate and /CropBox. The plain import keeps both; the form importer applies neither correctly (it ignores the crop box and draws a
            // 90 / 270 degree page distorted), so a rotated page is merged as it is (a viewer turns it; the title block then overprints its bottom edge, as in the
            // fallback below) and a crop box is applied here by clipping.
            using var meta = PdfReader.Open(new MemoryStream(pdfBytes), PdfDocumentOpenMode.Import);

            var bottom = (BookChrome.TitleBlockBottomMm + BookChrome.TitleBlockHeightMm + 2) * PointsPerMm;
            var top = 5 * PointsPerMm;
            var side = BookChrome.SideMarginMm * PointsPerMm;
            for (var i = 0; i < form.PageCount; i++)
            {
                form.PageIndex = i;
                var sourcePage = i < meta.PageCount ? meta.Pages[i] : null;
                if (sourcePage is { Rotate: not 0 })
                {
                    output.AddPage(sourcePage);
                    continue;
                }

                var mediaW = form.PointWidth;
                var mediaH = form.PointHeight;
                // what a viewer shows: the crop box clipped to the media box (PDF spec), else the whole media box
                var visible = sourcePage is null ? null : VisibleBox(sourcePage.CropBox, sourcePage.MediaBox);
                var hasCrop = visible is not null;
                var shownW = visible?.W ?? mediaW;
                var shownH = visible?.H ?? mediaH;
                var (offsetX, offsetY) = visible is { } v ? (v.X, v.Y) : (0, 0);

                var page = output.AddPage();
                page.Width = XUnit.FromPoint(shownW);
                page.Height = XUnit.FromPoint(shownH);
                var availW = shownW - 2 * side;
                var availH = shownH - top - bottom;
                var scale = Math.Min(1, Math.Min(availW / shownW, availH / shownH));
                var left = (shownW - shownW * scale) / 2;
                using var gfx = XGraphics.FromPdfPage(page);
                if (hasCrop)
                    gfx.IntersectClip(new XRect(left, top, shownW * scale, shownH * scale));
                gfx.DrawImage(form, left - offsetX * scale, top - offsetY * scale, mediaW * scale, mediaH * scale);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not shrink an attachment page above its title block — merged as it is");
            while (output.PageCount > before)
                output.Pages.RemoveAt(output.PageCount - 1);
            ImportAttachmentPages(output, pdfBytes);
        }
    }

    /// <summary>
    /// The part of the page a viewer shows when it has a crop box: the crop box clipped to the media box, as the PDF spec says (a crop box that sticks out of the
    /// media box shows only what is inside it). X/Y are its top-left corner measured from the media box's top-left corner, W/H its size. Both boxes are in PDF space
    /// (origin bottom-left), and the media box's origin need not be (0,0). Null when there is no usable crop box, or it does not overlap the media box.
    /// </summary>
    internal static (double X, double Y, double W, double H)? VisibleBox(PdfRectangle? crop, PdfRectangle? media)
    {
        if (crop is null || media is null || crop.X2 - crop.X1 <= 0 || crop.Y2 - crop.Y1 <= 0)
            return null;
        var x1 = Math.Max(crop.X1, media.X1);
        var x2 = Math.Min(crop.X2, media.X2);
        var y1 = Math.Max(crop.Y1, media.Y1);
        var y2 = Math.Min(crop.Y2, media.Y2);
        return x2 - x1 > 0 && y2 - y1 > 0 ? (x1 - media.X1, media.Y2 - y2, x2 - x1, y2 - y1) : null;
    }

    /// <summary>
    /// Assigns the label of every page and returns, for each part id, the label of the page it starts on (first part
    /// with that id wins; a part with no number — a raw one — leaves its id out, so its token prints "-").
    /// </summary>
    private static Dictionary<string, string> Layout(List<BookItem> items)
    {
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        var front = 0;
        var body = 0;
        foreach (var item in items)
        {
            var kind = item.Attachments is not null ? BookPartKind.Body : item.Part?.Kind ?? BookPartKind.Raw;
            item.Stamped = item.Attachments is null && kind != BookPartKind.Raw;
            item.Labels = new string?[item.Pages];
            for (var k = 0; k < item.Pages; k++)
                item.Labels[k] = kind switch
                {
                    BookPartKind.Front => BookChrome.Roman(++front),
                    BookPartKind.Body => (++body).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    _ => null
                };

            if (item.Part?.Id is { } id && item.Pages > 0 && item.Labels[0] is { } label)
                ids.TryAdd(id, label);
        }
        return ids;
    }

    private static string ResolveTokens(string html, Dictionary<string, string>? ids) =>
        PageToken.Replace(html, m => ids is null
            ? TokenPlaceholder
            : ids.GetValueOrDefault(m.Groups["id"].Value, "-"));

    /// <summary>
    /// The number of pages the attachment adds when imported, or null (with a warning) when its pages cannot be imported.
    /// Tried on a throwaway document, so a bad file never leaves pages behind or shifts the book's numbering.
    /// </summary>
    private int? ImportablePageCount(byte[] pdfBytes)
    {
        try
        {
            using var scratch = new PdfDocument();
            ImportPdfPages(scratch, pdfBytes);
            return scratch.PageCount;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "A PDF attachment's pages cannot be imported — skipped before the book is numbered");
            return null;
        }
    }

    private static int CountPages(byte[] pdf)
    {
        using var ms = new MemoryStream(pdf);
        using var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
        return doc.PageCount;
    }

    private static bool IsLandscape(PdfPage page) => page.Width.Point > page.Height.Point;
}
