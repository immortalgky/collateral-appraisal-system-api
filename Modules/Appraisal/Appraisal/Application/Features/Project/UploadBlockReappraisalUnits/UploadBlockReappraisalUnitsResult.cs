namespace Appraisal.Application.Features.Project.UploadBlockReappraisalUnits;

/// <summary>
/// Outcome counts from the Excel re-match operation.
/// </summary>
/// <param name="MatchedUnsold">
///   Number of existing units whose business key appeared in the incoming Excel (confirmed still unsold).
/// </param>
/// <param name="AutoSold">
///   Number of existing units whose business key was absent from the Excel (auto-marked as sold).
/// </param>
/// <param name="Added">
///   Number of incoming rows with no matching existing unit (new units in the Excel not yet seeded).
///   In v1 these rows are counted but NOT persisted — use the standard UploadProjectUnits endpoint
///   followed by UploadBlockReappraisalUnits if you need to add new units.
/// </param>
public record UploadBlockReappraisalUnitsResult(int MatchedUnsold, int AutoSold, int Added);
