using Carter;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Appraisal.Application.Features.Appraisals.SaveLawAndRegulations;

public class SaveLawAndRegulationsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPut("/appraisals/{appraisalId:guid}/law-and-regulations",
                async (Guid appraisalId, SaveLawAndRegulationsRequest request, ISender sender) =>
                {
                    var command = new SaveLawAndRegulationsCommand(
                        appraisalId,
                        request.Items.AsReadOnly());

                    var result = await sender.Send(command);
                    var response = result.Adapt<SaveLawAndRegulationsResponse>();
                    return Results.Ok(response);
                })
            .WithName("SaveLawAndRegulations")
            .Produces<SaveLawAndRegulationsResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Save law and regulations for an appraisal")
            .WithDescription("Batch saves all law and regulation entries. Items not in the request are deleted.")
            .WithTags("LawAndRegulation");
    }
}
