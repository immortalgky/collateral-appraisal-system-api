namespace Appraisal.Application.Features.Project.UploadProjectUnits;

/// <summary>HTTP response after uploading project units.</summary>
public record UploadProjectUnitsResponse(Guid UploadId, int UnitCount);
