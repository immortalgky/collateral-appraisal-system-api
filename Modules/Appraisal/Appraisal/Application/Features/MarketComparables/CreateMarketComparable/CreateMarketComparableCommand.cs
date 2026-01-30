using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparables.CreateMarketComparable;

/// <summary>
/// Command to create a new Market Comparable
/// </summary>
public record CreateMarketComparableCommand(
    string ComparableNumber,
    string PropertyType,
    string Province,
    string DataSource,
    DateTime SurveyDate,
    string? District = null,
    string? SubDistrict = null,
    string? Address = null,
    decimal? Latitude = null,
    decimal? Longitude = null,
    string? TransactionType = null,
    DateTime? TransactionDate = null,
    decimal? TransactionPrice = null,
    decimal? PricePerUnit = null,
    string? UnitType = null,
    string? Description = null,
    string? Notes = null,
    Guid? TemplateId = null
) : ICommand<CreateMarketComparableResult>, ITransactionalCommand<IAppraisalUnitOfWork>;