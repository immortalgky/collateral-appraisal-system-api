namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDataList;

public class GetSupportingDataListQueryHandler(ISupportingDataRepository repo, ICurrentUserService currentUserService) : IQueryHandler<GetSupportingDataListQuery, GetSupportingDataListResult>
{
    public async Task<GetSupportingDataListResult> Handle(
    GetSupportingDataListQuery req, CancellationToken ct)
    {

        var page = new PaginationRequest(req.Page, req.PageSize);

        var paged = await repo.GetListAsync(page, req.Status, req.DateFrom, req.DateTo, req.LastModifiedDateFrom, req.LastModifiedDateTo, req.SupportingNumber, ct);

        var items = paged.Items.Select(s => new SupportingDataListItem(
            s.Id,
            s.SupportingNumber?.Value,
            s.Status,
            s.ImportChannel,
            s.ImportDate,
            s.SourceOfData,
            s.AppraisalCompanyId,
            s.Description,
            s.Remark,
            s.CreatedAt,
            s.UpdatedAt,
            s.UpdatedBy
            ));

        var hasRemovePermission = currentUserService.HasPermission("SUPPORTING_DATA_MAINT_REMOVE"); // check permission to remove supporting data
        var hasEditPermission = currentUserService.HasPermission("SUPPORTING_DATA_MAINT_EDIT"); // check permission to create new supporting data

        return new GetSupportingDataListResult(
            items,
            hasRemovePermission,
            hasEditPermission,
            (int)paged.Count,
            page.PageNumber,
            page.PageSize
        );
    }
}