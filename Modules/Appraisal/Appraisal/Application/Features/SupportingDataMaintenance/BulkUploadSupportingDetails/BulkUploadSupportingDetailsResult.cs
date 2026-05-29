namespace Appraisal.Application.Features.SupportingDataMaintenance.BulkUploadSupportingDetails;

/// <summary>
/// Internal result returned by the command handler to the endpoint.
/// </summary>
public record BulkUploadSupportingDetailsResult(int InsertedCount);
