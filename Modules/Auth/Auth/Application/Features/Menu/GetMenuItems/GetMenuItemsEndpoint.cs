using Auth.Application.Features.Menu.Dtos;

namespace Auth.Application.Features.Menu.GetMenuItems;

public class GetMenuItemsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/menus",
                async (string? scope, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetMenuItemsQuery(scope), cancellationToken);
                    // Return the tree (roots with nested children) directly as an array.
                    // The frontend admin client expects MenuItemAdminDto[] (no envelope).
                    return Results.Ok(result.Items);
                })
            .WithName("GetMenuItems")
            .Produces<List<MenuItemAdminDto>>()
            .WithSummary("List menu items (admin)")
            .WithDescription("Returns the menu tree (roots with nested children) for the given scope (Main or Appraisal), unfiltered by permissions.")
            .WithTags("Menu")
            .RequireAuthorization("CanManageMenus");
    }
}
