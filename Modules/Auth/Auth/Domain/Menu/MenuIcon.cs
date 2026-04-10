namespace Auth.Domain.Menu;

public record MenuIcon(string Name, IconStyle Style)
{
    public static MenuIcon Create(string name, IconStyle style)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Icon name is required", nameof(name));
        return new MenuIcon(name.Trim(), style);
    }
}
