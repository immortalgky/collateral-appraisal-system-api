using MediatR;

namespace Appraisal.Contracts.Appraisals;

/// <summary>
/// The construction-inspection round ("ตรวจงวดครั้งที่ N") stamped on the appraisal created from a request:
/// the newest non-cancelled appraisal of that request. Null when the request has no appraisal yet (it is
/// created asynchronously after submit) or the appraisal carries no stamped number.
/// </summary>
public record GetInspectionNumberByRequestQuery(Guid RequestId) : IRequest<int?>;
