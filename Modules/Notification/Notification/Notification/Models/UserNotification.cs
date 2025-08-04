namespace Notification.Notification.Models;

public class UserNotification
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;
    public NotificationType Type { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
    public string? ActionUrl { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}