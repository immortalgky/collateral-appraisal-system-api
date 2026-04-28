namespace Appraisal.Application.Features.Project.UploadProjectUnits;

public class UploadProjectUnitsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/appraisals/{appraisalId:guid}/project/units/upload",
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
                    {
                        return Results.Problem(
                            title: "Invalid file type",
                            detail: "Only .xlsx files are allowed.",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    const long maxFileSize = 5 * 1024 * 1024;
                    if (file.Length > maxFileSize)
                    {
                        return Results.Problem(
                            title: "File too large",
                            detail: "File size must not exceed 5 MB.",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    using var stream = file.OpenReadStream();

                    var command = new UploadProjectUnitsCommand(
                        appraisalId, file.FileName, documentId, stream);

                    var result = await sender.Send(command, cancellationToken);

                    var response = result.Adapt<UploadProjectUnitsResponse>();

                    return Results.Created($"/appraisals/{appraisalId}/project/units/uploads", response);
                }
            )
            .WithName("UploadProjectUnits")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<UploadProjectUnitsResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Upload project units from Excel")
            .WithDescription("Uploads an Excel file containing project unit data. Column layout differs by ProjectType (Condo vs LandAndBuilding).")
            .WithTags("Project")
            .DisableAntiforgery();
    }
}
