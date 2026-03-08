using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.AddFactorToTemplate;

public record AddFactorToTemplateCommand(
    Guid TemplateId,
    Guid FactorId,
    int DisplaySequence,
    bool IsMandatory = false,
    decimal? DefaultWeight = null,
    decimal? DefaultIntensity = null,
    bool IsCalculationFactor = false
) : ICommand<AddFactorToTemplateResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
