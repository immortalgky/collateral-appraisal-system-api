namespace Notification.Infrastructure.Email.Templates;

/// <summary>Renders typed email templates to HTML strings.</summary>
public interface IEmailTemplateRenderer
{
    /// <summary>Renders the quotation-sent email.</summary>
    string QuotationSent(string subject, string? adminContent);

    /// <summary>Renders the meeting-invitation email.</summary>
    string MeetingInvitation(string subject, string? adminContent);

    /// <summary>
    /// Renders the "quotation fee notice" email sent to the RM to pick the winning appraisal
    /// company. Includes a companies × appraisals fee-comparison table.
    /// </summary>
    string QuotationFeeNotice(string subject, QuotationFeeNoticeModel model);

    /// <summary>Renders the "document follow-up" email (additional documents requested).</summary>
    string DocumentFollowupNotice(string subject, DocumentFollowupNoticeModel model);

    /// <summary>Renders the "route-back to appraisal-initiation" email (fix collateral data).</summary>
    string RouteBackNotice(string subject, RouteBackNoticeModel model);
}

/// <summary>Render model for the quotation fee-comparison email.</summary>
public sealed record QuotationFeeNoticeModel(
    string RmName,
    string? CustomerName,
    IReadOnlyList<QuotationFeeNoticeColumn> Columns,
    IReadOnlyList<QuotationFeeNoticeRow> Rows,
    string AdminName);

/// <summary>A table column header: report number, then property type + province name on their own lines.</summary>
public sealed record QuotationFeeNoticeColumn(string ReportNumber, string? PropertyType, string? Province);

/// <summary>A company row. <see cref="Cells"/> aligns positionally with the model's Columns.</summary>
public sealed record QuotationFeeNoticeRow(string CompanyName, IReadOnlyList<string> Cells, string Total);

/// <summary>Render model for the document-followup email.</summary>
public sealed record DocumentFollowupNoticeModel(
    string RmName,
    string? CustomerName,
    string? AppraisalNumber,
    IReadOnlyList<DocumentFollowupNoticeItem> Items,
    string AdminName);

/// <summary>A requested document: name shown as a header, notes as the body below.</summary>
public sealed record DocumentFollowupNoticeItem(string DocumentName, string? Notes);

/// <summary>Render model for the route-back email. Body shows only the greeting + the sender's
/// comment; the footer contact is the sender's full name + phone.</summary>
public sealed record RouteBackNoticeModel(
    string RmName,
    string? Remark,
    string SenderName,
    string? SenderPhone);
