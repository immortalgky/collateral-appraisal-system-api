using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Committees.CreateCommittee;

public class CreateCommitteeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/committees",
                async (
                    CreateCommitteeRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateCommitteeCommand>();

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateCommitteeResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("CreateCommittee")
            .Produces<CreateCommitteeResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create new committee")
            .WithDescription("Create a new committee for appraisal approval workflows.")
            .WithTags("Committee");
    }
}