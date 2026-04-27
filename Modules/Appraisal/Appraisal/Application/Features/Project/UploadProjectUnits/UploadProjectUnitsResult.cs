namespace Appraisal.Application.Features.Project.UploadProjectUnits;

/// <summary>Result of uploading project units.</summary>
public record UploadProjectUnitsResult(Guid UploadId, int UnitCount);
