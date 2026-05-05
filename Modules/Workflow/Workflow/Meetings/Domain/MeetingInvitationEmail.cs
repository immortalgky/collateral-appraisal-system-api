using System.Text.Json;

namespace Workflow.Meetings.Domain;

public class MeetingInvitationEmail
{
    public Guid Id { get; private set; }
    public Guid MeetingId { get; private set; }
    public string From { get; private set; } = string.Empty;
    public string To { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string? Content { get; private set; }
    public string? Attachments { get; private set; } // JSON array of strings

    private MeetingInvitationEmail() { }

    public static MeetingInvitationEmail Create(
        Guid meetingId, string from, string to,
        string subject, string? content, string[]? attachments)
    {
        return new MeetingInvitationEmail
        {
            Id = Guid.CreateVersion7(),
            MeetingId = meetingId,
            From = from,
            To = to,
            Subject = subject,
            Content = content,
            Attachments = attachments?.Length > 0
                ? JsonSerializer.Serialize(attachments)
                : null
        };
    }
}
