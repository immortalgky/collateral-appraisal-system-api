using Appraisal.Domain.ComparativeAnalysis;
using Shared.CQRS;
using Shared.Exceptions;

namespace Appraisal.Application.Features.ComparativeAnalysisTemplates.ReorderComparativeAnalysisTemplateFactors;

public class ReorderComparativeAnalysisTemplateFactorsCommandHandler(
    IComparativeAnalysisTemplateRepository repository
) : ICommandHandler<ReorderComparativeAnalysisTemplateFactorsCommand, ReorderComparativeAnalysisTemplateFactorsResult>
{
    public async Task<ReorderComparativeAnalysisTemplateFactorsResult> Handle(
        ReorderComparativeAnalysisTemplateFactorsCommand command,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetByIdWithFactorsAsync(command.TemplateId, cancellationToken);

        if (template is null)
            throw new NotFoundException("ComparativeAnalysisTemplate", command.TemplateId);

        if (command.Factors is null)
            throw new BadRequestException("The 'factors' collection is required.");

        template.ReorderFactors(command.Factors.Select(f => (f.FactorId, f.DisplaySequence)));

        return new ReorderComparativeAnalysisTemplateFactorsResult(true);
    }
}
