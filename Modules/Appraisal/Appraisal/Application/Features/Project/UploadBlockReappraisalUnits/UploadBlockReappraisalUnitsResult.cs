namespace Appraisal.Application.Features.Project.UploadBlockReappraisalUnits;

/// <summary>
/// Outcome counts from the Excel re-match operation.
/// </summary>
/// <param name="MatchedUnsold">
///   Existing units whose business key appeared in the incoming Excel (confirmed still unsold).
/// </param>
/// <param name="AutoSold">
///   Existing units whose business key was absent from the Excel (auto-marked as sold).
/// </param>
/// <param name="Added">Incoming rows with no matching existing unit, appended to the project.</param>
/// <param name="Updated">Existing units whose attributes were refreshed from the Excel.</param>
public record UploadBlockReappraisalUnitsResult(
    int MatchedUnsold,
    int AutoSold,
    int Added,
    int Updated);
