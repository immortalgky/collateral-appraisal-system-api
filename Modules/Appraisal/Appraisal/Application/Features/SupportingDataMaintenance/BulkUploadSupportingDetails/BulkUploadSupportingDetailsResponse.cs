namespace Appraisal.Application.Features.SupportingDataMaintenance.BulkUploadSupportingDetails;

/// <summary>
/// JSON response body returned to the caller on a successful upload.
/// </summary>
public record BulkUploadSupportingDetailsResponse(int InsertedCount);
