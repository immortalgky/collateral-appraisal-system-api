namespace Appraisal.Application.Features.Project;

/// <summary>Upload checks shared by the project Excel endpoints (unit list and unit price list).</summary>
internal static class ProjectExcelUpload
{
    public const long MaxFileSize = 5 * 1024 * 1024;

    /// <summary>Null when the file is acceptable, else a 400 problem.</summary>
    public static IResult? Validate(IFormFile file)
    {
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return Results.Problem(
                title: "Invalid file type",
                detail: "Only .xlsx files are allowed.",
                statusCode: StatusCodes.Status400BadRequest);

        if (file.Length > MaxFileSize)
            return Results.Problem(
                title: "File too large",
                detail: "File size must not exceed 5 MB.",
                statusCode: StatusCodes.Status400BadRequest);

        return null;
    }

    /// <summary>Client file name without any path, capped to the stored column length.</summary>
    public static string SafeName(IFormFile file)
    {
        var name = Path.GetFileName(file.FileName);
        return name.Length > 500 ? name[..500] : name;
    }

    /// <summary>
    /// The whole file as bytes. Commands carry bytes, not a stream, so a handler that runs again
    /// (EF execution-strategy retry) parses from the start.
    /// </summary>
    public static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken ct)
    {
        using var buffer = new MemoryStream((int)file.Length);
        await file.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }
}
