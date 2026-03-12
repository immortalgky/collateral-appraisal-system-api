CREATE
OR ALTER
VIEW appraisal.vw_MarketComparableList AS
SELECT mc.Id,
       mc.ComparableNumber,
       mc.PropertyType,
       mc.SurveyName,
       mc.InfoDateTime,
       mc.SourceInfo,
       mc.OfferPrice,
       mc.OfferPriceAdjustmentPercent,
       mc.OfferPriceAdjustmentAmount,
       mc.SalePrice,
       mc.SaleDate,
       mc.OfferPriceUnit,
       mc.SalePriceUnit,
       mc.Notes,
       mc.TemplateId,
       mc.CreatedAt
FROM appraisal.MarketComparables mc
WHERE mc.IsDeleted = 0