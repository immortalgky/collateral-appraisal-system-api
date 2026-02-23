using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.CreateAppraisal;

public class CreateAppraisalEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals",
                async (
                    CreateAppraisalRequest request,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var command = request.Adapt<CreateAppraisalCommand>();

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<CreateAppraisalResponse>();

                    return Results.Ok(response);
                }
            )
            .WithName("CreateAppraisal")
            .Produces<CreateAppraisalResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Create new appraisal")
            .WithDescription("Create a new appraisal for a request.")
            .WithTags("Appraisal");
    }
}