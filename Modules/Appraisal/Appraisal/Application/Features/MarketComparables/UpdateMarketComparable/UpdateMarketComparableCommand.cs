namespace Appraisal.Application.Features.MarketComparables.UpdateMarketComparable;

public record UpdateMarketComparableCommand(
  Guid Id,
  string PropertyType,
  string SurveyName,
  DateTime? InfoDateTime = null,
  string? SourceInfo = null,
  string? Notes = null,
  Guid? TemplateId = null
):ICommand<UpdateMarketComparableResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
