using Carter;
using Mapster;
using MediatR;

namespace Appraisal.Application.Features.Appraisals.AddAppraisalDocument;

public class AddAppraisalDocumentEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/appraisals/{appraisalId:guid}/documents",
                async (Guid appraisalId, AddAppraisalDocumentRequest request,
                    ISender sender, CancellationToken cancellationToken) =>
                {
                    var command = new AddAppraisalDocumentCommand(
                        appraisalId,
                        request.DocumentTypeCode,
                        request.DocumentId,
                        request.FileName,
                        request.MimeType,
                        request.FileSizeBytes,
                        request.Notes,
                        request.SortOrder,
                        request.UploadedByName);

                    var result = await sender.Send(command, cancellationToken);
                    var response = result.Adapt<AddAppraisalDocumentResponse>();
                    return Results.Created(
                        $"/appraisals/{appraisalId}/documents/{response.Id}",
                        response);
                })
            .WithName("AddAppraisalDocument")
            .Produces<AddAppraisalDocumentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Attach a valuation document")
            .WithDescription("Links an already-uploaded document (image or PDF, via POST /documents) to a VAL_DOC document type checklist entry for the appraisal.")
            .WithTags("Appraisal Documents");
    }
}
