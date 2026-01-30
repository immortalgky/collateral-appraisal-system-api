using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableTemplates.AddFactorToTemplate;

public record AddFactorToTemplateCommand(
    Guid TemplateId,
    Guid FactorId,
    int DisplaySequence,
    bool IsMandatory
) : ICommand<AddFactorToTemplateResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
