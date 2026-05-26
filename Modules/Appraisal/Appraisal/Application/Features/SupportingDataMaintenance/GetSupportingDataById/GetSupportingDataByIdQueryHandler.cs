namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDataById;

public class GetSupportingDataByIdQueryHandler(AppraisalDbContext db)
    : IQueryHandler<GetSupportingDataByIdQuery, GetSupportingDataByIdResult>
{
    public async Task<GetSupportingDataByIdResult> Handle(
        GetSupportingDataByIdQuery query,
        CancellationToken cancellationToken)
    {
        var s = await db.SupportingData.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == query.SupportingId, cancellationToken)
            ?? throw new SupportingDataNotFoundException(query.SupportingId);

        return new GetSupportingDataByIdResult(
            s.Id,
            s.SupportingNumber?.Value,
            s.Status,
            s.ImportChannel,
            s.ImportDate,
            s.SourceOfData,
            s.AppraisalCompany,
            s.Description,
            s.Remark
        );
    }
}