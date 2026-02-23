using Carter;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.Appraisals.GetLawAndRegulations;

public class GetLawAndRegulationsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/appraisals/{appraisalId:guid}/law-and-regulations",
                async (Guid appraisalId, ISender sender) =>
                {
                    var query = new GetLawAndRegulationsQuery(appraisalId);
                    var result = await sender.Send(query);
                    return Results.Ok(result);
                })
            .WithName("GetLawAndRegulations")
            .Produces<GetLawAndRegulationsResult>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Get law and regulations for an appraisal")
            .WithDescription("Returns all law and regulation entries with their images for the given appraisal.")
            .WithTags("LawAndRegulation");
    }
}
