using Appraisal.Domain.MarketComparables;
using Shared.CQRS;
using Shared.Exceptions;

namespace Appraisal.Application.Features.MarketComparableTemplates.ReorderTemplateFactors;

public class ReorderTemplateFactorsCommandHandler(
    IMarketComparableTemplateRepository repository
) : ICommandHandler<ReorderTemplateFactorsCommand, ReorderTemplateFactorsResult>
{
    public async Task<ReorderTemplateFactorsResult> Handle(
        ReorderTemplateFactorsCommand command,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetByIdWithFactorsAsync(command.TemplateId, cancellationToken);

        if (template is null)
            throw new NotFoundException("MarketComparableTemplate", command.TemplateId);

        template.ReorderFactors(command.Factors.Select(f => (f.FactorId, f.DisplaySequence)));

        return new ReorderTemplateFactorsResult(true);
    }
}
