namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDataList;

public class GetSupportingDataListQueryHandler(ISupportingDataRepository repo, ICurrentUserService currentUserService) : IQueryHandler<GetSupportingDataListQuery, GetSupportingDataListResult>
{
    public async Task<GetSupportingDataListResult> Handle(
    GetSupportingDataListQuery req, CancellationToken ct)
    {
        var hasRemovePermission = currentUserService.HasPermission("SUPPORTING_DATA_MAINT_REMOVE"); // check permission to remove supporting data
        var hasEditPermission = currentUserService.HasPermission("SUPPORTING_DATA_MAINT_EDIT"); // check permission to create new supporting data

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

        // If user doesn't have edit permission, mean they normally staff. so, they should not see the supporting data in Draft or RoutedBack status.
        if (!hasEditPermission)
        {
            items = items.Where(s => s.Status != SupportingStatus.Draft && s.Status != SupportingStatus.RoutedBack);
        }


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