using Appraisal.Contracts.Appraisals;

using Request.Domain.Requests;

namespace Request.Application.Services;

/// <summary>
/// The inspection round of the appraisal built from a request, for the request page. Asked of the Appraisal
/// module only when it can matter: the request is submitted (the appraisal is created after submit) and its
/// purpose is a construction inspection (06 / 11, both Progressive).
/// </summary>
internal static class RequestInspectionNumber
{
    public static Task<int?> ResolveAsync(
        Domain.Requests.Request request, ISender sender, CancellationToken cancellationToken) =>
        RequestPurposes.IsConstructionInspection(request.Purpose) && request.HasBeenSubmitted()
            ? sender.Send(new GetInspectionNumberByRequestQuery(request.Id), cancellationToken)
            : Task.FromResult<int?>(null);
}
