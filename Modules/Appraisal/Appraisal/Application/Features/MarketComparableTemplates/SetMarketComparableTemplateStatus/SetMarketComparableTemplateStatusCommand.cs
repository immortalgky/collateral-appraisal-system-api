using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableTemplates.SetMarketComparableTemplateStatus;

public record SetMarketComparableTemplateStatusCommand(
    Guid Id,
    bool IsActive
) : ICommand<SetMarketComparableTemplateStatusResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
