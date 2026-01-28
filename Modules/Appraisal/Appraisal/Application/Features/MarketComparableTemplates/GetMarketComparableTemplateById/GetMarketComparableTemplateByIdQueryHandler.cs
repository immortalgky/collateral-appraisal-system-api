using Appraisal.Domain.MarketComparables;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableTemplates.GetMarketComparableTemplateById;

public class GetMarketComparableTemplateByIdQueryHandler(
    IMarketComparableTemplateRepository repository
) : IQueryHandler<GetMarketComparableTemplateByIdQuery, GetMarketComparableTemplateByIdResult>
{
    public async Task<GetMarketComparableTemplateByIdResult> Handle(
        GetMarketComparableTemplateByIdQuery query,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetByIdWithFactorsAsync(query.Id, cancellationToken);

        if (template is null)
        {
            throw new InvalidOperationException($"Market comparable template with ID {query.Id} not found.");
        }

        var dto = new MarketComparableTemplateDetailDto(
            template.Id,
            template.TemplateCode,
            template.TemplateName,
            template.PropertyType,
            template.Description,
            template.IsActive,
            template.TemplateFactors.Select(tf => new TemplateFactorDto(
                tf.Id,
                tf.FactorId,
                tf.Factor?.FactorCode ?? string.Empty,
                tf.Factor?.FactorName ?? string.Empty,
                tf.Factor?.FieldName ?? string.Empty,
                tf.Factor?.DataType.ToString() ?? string.Empty,
                tf.Factor?.FieldLength,
                tf.Factor?.FieldDecimal,
                tf.Factor?.ParameterGroup,
                tf.DisplaySequence,
                tf.IsMandatory,
                tf.Factor?.IsActive ?? false
            )).OrderBy(f => f.DisplaySequence).ToList(),
            template.CreatedOn,
            template.UpdatedOn
        );

        return new GetMarketComparableTemplateByIdResult(dto);
    }
}
