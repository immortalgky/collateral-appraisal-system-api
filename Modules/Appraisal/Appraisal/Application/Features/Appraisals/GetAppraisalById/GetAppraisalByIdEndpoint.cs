using Carter;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalById;

public class GetAppraisalByIdEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/appraisals/{id:guid}",
                async (Guid id, [FromQuery] string? include, ISender sender,
                    IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json, CancellationToken cancellationToken) =>
                {
                    var parts = AppraisalIncludeParser.Parse(include);
                    var query = new GetAppraisalByIdQuery(id, parts);

                    var result = await sender.Send(query, cancellationToken);

                    return Results.Ok(AppraisalByIdResponseWriter.Build(result, parts, json.Value.SerializerOptions));
                }
            )
            .WithName("GetAppraisalById")
            .Produces<GetAppraisalByIdResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get appraisal by ID")
            .WithDescription(
                "Retrieves a single appraisal by its ID. Opt in with include=request (the request data: previous " +
                "appraisal snapshot, detail, customers, properties, titles) and/or include=documents (the request's " +
                "files with a suggested type). An unknown include value is a 400.")
            .WithTags("Appraisal")
            .RequireAuthorization();
    }
}