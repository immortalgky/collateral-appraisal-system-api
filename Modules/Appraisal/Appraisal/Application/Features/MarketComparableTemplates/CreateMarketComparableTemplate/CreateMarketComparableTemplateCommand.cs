using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableTemplates.CreateMarketComparableTemplate;

public record CreateMarketComparableTemplateCommand(
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description
) : ICommand<CreateMarketComparableTemplateResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
