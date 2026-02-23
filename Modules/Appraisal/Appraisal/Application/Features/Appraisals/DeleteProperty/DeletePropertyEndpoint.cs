namespace Appraisal.Application.Features.Appraisals.DeleteProperty;

public class DeletePropertyEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapDelete("/appraisal/{appraisalId:guid}/{propertyId:guid}",
                async (Guid appraisalId, Guid propertyId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new DeletePropertyCommand(appraisalId, propertyId);
                    var result = await sender.Send(command, cancellationToken);
                    return Results.Ok(result.IsSuccess);
                });
    }
}
