using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparables.CreateMarketComparable;

/// <summary>
/// Command to create a new Market Comparable
/// </summary>
public record CreateMarketComparableCommand(
    string ComparableNumber,
    string PropertyType,
    string SurveyName,
    DateTime? InfoDateTime = null,
    string? SourceInfo = null,
    string? Notes = null,
    Guid? TemplateId = null
) : ICommand<CreateMarketComparableResult>, ITransactionalCommand<IAppraisalUnitOfWork>;