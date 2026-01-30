namespace Appraisal.Domain.Settings;

/// <summary>
/// Module-level settings for appraisal configuration.
/// </summary>
public class AppraisalSettings : Entity<Guid>
{
    public string SettingKey { get; private set; } = null!;
    public string SettingValue { get; private set; } = null!;
    public string? Description { get; private set; }

    private AppraisalSettings()
    {
    }

    public static AppraisalSettings Create(
        string key,
        string value,
        string? description = null)
    {
        return new AppraisalSettings
        {
            Id = Guid.NewGuid(),
            SettingKey = key,
            SettingValue = value,
            Description = description
        };
    }

    public void UpdateValue(string value)
    {
        SettingValue = value;
    }
}