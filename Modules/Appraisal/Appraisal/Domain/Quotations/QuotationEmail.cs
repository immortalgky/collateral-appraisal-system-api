namespace Appraisal.Domain.Quotations;

public class QuotationEmail
{
    public Guid Id { get; private set; }
    public Guid QuotationRequestId { get; private set; }
    public string From { get; private set; } = string.Empty;
    public string To { get; private set; } = string.Empty;
    public string? Cc { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string? Content { get; private set; }

    private QuotationEmail() { }

    public static QuotationEmail Create(
        Guid quotationRequestId, string from, string to,
        string? cc, string subject, string? content)
    {
        return new QuotationEmail
        {
            Id = Guid.CreateVersion7(),
            QuotationRequestId = quotationRequestId,
            From = from,
            To = to,
            Cc = cc,
            Subject = subject,
            Content = content
        };
    }
}
