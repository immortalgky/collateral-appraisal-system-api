using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparables.CreateMarketComparable;

/// <summary>
/// Command to create a new Market Comparable
/// </summary>
public record CreateMarketComparableCommand(
    string PropertyType,
    string SurveyName,
    DateTime? InfoDateTime = null,
    string? SourceInfo = null,
    string? Notes = null,
    Guid? TemplateId = null,
    decimal? OfferPrice = null,
    decimal? OfferPriceAdjustmentPercent = null,
    decimal? OfferPriceAdjustmentAmount = null,
    decimal? SalePrice = null,
    DateTime? SaleDate = null,
    string? OfferPriceUnit = null,
    string? SalePriceUnit = null
) : ICommand<CreateMarketComparableResult>, ITransactionalCommand<IAppraisalUnitOfWork>;