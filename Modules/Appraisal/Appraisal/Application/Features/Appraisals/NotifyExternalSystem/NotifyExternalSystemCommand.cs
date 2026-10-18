using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.NotifyExternalSystem;

/// <summary>
/// Tells the source system again that the result package of a Completed appraisal is ready to collect,
/// without regenerating anything. <see cref="Reason"/> is mandatory and stored on the audit row.
/// </summary>
public record NotifyExternalSystemCommand(Guid AppraisalId, string Reason)
    : ICommand<NotifyExternalSystemResult>, ITransactionalCommand<IAppraisalUnitOfWork>;

public record NotifyExternalSystemResult(string ExternalSystem);
