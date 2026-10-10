using Appraisal.Contracts.Appraisals;
using Integration.Application.Services;
using MediatR;
using Shared.CQRS;

namespace Integration.Application.Features.Appraisals.GetCarryForwardDocumentsByNumber;

/// <summary>
/// Same data as the FE carry-forward endpoint, addressed by appraisal number. Returns null when the
/// number is unknown; the 409 for a non-Completed appraisal comes from the shared query.
/// </summary>
public class GetCarryForwardDocumentsByNumberQueryHandler(
    IAppraisalLookupService appraisalLookup,
    ISender sender)
    : IQueryHandler<GetCarryForwardDocumentsByNumberQuery, CarryForwardDocumentsResult?>
{
    public async Task<CarryForwardDocumentsResult?> Handle(
        GetCarryForwardDocumentsByNumberQuery query,
        CancellationToken cancellationToken)
    {
        var number = Request.Domain.Requests.Request.NormalizeBookNumber(query.AppraisalNumber);
        if (number is null) return null;

        var appraisal = await appraisalLookup.ResolvePriorAppraisalByNumberAsync(number, cancellationToken);
        if (appraisal is null) return null;

        return await sender.Send(new GetCarryForwardDocumentsQuery(appraisal.Id), cancellationToken);
    }
}
