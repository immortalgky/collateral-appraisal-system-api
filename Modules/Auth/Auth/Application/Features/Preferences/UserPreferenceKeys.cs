namespace Auth.Application.Features.Preferences;

public static class UserPreferenceKeys
{
    public const string MenuFavorites = "menu.favorites";
    public const string TaskColumns   = "task.columns.all";

    public static readonly HashSet<string> All =
        new(StringComparer.Ordinal) { MenuFavorites, TaskColumns };
}
