namespace Common.Domain.Configuration;

/// <summary>
/// A system-wide configuration entry managed by administrators.
/// Keyed by a unique string key; value stored as string with a declared ValueType for parsing.
/// </summary>
public class SystemConfiguration
{
    public const int MaxKeyLength = 100;
    public const int MaxValueTypeLength = 20;
    public const int MaxCategoryLength = 50;

    public Guid Id { get; private set; }

    /// <summary>Unique configuration key, e.g. "BlockReappraisalIntervalYears".</summary>
    public string Key { get; private set; } = null!;

    /// <summary>String representation of the value.</summary>
    public string Value { get; private set; } = null!;

    /// <summary>Declared type for parsing: "int", "string", "bool", or "decimal". Default "string".</summary>
    public string ValueType { get; private set; } = "string";

    public string? Description { get; private set; }

    public string? Category { get; private set; }

    public bool IsActive { get; private set; } = true;

    // Required by EF Core
    private SystemConfiguration()
    {
    }

    public static SystemConfiguration Create(
        string key,
        string value,
        string valueType = "string",
        string? description = null,
        string? category = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Configuration key cannot be empty.", nameof(key));

        if (key.Length > MaxKeyLength)
            throw new ArgumentException(
                $"Configuration key exceeds the maximum allowed length of {MaxKeyLength} characters.",
                nameof(key));

        return new SystemConfiguration
        {
            Id = Guid.CreateVersion7(),
            Key = key.Trim(),
            Value = value ?? string.Empty,
            ValueType = string.IsNullOrWhiteSpace(valueType) ? "string" : valueType.Trim().ToLowerInvariant(),
            Description = description,
            Category = category,
            IsActive = true
        };
    }

    public void UpdateValue(string value)
    {
        Value = value ?? string.Empty;
    }

    public void UpdateDescription(string? description)
    {
        Description = description;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}
