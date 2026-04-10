using Auth.Application.Features.Menu.Dtos;

namespace Auth.Application.Features.Menu.GetMenuItemById;

public class GetMenuItemByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/admin/menus/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new GetMenuItemByIdQuery(id), cancellationToken);
                    // Return the item directly (no envelope) — frontend expects MenuItemAdminDto.
                    return Results.Ok(result.Item);
                })
            .WithName("GetMenuItemById")
            .Produces<MenuItemAdminDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get a menu item by id (admin)")
            .WithTags("Menu")
            .RequireAuthorization("CanManageMenus");
    }
}
