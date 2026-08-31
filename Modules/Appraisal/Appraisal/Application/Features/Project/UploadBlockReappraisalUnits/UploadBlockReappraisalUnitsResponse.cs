namespace Appraisal.Application.Features.Project.UploadBlockReappraisalUnits;

/// <summary>HTTP response for the block reappraisal units re-match operation.</summary>
public record UploadBlockReappraisalUnitsResponse(int MatchedUnsold, int AutoSold, int Added);
