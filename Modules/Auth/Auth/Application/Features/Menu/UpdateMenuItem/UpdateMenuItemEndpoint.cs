namespace Auth.Application.Features.Menu.UpdateMenuItem;

public class UpdateMenuItemEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/admin/menus/{id:guid}",
                async (Guid id, UpdateMenuItemRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new UpdateMenuItemCommand(
                        id,
                        request.ParentId,
                        request.Path,
                        request.IconName,
                        request.IconStyle,
                        request.IconColor,
                        request.SortOrder,
                        request.ViewPermissionCode,
                        request.EditPermissionCode,
                        request.Translations);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(new UpdateMenuItemResponse(result.Success));
                })
            .WithName("UpdateMenuItem")
            .Produces<UpdateMenuItemResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Update a menu item (admin)")
            .WithTags("Menu")
            .RequireAuthorization("CanManageMenus");
    }
}
