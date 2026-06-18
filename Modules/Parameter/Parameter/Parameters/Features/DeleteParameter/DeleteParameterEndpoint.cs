public class DeleteParameterEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/parameter/{parId:long}",
            async (long parId, ISender sender, CancellationToken cancellationToken) =>
            {
                var command = new DeleteParameterCommand(parId);
                var result = await sender.Send(command, cancellationToken);
                return Results.Ok(result.IsSuccess);
            }) .WithName("DeleteParameter")
            .Produces<DeleteParameterResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Parameter")
            .WithSummary("Delete parameter by parID")
            .WithDescription("Deletes a parameter by its ID.")
            .AllowAnonymous();
    }
}
