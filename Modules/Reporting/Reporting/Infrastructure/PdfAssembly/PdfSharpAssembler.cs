using Microsoft.Extensions.Options;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Reporting.Application.Services;
using Shared.Configurations;

namespace Reporting.Infrastructure.PdfAssembly;

/// <summary>
/// Assembles the ordered segment list into one merged PDF using PDFsharp 6.x.
///
/// For each segment:
///   - <see cref="HtmlSegment"/>: rendered to PDF bytes via <see cref="IPdfRenderer"/>,
///     then each page is imported with <c>PdfDocument.AddPage</c>.
///   - <see cref="AttachmentSlotSegment"/>: each DocumentId is resolved to a file path
///     via <see cref="IReportAttachmentSource"/>; the PDF is opened and pages imported.
///     If the file is missing or not a valid PDF, the slot is skipped with a warning.
///
/// PdfSharp requires <c>Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)</c>
/// for Windows-1252 encoded PDFs — registered once in <see cref="ReportingModule"/>.
/// </summary>
internal sealed partial class PdfSharpAssembler(
    IPdfRenderer pdfRenderer,
    IReportAttachmentSource attachmentSource,
    IOptions<FileStorageConfiguration> fileStorageOptions,
    ILogger<PdfSharpAssembler> logger) : IPdfAssembler
{
    // Safety caps so a request with many/large uploaded PDFs can't exhaust memory
    // on the shared app server. Beyond these, remaining attachments are skipped with
    // a warning (the generated form itself always renders).
    private const int MaxAttachments = 50;

    // The same number the attach-time check refuses a file against
    // (AddAppendixDocumentCommandHandler), so what a user is told when attaching and what the
    // book can actually carry are one value rather than two that drift apart.
    private readonly long _maxTotalAttachmentBytes = fileStorageOptions.Value.MaxAppendixFileSizeBytes;

    // Overall ceiling for a composite merge. The per-child attachment caps above are enforced
    // per AssembleAsync (i.e. per child form), so an N-form composite could otherwise import
    // up to N×100 MB before this runs. This backstop bounds the merged document so a composite
    // can't exhaust memory on the shared app server (the N=2 IIS boxes). Beyond it, remaining
    // child PDFs are skipped with a warning — the forms already merged still render.
    private const long MaxMergedBytes = 200L * 1024 * 1024; // 200 MB

    public async Task<byte[]> AssembleAsync(
        IReadOnlyList<RenderSegment> segments,
        CancellationToken cancellationToken)
    {
        // A template that marks its regions with PART markers (the appraisal book) gets running headers/footers,
        // roman/arabic page numbers and a contents list with real page numbers; every other report is merged as is.
        if (segments.OfType<HtmlSegment>().Any(h => h.Part is not null))
            return await AssembleBookAsync(segments, cancellationToken);

        using var output = new PdfDocument();
        var budget = new MergeBudget();

        foreach (var segment in segments)
        {
            switch (segment)
            {
                case HtmlSegment html:
                    await AppendHtmlSegmentAsync(output, html, cancellationToken);
                    break;

                case AttachmentSlotSegment slot:
                    await AppendAttachmentSlotAsync(output, slot, budget, cancellationToken);
                    break;
            }
        }

        // If nothing was added (all segments empty), add a blank page so
        // the output is a valid PDF (not empty bytes).
        if (output.PageCount == 0)
            output.AddPage();

        using var ms = new MemoryStream();
        output.Save(ms);
        return ms.ToArray();
    }

    public Task<byte[]> MergeAsync(
        IReadOnlyList<byte[]> pdfs,
        CancellationToken cancellationToken)
    {
        // One input: hand it back as-is (no re-encode). Zero inputs: fall through to the
        // blank-page guard below so the result is always a valid PDF.
        if (pdfs.Count == 1)
            return Task.FromResult(pdfs[0]);

        using var output = new PdfDocument();
        long mergedBytes = 0;
        for (var i = 0; i < pdfs.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pdf = pdfs[i];

            // Keep at least the first child so the output is never empty; cap the rest.
            if (mergedBytes + pdf.LongLength > MaxMergedBytes && output.PageCount > 0)
            {
                logger.LogWarning(
                    "Merged-document cap reached ({Bytes} bytes) — skipping {Remaining} remaining child PDF(s)",
                    mergedBytes, pdfs.Count - i);
                break;
            }

            ImportPdfPages(output, pdf);
            mergedBytes += pdf.LongLength;
        }

        if (output.PageCount == 0)
            output.AddPage();

        using var ms = new MemoryStream();
        output.Save(ms);
        return Task.FromResult(ms.ToArray());
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task AppendHtmlSegmentAsync(
        PdfDocument output,
        HtmlSegment segment,
        CancellationToken cancellationToken)
    {
        var pdfBytes = await pdfRenderer.RenderAsync(segment.Html, cancellationToken);
        ImportPdfPages(output, pdfBytes);
    }

    private async Task AppendAttachmentSlotAsync(
        PdfDocument output,
        AttachmentSlotSegment slot,
        MergeBudget budget,
        CancellationToken cancellationToken)
    {
        foreach (var (bytes, _) in await LoadSlotPdfsAsync(slot, budget, cancellationToken))
            ImportAttachmentPages(output, bytes);
    }

    /// <summary>
    /// Imports one attachment's pages. A file that opened fine can still fail on AddPage (a damaged page tree, a restricted
    /// document): that is a bad attachment, so its pages are rolled back and it is skipped with a warning, the report goes on.
    /// Used by the plain and the book path alike; rendered report parts use <see cref="ImportPdfPages"/> and let a failure surface.
    /// </summary>
    private void ImportAttachmentPages(PdfDocument output, byte[] pdfBytes)
    {
        var before = output.PageCount;
        try
        {
            ImportPdfPages(output, pdfBytes);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to import the pages of a PDF attachment — skipping it");
            while (output.PageCount > before)
                output.Pages.RemoveAt(output.PageCount - 1);
        }
    }

    /// <summary>
    /// The readable, importable PDFs of one slot (with the number of pages each adds), within the attachment caps. A file that is missing,
    /// over budget or not importable is skipped with a warning; the budget is only charged for files that were accepted.
    /// </summary>
    private async Task<List<(byte[] Bytes, int Pages)>> LoadSlotPdfsAsync(
        AttachmentSlotSegment slot,
        MergeBudget budget,
        CancellationToken cancellationToken)
    {
        var accepted = new List<(byte[] Bytes, int Pages)>();
        foreach (var documentId in slot.DocumentIds)
        {
            if (budget.Count >= MaxAttachments || budget.Bytes >= _maxTotalAttachmentBytes)
            {
                logger.LogWarning(
                    "Attachment cap reached ({Count} files / {Bytes} bytes) — skipping remaining attachments in slot '{Slot}'",
                    budget.Count, budget.Bytes, slot.SlotName);
                break;
            }

            var filePath = await attachmentSource.GetFilePathAsync(documentId, cancellationToken);
            if (filePath is null || !File.Exists(filePath))
            {
                logger.LogWarning(
                    "Attachment file not found for DocumentId {DocumentId} in slot '{Slot}' — skipping",
                    documentId, slot.SlotName);
                continue;
            }

            // Check what this file would cost before reading it. The budget test at the top of the
            // loop only knows what earlier attachments have already spent, so the first attachment
            // passes it whatever its size — a single 1 GB PDF went straight into memory. Attaching
            // one that big is refused at attach time (FileStorageConfiguration.MaxAppendixFileSizeBytes);
            // this is the net under that, for rows that predate the rule.
            var fileLength = new FileInfo(filePath).Length;
            if (budget.Bytes + fileLength > _maxTotalAttachmentBytes)
            {
                logger.LogWarning(
                    "Attachment {DocumentId} ({Bytes} bytes) would exceed the {Cap} byte attachment budget in slot '{Slot}' — skipping",
                    documentId, fileLength, _maxTotalAttachmentBytes, slot.SlotName);
                continue;
            }

            try
            {
                var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
                // The validity check is a trial import into a throwaway document: a file that is not a readable PDF, or whose pages
                // cannot be imported, is skipped HERE, before the budget is charged, so a bad attachment never uses up the cap.
                // The page count it returns is what the book numbers from.
                if (ImportablePageCount(bytes) is not { } pages)
                    continue;
                accepted.Add((bytes, pages));
                budget.Count++;
                budget.Bytes += bytes.LongLength;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex,
                    "Failed to import PDF attachment {DocumentId} from '{FilePath}' — skipping",
                    documentId, filePath);
            }
        }
        return accepted;
    }

    // Running total across all attachment slots in one report (per-request, local state).
    private sealed class MergeBudget
    {
        public int Count;
        public long Bytes;
    }

    private static void ImportPdfPages(PdfDocument output, byte[] pdfBytes)
    {
        using var ms = new MemoryStream(pdfBytes);
        using var source = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
        for (var i = 0; i < source.PageCount; i++)
            output.AddPage(source.Pages[i]);
    }
}
