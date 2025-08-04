namespace Shared.Time;

/// <summary>
/// Configuration for timezone and culture settings
/// </summary>
public class TimeZoneConfiguration
{
    public const string SectionName = "TimeZone";

    /// <summary>
    /// The application's default timezone (e.g., "UTC", "America/New_York", "Asia/Bangkok")
    /// </summary>
    public string DefaultTimeZone { get; set; } = "UTC";

    /// <summary>
    /// Whether to always return UTC time regardless of timezone setting
    /// </summary>
    public bool ForceUtc { get; set; } = true;

    /// <summary>
    /// Culture for date/time formatting (e.g., "en-US", "th-TH")
    /// </summary>
    public string Culture { get; set; } = "en-US";

    /// <summary>
    /// Date format pattern
    /// </summary>
    public string DateFormat { get; set; } = "yyyy-MM-dd";

    /// <summary>
    /// Time format pattern
    /// </summary>
    public string TimeFormat { get; set; } = "HH:mm:ss";

    /// <summary>
    /// DateTime format pattern
    /// </summary>
    public string DateTimeFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
}