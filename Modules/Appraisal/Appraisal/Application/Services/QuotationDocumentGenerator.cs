using Appraisal.Domain.Quotations;
using Document.Contracts;
using Reporting.Contracts;
using Shared.Data.Outbox;
using Shared.Identity;
using Shared.Messaging.Events;
using Shared.Time;

namespace Appraisal.Application.Services;

/// <summary>
/// Generates a quotation summary PDF report, persists it as a Document, links it to the
/// quotation aggregate, and enqueues the DocumentLinked outbox event — all in one call.
/// The caller's transactional Unit-of-Work commits the quotation-side changes;
/// IDocumentCreator keeps its own Document-module SaveChanges (unchanged behaviour).
/// </summary>
public interface IQuotationDocumentGenerator
{
    /// <summary>
    /// Generates and links a "Summary" document for the quotation.
    /// Throws if PDF generation fails — let exceptions propagate to the caller.
    /// </summary>
    Task<QuotationDocument> GenerateAndLinkAsync(
        QuotationRequest quotation,
        string documentType,
        CancellationToken ct);
}

internal sealed class QuotationDocumentGenerator(
    IReportPdfGenerator reportPdfGenerator,
    IDocumentCreator documentCreator,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IIntegrationEventOutbox outbox)
    : IQuotationDocumentGenerator
{
    // Maps the user-facing DocumentType to the registered report key.
    private static readonly Dictionary<string, string> ReportKeyMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "Summary", QuotationDocumentConstants.SummaryReportKey }
        };

    public async Task<QuotationDocument> GenerateAndLinkAsync(
        QuotationRequest quotation,
        string documentType,
        CancellationToken ct)
    {
        if (!ReportKeyMap.TryGetValue(documentType, out var reportKey))
            throw new ArgumentException(
                $"Unknown document type '{documentType}'. Valid values: Summary.",
                nameof(documentType));

        var userCode = currentUserService.UserCode
            ?? currentUserService.Username
            ?? "system";
        var userName = currentUserService.Username ?? "system";
        var now = dateTimeProvider.ApplicationNow;

        // Generate the PDF — synchronous in-process call, returns raw bytes.
        // Any exception (e.g. Puppeteer render failure) propagates to the caller.
        var reportFile = await reportPdfGenerator.GenerateAsync(reportKey, quotation.Id.ToString(), ct);

        // Quotation-specific filename so regenerated docs aren't all "quotation-summary.pdf".
        var fileName = BuildFileName(reportKey, quotation.QuotationNumber);

        // Persist as a real Document so it gets a stable id and can be downloaded later.
        var documentId = await documentCreator.CreateFromBytesAsync(
            reportFile.Bytes,
            fileName,
            reportFile.ContentType,
            documentType: QuotationDocumentConstants.DocumentType,
            documentCategory: QuotationDocumentConstants.DocumentCategory,
            uploadedBy: userCode,
            uploadedByName: userName,
            ct);

        // Link the document to the quotation aggregate.
        var data = new QuotationDocumentData(
            DocumentId: documentId,
            DocumentType: documentType,
            FileName: fileName,
            Source: "Generated",
            CreatedBy: userCode,
            CreatedAt: now);

        var quotationDoc = quotation.AddDocument(data);

        // Publish link event so the Document module increments ReferenceCount.
        outbox.Publish(
            new DocumentLinkedIntegrationEventV2(
                RequestId: quotation.Id,     // owner id — quotation id reuses the RequestId field
                DocumentId: documentId,
                DocumentType: documentType),
            correlationId: quotation.Id.ToString());

        return quotationDoc;
    }

    /// <summary>
    /// e.g. "quotation-summary-QT-47-2569.pdf"; falls back to the report key when no QuotationNumber.
    /// </summary>
    internal static string BuildFileName(string reportKey, string? quotationNumber)
    {
        if (string.IsNullOrWhiteSpace(quotationNumber))
            return $"{reportKey}.pdf";

        var safe = quotationNumber.Replace('/', '-').Replace('\\', '-').Trim();
        return $"{reportKey}-{safe}.pdf";
    }
}
