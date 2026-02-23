using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableTemplates.DeleteMarketComparableTemplate;

public record DeleteMarketComparableTemplateCommand(Guid Id)
    : ICommand<DeleteMarketComparableTemplateResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
