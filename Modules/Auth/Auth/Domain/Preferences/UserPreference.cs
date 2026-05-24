namespace Auth.Domain.Preferences;

public class UserPreference
{
    public Guid UserId { get; private set; }
    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public DateTime UpdatedOn { get; private set; }

    private UserPreference() { }

    public static UserPreference Create(Guid userId, string key, string valueJson)
    {
        return new UserPreference
        {
            UserId = userId,
            Key = key,
            Value = valueJson,
            UpdatedOn = DateTime.UtcNow
        };
    }

    public void Update(string valueJson)
    {
        Value = valueJson;
        UpdatedOn = DateTime.UtcNow;
    }
}
