using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalById;

/// <summary>
/// Query to get an Appraisal by ID. <paramref name="Include"/> opts in to the request data and/or the request's
/// files; with none, the response is the header alone and no extra query runs.
/// <paramref name="StrictRelease"/>: also for the plain header, withhold the appraised value until the appraisal is
/// released from anyone who does not hold APPRAISAL_VIEW (set by the Integration route, whose client token holds no
/// appraisal permission; the Appraisal route keeps masking only the credit-side audience, as the list does).
/// </summary>
public record GetAppraisalByIdQuery(Guid Id, AppraisalInclude Include = AppraisalInclude.None, bool StrictRelease = false)
    : IQuery<GetAppraisalByIdResult>;