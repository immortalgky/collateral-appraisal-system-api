namespace Appraisal.Domain.Quotations;

/// <summary>
/// Shared string constants for quotation documents.
/// Values must stay in sync with the Document module's type/category registry
/// and with the ReportDefinitions registered in the Reporting module.
/// Do NOT change the values — the frontend uploads with the same strings.
/// </summary>
public static class QuotationDocumentConstants
{
    // Document module type/category used when persisting generated PDFs.
    public const string DocumentType = "QUOTATION";
    public const string DocumentCategory = "quotation";

    // Report key registered in the Reporting module's ReportDefinitions.
    public const string SummaryReportKey = "quotation-summary";
}
