namespace Auth.Application.Features.Menu.DeleteMenuItem;

public class DeleteMenuItemEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/admin/menus/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    await sender.Send(new DeleteMenuItemCommand(id), cancellationToken);
                    return Results.NoContent();
                })
            .WithName("DeleteMenuItem")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Delete a menu item (admin)")
            .WithDescription("Deletes a non-system menu item. System items cannot be deleted.")
            .WithTags("Menu")
            .RequireAuthorization("CanManageMenus");
    }
}
