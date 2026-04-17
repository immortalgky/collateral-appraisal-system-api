using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalViews;

/// <summary>
/// Query to get smart view presets for the appraisals list.
/// Returns pre-built filter combinations that clients can apply directly to GET /appraisals.
/// </summary>
public record GetAppraisalViewsQuery() : IQuery<GetAppraisalViewsResult>;
