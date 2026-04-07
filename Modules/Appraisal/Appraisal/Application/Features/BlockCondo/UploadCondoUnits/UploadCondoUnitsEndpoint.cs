namespace Appraisal.Application.Features.BlockCondo.UploadCondoUnits;

public class UploadCondoUnitsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/condo-units/upload",
                async (
                    Guid appraisalId,
                    IFormFile file,
                    Guid? documentId,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    // Validate file extension - only .xlsx allowed
                    var extension = Path.GetExtension(file.FileName);
                    if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
                    {
                        return Results.Problem(
                            title: "Invalid file type",
                            detail: "Only .xlsx files are allowed.",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    // Validate file size - max 5MB
                    const long maxFileSize = 5 * 1024 * 1024;
                    if (file.Length > maxFileSize)
                    {
                        return Results.Problem(
                            title: "File too large",
                            detail: "File size must not exceed 5 MB.",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    using var stream = file.OpenReadStream();

                    var command = new UploadCondoUnitsCommand(
                        appraisalId,
                        file.FileName,
                        documentId,
                        stream);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<UploadCondoUnitsResponse>();

                    return Results.Created(
                        $"/appraisals/{appraisalId}/condo-unit-uploads",
                        response);
                }
            )
            .WithName("UploadCondoUnits")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<UploadCondoUnitsResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Upload condo units from Excel")
            .WithDescription("Uploads an Excel file containing condo unit data and imports them into the appraisal.")
            .WithTags("Block Condo")
            .DisableAntiforgery();
    }
}
