using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableTemplates.SetTemplateFactorMandatory;

public record SetTemplateFactorMandatoryCommand(
    Guid TemplateId,
    Guid FactorId,
    bool IsMandatory
) : ICommand<SetTemplateFactorMandatoryResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
