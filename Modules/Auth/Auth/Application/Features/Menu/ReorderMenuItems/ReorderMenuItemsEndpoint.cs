namespace Auth.Application.Features.Menu.ReorderMenuItems;

public class ReorderMenuItemsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/admin/menus/reorder",
                async (ReorderMenuItemsRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new ReorderMenuItemsCommand(request.Items), cancellationToken);
                    return Results.Ok(new ReorderMenuItemsResponse(result.Success));
                })
            .WithName("ReorderMenuItems")
            .Produces<ReorderMenuItemsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Bulk reorder menu items (admin)")
            .WithTags("Menu")
            .RequireAuthorization("CanManageMenus");
    }
}
