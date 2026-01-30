using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Shared.Pagination;

namespace Appraisal.Application.Features.Committees.GetCommittees;

public class GetCommitteesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/committees",
                async (
                    [AsParameters] PaginationRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var query = new GetCommitteesQuery(request);

                    var result = await sender.Send(query, cancellationToken);

                    var response = result.Adapt<GetCommitteesResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("GetCommittees")
            .Produces<GetCommitteesResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get all committees")
            .WithDescription("Retrieves all committees with pagination support.")
            .WithTags("Committee");
    }
}