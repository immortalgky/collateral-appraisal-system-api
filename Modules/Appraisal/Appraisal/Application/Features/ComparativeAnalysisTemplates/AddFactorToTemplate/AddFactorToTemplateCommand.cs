using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.AddFactorToTemplate;

public record AddFactorToTemplateCommand(
    Guid TemplateId,
    Guid FactorId,
    int DisplaySequence,
    bool IsMandatory = false,
    decimal? DefaultWeight = null
) : ICommand<AddFactorToTemplateResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
