using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.PricingAnalysis.UploadHypothesisUnitDetails;

public record UploadHypothesisUnitDetailsCommand(
    Guid PricingAnalysisId,
    Guid MethodId,
    string FileName,
    Stream FileStream
) : ICommand<UploadHypothesisUnitDetailsResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
