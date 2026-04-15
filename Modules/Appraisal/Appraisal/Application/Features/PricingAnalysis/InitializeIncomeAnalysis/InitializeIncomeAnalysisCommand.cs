using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.InitializeIncomeAnalysis;

public record InitializeIncomeAnalysisCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
    string TemplateCode
) : ICommand<InitializeIncomeAnalysisResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
