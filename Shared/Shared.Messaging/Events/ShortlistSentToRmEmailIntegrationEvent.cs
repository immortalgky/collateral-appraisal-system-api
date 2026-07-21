namespace Shared.Messaging.Events;

/// <summary>
/// Published by the Appraisal module (via outbox in SendShortlistToRmCommandHandler) when
/// the admin sends the shortlisted quotations to the RM to pick the winning appraisal company.
/// Consumed by the Notification module, which resolves the RM email + company names via
/// <c>IUserLookupService</c>, renders the fee-comparison table, and delivers it via SMTP.
/// <para>
/// Domain data (customer name, per-appraisal report numbers, per-company fee amounts) is
/// resolved at the source because the Appraisal aggregate owns it. Identity data (RM email,
/// company display names) is resolved in the consumer because only the Notification module
/// references <c>Auth.Contracts</c>.
/// </para>
/// </summary>
public record ShortlistSentToRmEmailIntegrationEvent : IntegrationEvent
{
    /// <summary>The quotation request that triggered this send — used as the send-log ReferenceId.</summary>
    public Guid QuotationRequestId { get; init; }

    /// <summary>RM bank code (employee id, e.g. P5229). The consumer resolves this to an email + display name.</summary>
    public string? RmUsername { get; init; }

    /// <summary>Bank code of the admin who sent the shortlist. The consumer resolves this to the signature name.</summary>
    public string? AdminUsername { get; init; }

    /// <summary>Customer display name (resolved at source from request.RequestCustomers).</summary>
    public string? CustomerName { get; init; }

    /// <summary>One column per shortlisted appraisal (the fee table's columns).</summary>
    public IReadOnlyList<QuotationEmailAppraisalColumn> Columns { get; init; } = [];

    /// <summary>One row per shortlisted company (the fee table's rows).</summary>
    public IReadOnlyList<QuotationEmailCompanyRow> Rows { get; init; } = [];
}

/// <summary>A single appraisal column in the fee-comparison table (report no. + property type + province name).</summary>
public sealed record QuotationEmailAppraisalColumn(
    Guid AppraisalId,
    string? ReportNumber,
    string? PropertyType,
    string? Province);

/// <summary>
/// A single company row in the fee-comparison table. <see cref="CompanyId"/> is resolved to a
/// display name in the consumer. <see cref="AmountByAppraisalId"/> maps each column's AppraisalId
/// to that company's net fee (incl. VAT) for that appraisal; <see cref="Total"/> is the row total.
/// </summary>
public sealed record QuotationEmailCompanyRow(
    Guid CompanyId,
    IReadOnlyDictionary<Guid, decimal> AmountByAppraisalId,
    decimal Total);
