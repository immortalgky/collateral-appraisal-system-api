using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.DeleteHypothesisUnitDetailUpload;

public record DeleteHypothesisUnitDetailUploadCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
    Guid UploadId
) : ICommand, ITransactionalCommand<IAppraisalUnitOfWork>;
