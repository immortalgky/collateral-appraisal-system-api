namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDataList;

public class GetSupportingDataListQueryHandler(ISupportingDataRepository repo) : IQueryHandler<GetSupportingDataListQuery, GetSupportingDataListResult>
{
    public async Task<GetSupportingDataListResult> Handle(
    GetSupportingDataListQuery req, CancellationToken ct)
    {
        var page = new PaginationRequest(req.Page, req.PageSize);

        var paged = await repo.GetListAsync(page, req.Status, req.ImportDate, req.SupportingNumber, ct);

        var items = paged.Items.Select(s => new SupportingDataListItem(
            s.Id,
            s.SupportingNumber?.Value,
            s.ImportChannel,
            s.ImportDate,
            s.SourceOfData,
            s.AppraisalCompany,
            s.Description,
            s.Remark));

        return new GetSupportingDataListResult(items, (int)paged.Count);
    }
}