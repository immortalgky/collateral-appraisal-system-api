using Microsoft.Extensions.Options;
using System.Globalization;

namespace Shared.Time;

/// <summary>
/// Production implementation of IDateTimeProvider with timezone and culture support
/// </summary>
public class DateTimeProvider : IDateTimeProvider
{
    private readonly TimeZoneConfiguration _config;
    private readonly TimeZoneInfo _applicationTimeZone;
    private readonly CultureInfo _culture;

    public DateTimeProvider(IOptions<TimeZoneConfiguration> config)
    {
        _config = config.Value;
        
        // Set up timezone - default to UTC if configuration fails
        try
        {
            _applicationTimeZone = TimeZoneInfo.FindSystemTimeZoneById(_config.DefaultTimeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            // Try standard timezone names
            _applicationTimeZone = _config.DefaultTimeZone.ToUpper() switch
            {
                "UTC" or "GMT" => TimeZoneInfo.Utc,
                "EST" or "EASTERN" => TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"),
                "PST" or "PACIFIC" => TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"),
                "CST" or "CENTRAL" => TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"),
                "ICT" or "BANGKOK" => TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time"),
                _ => TimeZoneInfo.Utc
            };
        }

        // Set up culture
        try
        {
            _culture = new CultureInfo(_config.Culture);
        }
        catch (CultureNotFoundException)
        {
            _culture = CultureInfo.InvariantCulture;
        }
    }

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime Now => DateTime.Now;

    public DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    public TimeOnly TimeOfDay => TimeOnly.FromDateTime(DateTime.Now);

    public DateTime ApplicationNow 
    {
        get
        {
            if (_config.ForceUtc)
                return UtcNow;

            return TimeZoneInfo.ConvertTimeFromUtc(UtcNow, _applicationTimeZone);
        }
    }

    public TimeZoneInfo ApplicationTimeZone => _applicationTimeZone;

    public DateTime ToApplicationTime(DateTime utcDateTime)
    {
        if (_config.ForceUtc)
            return utcDateTime;

        if (utcDateTime.Kind != DateTimeKind.Utc)
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, _applicationTimeZone);
    }

    public DateTime ToUtc(DateTime applicationDateTime)
    {
        if (_config.ForceUtc)
            return applicationDateTime;

        if (applicationDateTime.Kind == DateTimeKind.Utc)
            return applicationDateTime;

        return TimeZoneInfo.ConvertTimeToUtc(applicationDateTime, _applicationTimeZone);
    }

    public string FormatDateTime(DateTime dateTime, string? format = null)
    {
        var formatString = format ?? _config.DateTimeFormat;
        return dateTime.ToString(formatString, _culture);
    }
}