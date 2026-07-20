using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalDocuments;

public class GetAppraisalDocumentsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/appraisals/{appraisalId:guid}/documents",
                async (Guid appraisalId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var query = new GetAppraisalDocumentsQuery(appraisalId);
                    var result = await sender.Send(query, cancellationToken);
                    var response = result.Adapt<GetAppraisalDocumentsResponse>();
                    return Results.Ok(response);
                })
            .WithName("GetAppraisalDocuments")
            .Produces<GetAppraisalDocumentsResponse>()
            .WithSummary("Get the valuation document checklist for an appraisal")
            .WithDescription("Returns every VAL_DOC document type (parameter.DocumentTypes) with its attached files for the given appraisal.")
            .WithTags("Appraisal Documents");
    }
}
