namespace Notification.Infrastructure.Email.Templates;

/// <summary>Renders typed email templates to HTML strings.</summary>
public interface IEmailTemplateRenderer
{
    /// <summary>Renders the quotation-sent email.</summary>
    string QuotationSent(string subject, string? adminContent);

    /// <summary>Renders the meeting-invitation email.</summary>
    string MeetingInvitation(string subject, string? adminContent);
}
