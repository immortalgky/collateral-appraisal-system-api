namespace Appraisal.Application.Features.PricingAnalysis.UpdateRemark;

public record UpdateRemarkCommand(Guid PricingAnalysisId, string Remark) : ICommand<UpdateRemarkResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
