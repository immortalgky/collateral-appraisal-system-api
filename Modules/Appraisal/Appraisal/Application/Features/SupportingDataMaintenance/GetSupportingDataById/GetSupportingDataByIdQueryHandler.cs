namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDataById;

public class GetSupportingDataByIdQueryHandler(ISupportingDataRepository repo, ICurrentUserService currentUserService)
    : IQueryHandler<GetSupportingDataByIdQuery, GetSupportingDataByIdResult>
{
    public async Task<GetSupportingDataByIdResult> Handle(
        GetSupportingDataByIdQuery query,
        CancellationToken cancellationToken)
    {

        var s = await repo.GetByIdAsync(query.SupportingId, cancellationToken)
            ?? throw new SupportingDataNotFoundException(query.SupportingId);

        var isArchived = s.Status == SupportingStatus.Approved
                    || s.Status == SupportingStatus.Rejected
                    || s.Status == SupportingStatus.Cancelled;

        var hasDecisionPermission = currentUserService.HasPermission("SUPPORTING_DATA_MAINT_DECISION") && !isArchived; // check permission to make decision on supporting data
        var hasEditPermission = currentUserService.HasPermission("SUPPORTING_DATA_MAINT_EDIT") && !isArchived; // check permission to create new supporting data

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