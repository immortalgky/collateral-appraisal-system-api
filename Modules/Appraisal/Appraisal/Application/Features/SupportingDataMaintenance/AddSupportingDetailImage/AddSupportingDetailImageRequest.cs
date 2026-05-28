namespace Appraisal.Application.Features.SupportingDataMaintenance.AddSupportingDetailImage;

/// <summary>
/// HTTP request body for adding an image to a supporting detail.
/// Accepts document metadata directly (no gallery — SupportingData is standalone).
/// </summary>
public record AddSupportingDetailImageRequest(
    Guid DocumentId,
    string StorageUrl,
    string? FileName = null,
    string? Title = null,
    string? Description = null
);
