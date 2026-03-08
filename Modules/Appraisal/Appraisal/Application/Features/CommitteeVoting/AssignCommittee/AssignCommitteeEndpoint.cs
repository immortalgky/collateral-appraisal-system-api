namespace Appraisal.Application.Features.CommitteeVoting.AssignCommittee;

public class AssignCommitteeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/reviews/assign-committee",
                async (
                    Guid appraisalId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new AssignCommitteeCommand(appraisalId);
                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<AssignCommitteeResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("AssignCommittee")
            .Produces<AssignCommitteeResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Assign committee for review")
            .WithDescription("Auto-routes appraisal to the correct committee based on total appraised value thresholds.")
            .WithTags("CommitteeVoting");
    }
}
