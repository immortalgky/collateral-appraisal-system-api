using System.Text.Json;
using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Appraisals.CorrectPropertyData;

/// <summary>
/// Admin correction of one property of a Completed appraisal, through the same write logic as the real
/// property page. <see cref="Suffix"/> is the real route suffix (land-detail, condo-detail, ...) and
/// <see cref="Data"/> is exactly the body that route's PUT accepts. <see cref="Reason"/> is mandatory and
/// is stored on the audit row, which is written in the same transaction as the change.
/// </summary>
public record CorrectPropertyDataCommand(
    Guid AppraisalId,
    Guid PropertyId,
    string Suffix,
    string Reason,
    JsonElement Data
) : ICommand<CorrectPropertyDataResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
