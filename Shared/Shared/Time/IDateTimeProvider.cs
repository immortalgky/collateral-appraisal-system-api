namespace Shared.Time;

/// <summary>
/// Provides the current date and time in a testable way with timezone and culture support
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>
    /// Gets the current date and time in UTC
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Gets the current date and time in local time
    /// </summary>
    DateTime Now { get; }

    /// <summary>
    /// Gets today's date
    /// </summary>
    DateOnly Today { get; }

    /// <summary>
    /// Gets the current time
    /// </summary>
    TimeOnly TimeOfDay { get; }

    /// <summary>
    /// Gets the current date and time in the application's configured timezone
    /// </summary>
    DateTime ApplicationNow { get; }

    /// <summary>
    /// Converts UTC time to application timezone
    /// </summary>
    DateTime ToApplicationTime(DateTime utcDateTime);

    /// <summary>
    /// Converts application time to UTC
    /// </summary>
    DateTime ToUtc(DateTime applicationDateTime);

    /// <summary>
    /// Formats a DateTime using the configured culture and format
    /// </summary>
    string FormatDateTime(DateTime dateTime, string? format = null);

    /// <summary>
    /// Gets the configured application timezone info
    /// </summary>
    TimeZoneInfo ApplicationTimeZone { get; }
}