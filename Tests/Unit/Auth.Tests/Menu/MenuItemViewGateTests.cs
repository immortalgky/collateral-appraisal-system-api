using Auth.Domain.Menu;
using Shared.Exceptions;

namespace Auth.Tests.Menu;

public class MenuItemViewGateTests
{
    private static MenuItem Create(string? path, string? viewCode, string? viewPrefix = null) =>
        MenuItem.Create("main.x", MenuScope.Main, null, path, MenuIcon.Create("bars", IconStyle.Solid), null, 0,
            viewCode, null, [MenuItemTranslation.Create("en", "X")], viewPermissionPrefix: viewPrefix);

    [Fact]
    public void Group_without_path_may_leave_the_view_permission_empty() =>
        Assert.Null(Create(path: null, viewCode: null).ViewPermissionCode);

    [Fact]
    public void Page_with_path_needs_a_view_permission_or_prefix()
    {
        Assert.Throws<DomainException>(() => Create(path: "/x", viewCode: null));
        Assert.NotNull(Create(path: "/x", viewCode: "X_VIEW"));
        Assert.NotNull(Create(path: "/x", viewCode: null, viewPrefix: "X_"));
    }

    [Fact]
    public void Update_enforces_the_same_rule()
    {
        var item = Create(path: "/x", viewCode: "X_VIEW");
        Assert.Throws<DomainException>(() =>
            item.Update("/x", MenuIcon.Create("bars", IconStyle.Solid), null, 0, null, null));
    }
}
