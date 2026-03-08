namespace Appraisal.Application.Features.CommitteeVoting.SubmitVote;

public class SubmitVoteEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/reviews/{reviewId:guid}/votes",
                async (
                    Guid appraisalId,
                    Guid reviewId,
                    SubmitVoteRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = new SubmitVoteCommand(
                        appraisalId,
                        reviewId,
                        request.Vote,
                        request.Remark);

                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<SubmitVoteResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("SubmitVote")
            .Produces<SubmitVoteResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Submit committee vote")
            .WithDescription("Submit a vote (Approve, Reject, Abstain, or RouteBack) for a committee-level review.")
            .WithTags("CommitteeVoting");
    }
}
