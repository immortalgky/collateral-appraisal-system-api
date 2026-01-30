using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableTemplates.UpdateMarketComparableTemplate;

public record UpdateMarketComparableTemplateCommand(
    Guid Id,
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description
) : ICommand<UpdateMarketComparableTemplateResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
