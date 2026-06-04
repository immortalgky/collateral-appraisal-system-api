namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDataById;

public class GetSupportingDataByIdQueryHandler(ISupportingDataRepository repo, ICurrentUserService currentUserService)
    : IQueryHandler<GetSupportingDataByIdQuery, GetSupportingDataByIdResult>
{
    private static readonly HashSet<string> CannotEditStatuses =
        new(StringComparer.Ordinal) { "Approved", "Rejected", "Cancelled" };

    public async Task<GetSupportingDataByIdResult> Handle(
        GetSupportingDataByIdQuery query,
        CancellationToken cancellationToken)
    {

        var s = await repo.GetByIdAsync(query.SupportingId, cancellationToken)
            ?? throw new SupportingDataNotFoundException(query.SupportingId);

        var hasDecisionPermission = currentUserService.HasPermission("SUPPORTING_DATA_MAINT_DECISION") && !CannotEditStatuses.Contains(s.Status.Code); // check permission to make decision on supporting data
        var hasEditPermission = currentUserService.HasPermission("SUPPORTING_DATA_MAINT_EDIT") && !CannotEditStatuses.Contains(s.Status.Code); // check permission to create new supporting data

        return new GetSupportingDataByIdResult(
            s.Id,
            s.SupportingNumber?.Value,
            hasEditPermission,
            hasDecisionPermission,
            s.Status,
            s.ImportChannel,
            s.ImportDate,
            s.SourceOfData,
            s.AppraisalCompanyId,
            s.Description,
            s.Remark
        );
    }
}