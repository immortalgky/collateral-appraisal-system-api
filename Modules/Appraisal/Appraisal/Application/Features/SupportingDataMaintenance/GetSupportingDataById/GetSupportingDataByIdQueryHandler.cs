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

        return new GetSupportingDataByIdResult(
            s.Id,
            currentUserService.IsInRole("IntAppraisalChecker") || currentUserService.IsInRole("ExtAppraisalChecker"),
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