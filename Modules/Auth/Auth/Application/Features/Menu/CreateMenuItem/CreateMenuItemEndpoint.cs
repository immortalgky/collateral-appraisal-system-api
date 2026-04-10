namespace Auth.Application.Features.Menu.CreateMenuItem;

public class CreateMenuItemEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/admin/menus",
                async (CreateMenuItemRequest request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = request.Adapt<CreateMenuItemCommand>();
                    var result = await sender.Send(command, cancellationToken);
                    var response = new CreateMenuItemResponse(result.Id);
                    return Results.Created($"/admin/menus/{response.Id}", response);
                })
            .WithName("CreateMenuItem")
            .Produces<CreateMenuItemResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Create a menu item (admin)")
            .WithTags("Menu")
            .RequireAuthorization("CanManageMenus");
    }
}
