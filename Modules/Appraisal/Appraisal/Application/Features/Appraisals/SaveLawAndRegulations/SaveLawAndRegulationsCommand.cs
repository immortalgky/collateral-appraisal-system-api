using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.SaveLawAndRegulations;

public record SaveLawAndRegulationsCommand(
    Guid AppraisalId,
    IReadOnlyList<LawAndRegulationItemInput> Items
) : ICommand<SaveLawAndRegulationsResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
