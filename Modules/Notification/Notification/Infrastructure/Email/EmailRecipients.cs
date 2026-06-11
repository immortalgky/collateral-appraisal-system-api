namespace Notification.Infrastructure.Email;

/// <summary>
/// Shared parsing of an admin-entered recipient string into individual addresses.
/// Single source of truth for the recipient delimiter convention (',' or ';').
/// </summary>
public static class EmailRecipients
{
    public static List<string> Parse(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                 .ToList();
}
