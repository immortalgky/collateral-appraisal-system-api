namespace Appraisal.Application.Features.SupportingDataMaintenance.BulkUploadSupportingDetails;

public class BulkUploadSupportingDetailsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/supporting-data/{supportingId:guid}/details/bulk-upload",
                async (
                    Guid supportingId,
                    IFormFile file,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    // File-level guards 
                    var extension = Path.GetExtension(file.FileName);
                    if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
                    {
                        return Results.Problem(
                            title: "Invalid file type",
                            detail: "Only .xlsx files are allowed.",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    const long maxFileSize = 5 * 1024 * 1024; // 5 MB
                    if (file.Length > maxFileSize)
                    {
                        return Results.Problem(
                            title: "File too large",
                            detail: "File size must not exceed 5 MB.",
                            statusCode: StatusCodes.Status400BadRequest);
                    }

                    // Dispatch command 
                    using var stream = file.OpenReadStream();
                    var command = new BulkUploadSupportingDetailsCommand(supportingId, stream);
                    var result = await sender.Send(command, cancellationToken);

                    var response = new BulkUploadSupportingDetailsResponse(result.InsertedCount);

                    return Results.Created(
                        $"/supporting-data/{supportingId}/details",
                        response);
                }
            )
            .WithName("BulkUploadSupportingDetails")
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<BulkUploadSupportingDetailsResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Bulk upload supporting details from Excel")
            .WithDescription(
                "Parses an Excel file (.xlsx, max 5 MB) and inserts all rows as supporting details. " +
                "All-or-nothing: if any row fails validation the entire upload is rejected and a 400 " +
                "is returned with a 'rowErrors' array in the ProblemDetails extensions.")
            .WithTags("SupportingDataMaintenance")
            .DisableAntiforgery();
    }
}
