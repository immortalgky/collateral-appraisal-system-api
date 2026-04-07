namespace Appraisal.Application.Features.BlockVillage.UploadVillageUnits;

public class UploadVillageUnitsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/village-units/upload",
                async (
                    Guid appraisalId,
                    IFormFile file,
                    Guid? documentId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var extension = Path.GetExtension(file.FileName);
                    if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
                        return Results.Problem(title: "Invalid file type", detail: "Only .xlsx files are allowed.", statusCode: StatusCodes.Status400BadRequest);

                    const long maxFileSize = 5 * 1024 * 1024;
                    if (file.Length > maxFileSize)
                        return Results.Problem(title: "File too large", detail: "File size must not exceed 5 MB.", statusCode: StatusCodes.Status400BadRequest);

                    using var stream = file.OpenReadStream();

                    var result = await sender.Send(
                        new UploadVillageUnitsCommand(appraisalId, file.FileName, documentId, stream),
                        cancellationToken);

                    return Results.Created($"/appraisals/{appraisalId}/village-unit-uploads", result.Adapt<UploadVillageUnitsResponse>());
                }
            )
            .WithName("UploadVillageUnits")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<UploadVillageUnitsResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Upload village units from Excel")
            .WithDescription("Uploads an Excel file containing village unit data.")
            .WithTags("Block Village")
            .DisableAntiforgery();
    }
}
