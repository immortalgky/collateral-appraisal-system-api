using Appraisal.Domain.Appraisals;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetLawAndRegulations;

public class GetLawAndRegulationsQueryHandler(
    ILawAndRegulationRepository repository
) : IQueryHandler<GetLawAndRegulationsQuery, GetLawAndRegulationsResult>
{
    public async Task<GetLawAndRegulationsResult> Handle(
        GetLawAndRegulationsQuery query,
        CancellationToken cancellationToken)
    {
        var regulations = await repository.GetByAppraisalIdWithImagesAsync(
            query.AppraisalId, cancellationToken);

        var items = regulations.Select(r => new LawAndRegulationDto(
            r.Id,
            r.HeaderCode,
            r.Remark,
            r.Images.Select(i => new LawAndRegulationImageDto(
                i.Id,
                i.DocumentId,
                i.DisplaySequence,
                i.FileName,
                i.FilePath,
                i.Title,
                i.Description
            )).OrderBy(i => i.DisplaySequence).ToList()
        )).ToList();

        return new GetLawAndRegulationsResult(items);
    }
}
