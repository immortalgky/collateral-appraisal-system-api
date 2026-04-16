using Shared.DDD;

namespace Auth.Domain.Menu;

public class MenuItemTranslation : Entity<Guid>
{
    public Guid MenuItemId { get; internal set; }
    public string LanguageCode { get; private set; } = default!;
    public string Label { get; private set; } = default!;

    private MenuItemTranslation() { }

    public static MenuItemTranslation Create(string languageCode, string label)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            throw new ArgumentException("LanguageCode required", nameof(languageCode));
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Label required", nameof(label));

        return new MenuItemTranslation
        {
            Id = Guid.CreateVersion7(),
            LanguageCode = languageCode.Trim().ToLowerInvariant(),
            Label = label.Trim()
        };
    }
}
