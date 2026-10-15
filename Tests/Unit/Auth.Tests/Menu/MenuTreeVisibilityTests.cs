using Auth.Application.Features.Menu.Dtos;
using Auth.Application.Features.Menu.GetMyMenu;
using Auth.Domain.Menu;

namespace Auth.Tests.Menu;

public class MenuTreeVisibilityTests
{
    private static MenuItem Item(string key, Guid? parentId, string? viewCode, string? path = "/x") =>
        MenuItem.Create(key, MenuScope.Main, parentId, path, MenuIcon.Create("bars", IconStyle.Solid), null, 0,
            viewCode, null, [MenuItemTranslation.Create("en", key)]);

    private static List<string> Keys(IReadOnlyList<MenuItem> items, params string[] permissions) =>
        AllKeys(GetMyMenuQueryHandler.BuildFilteredTree(items, MenuScope.Main, [.. permissions], overrides: null));

    [Fact]
    public void Ungated_group_shows_when_any_child_is_visible()
    {
        var group = Item("group", null, null, path: null);
        var items = new[] { group, Item("a", group.Id, "A"), Item("b", group.Id, "B") };

        Assert.Equal(["group", "b"], Keys(items, "B"));
    }

    [Fact]
    public void Ungated_group_hides_when_no_child_is_visible()
    {
        var group = Item("group", null, null, path: null);
        var items = new[] { group, Item("a", group.Id, "A") };

        Assert.Empty(Keys(items, "OTHER"));
    }

    [Fact]
    public void Gated_group_still_hides_its_subtree()
    {
        var group = Item("group", null, "GROUP", path: null);
        var items = new[] { group, Item("a", group.Id, "A") };

        Assert.Empty(Keys(items, "A"));
        Assert.Equal(["group", "a"], Keys(items, "GROUP", "A"));
    }

    [Fact]
    public void Ungated_leaf_never_shows()
    {
        // A gate-less item with a Path is refused by MenuItem; a path-less one with no children never shows.
        Assert.Empty(Keys([Item("leaf", null, null, path: null)], "A"));
    }

    private static List<string> AllKeys(IEnumerable<MenuTreeNodeDto> nodes) =>
        nodes.SelectMany(n => new[] { n.ItemKey }.Concat(AllKeys(n.Children))).ToList();

    [Fact]
    public void Nested_ungated_groups_follow_the_same_rule_at_every_level()
    {
        // The seeded shape: main.access -> main.user-management / main.oauth -> pages.
        var access = Item("access", null, null, path: null);
        var userManagement = Item("user-management", access.Id, null, path: null);
        var oauth = Item("oauth", access.Id, null, path: null);
        var items = new[]
        {
            access, userManagement, oauth,
            Item("menus", userManagement.Id, "MENU_MANAGE"),
            Item("users", userManagement.Id, "USER_MANAGE"),
            Item("clients", oauth.Id, "OAUTH_CLIENTS_MANAGE"),
        };

        // MENU_MANAGE alone reaches Menus through two ungated levels; the empty OAuth sub-group is dropped.
        Assert.Equal(["access", "user-management", "menus"], Keys(items, "MENU_MANAGE"));
    }
}
