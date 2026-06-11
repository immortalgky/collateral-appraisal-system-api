using System.Text.Json;

namespace Workflow.Meetings.Domain;

public class MeetingInvitationEmail
{
    public Guid Id { get; private set; }
    public Guid MeetingId { get; private set; }
    public string From { get; private set; } = string.Empty;
    public string? To { get; private set; }
    public string? Cc { get; private set; }
    public string? Bcc { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string? Content { get; private set; }
    public string? Attachments { get; private set; } // JSON array of strings

    private MeetingInvitationEmail() { }

    public static MeetingInvitationEmail Create(
        Guid meetingId, string from, string? to,
        string? cc, string? bcc, string subject, string? content, string[]? attachments)
    {
        return new MeetingInvitationEmail
        {
            Id = Guid.CreateVersion7(),
            MeetingId = meetingId,
            From = from,
            To = to,
            Cc = cc,
            Bcc = bcc,
            Subject = subject,
            Content = content,
            Attachments = attachments?.Length > 0
                ? JsonSerializer.Serialize(attachments)
                : null
        };
    }
}
