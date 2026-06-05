using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using Reporting.Application.Services;

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
internal sealed class PdfSharpAssembler(
    IPdfRenderer pdfRenderer,
    IReportAttachmentSource attachmentSource,
    ILogger<PdfSharpAssembler> logger) : IPdfAssembler
{
    // Safety caps so a request with many/large uploaded PDFs can't exhaust memory
    // on the shared app server. Beyond these, remaining attachments are skipped with
    // a warning (the generated form itself always renders).
    private const int MaxAttachments = 50;
    private const long MaxTotalAttachmentBytes = 100L * 1024 * 1024; // 100 MB

    public async Task<byte[]> AssembleAsync(
        IReadOnlyList<RenderSegment> segments,
        CancellationToken cancellationToken)
    {
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
        foreach (var documentId in slot.DocumentIds)
        {
            if (budget.Count >= MaxAttachments || budget.Bytes >= MaxTotalAttachmentBytes)
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

            try
            {
                var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
                ImportPdfPages(output, bytes);
                budget.Count++;
                budget.Bytes += bytes.LongLength;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to import PDF attachment {DocumentId} from '{FilePath}' — skipping",
                    documentId, filePath);
            }
        }
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
