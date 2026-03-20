namespace Appraisal.Application.Features.Appraisals.CopyPropertyToGroup;

public class CopyPropertyToGroupEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/properties/{propertyId:guid}/copy",
                async (
                    Guid appraisalId,
                    Guid propertyId,
                    CopyPropertyToGroupRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new CopyPropertyToGroupCommand(
                        appraisalId,
                        propertyId,
                        request.TargetGroupId);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CopyPropertyToGroupResponse>();

                    return Results.Created(
                        $"/appraisals/{appraisalId}/properties/{response.PropertyId}",
                        response);
                }
            )
            .WithName("CopyPropertyToGroup")
            .Produces<CopyPropertyToGroupResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Copy a property to a group")
            .WithDescription("Deep-copies an existing property and adds it to a target group.")
            .WithTags("Appraisal Properties");
    }
}
