using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.UpdateFactorInTemplate;

public record UpdateFactorInTemplateCommand(
    Guid TemplateId,
    Guid FactorId,
    bool IsMandatory = false,
    decimal? DefaultWeight = null,
    decimal? DefaultIntensity = null,
    bool IsCalculationFactor = false
) : ICommand<UpdateFactorInTemplateResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
