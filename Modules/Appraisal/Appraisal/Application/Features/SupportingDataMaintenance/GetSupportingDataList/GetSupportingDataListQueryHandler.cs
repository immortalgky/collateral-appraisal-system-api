namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDataList;

public class GetSupportingDataListQueryHandler(ISupportingDataRepository repo, ICurrentUserService currentUserService) : IQueryHandler<GetSupportingDataListQuery, GetSupportingDataListResult>
{
    public async Task<GetSupportingDataListResult> Handle(
    GetSupportingDataListQuery req, CancellationToken ct)
    {
        var page = new PaginationRequest(req.Page, req.PageSize);

        var paged = await repo.GetListAsync(page, req.Status, req.ImportDate, req.SupportingNumber, ct);

        var items = paged.Items.Select(s => new SupportingDataListItem(
            s.Id,
            s.SupportingNumber?.Value,
            s.Status,
            s.ImportChannel,
            s.ImportDate,
            s.SourceOfData,
            s.AppraisalCompany,
            s.Description,
            s.Remark));

        if (currentUserService.IsInRole("IntAppraisalChecker") || currentUserService.IsInRole("ExtAppraisalChecker"))
        {
            items = items.Where(s => s.Status != SupportingStatus.Draft && s.Status != SupportingStatus.RoutedBack);
        }

        return new GetSupportingDataListResult(
            items,
            currentUserService.IsInRole("IntAppraisalStaff") || currentUserService.IsInRole("ExtAppraisalStaff"),
            (int)paged.Count,
            page.PageNumber,
            page.PageSize
        );
    }
}