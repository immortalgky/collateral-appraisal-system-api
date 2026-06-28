namespace Parameter.DocumentRequirements.Features.SetScopeRequirements;

public record SetScopeRequirementsRequest(
    string? PropertyTypeCode,
    string? PurposeCode,
    List<ScopeRequirementItem> Items);

public class SetScopeRequirementsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/document-requirements/scope", async (
                SetScopeRequirementsRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var command = new SetScopeRequirementsCommand(
                    request.PropertyTypeCode,
                    request.PurposeCode,
                    request.Items ?? []);

                await sender.Send(command, cancellationToken);

                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithName("SetScopeRequirements")
            .WithSummary("Set all document requirements for a collateral-type/purpose scope")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Document Requirements");
    }
}
