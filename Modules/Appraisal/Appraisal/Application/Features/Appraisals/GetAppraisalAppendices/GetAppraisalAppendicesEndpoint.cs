using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalAppendices;

public class GetAppraisalAppendicesEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/appraisals/{appraisalId:guid}/appendices",
                async (Guid appraisalId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetAppraisalAppendicesQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetAppraisalAppendicesResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetAppraisalAppendices")
            .Produces<GetAppraisalAppendicesResponse>()
            .WithSummary("Get all appendices for an appraisal")
            .WithDescription("Returns all appendix entries with their documents, joined with appendix type names.")
            .WithTags("Appendix");
    }
}
