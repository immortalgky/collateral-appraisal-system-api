CREATE
OR ALTER
VIEW appraisal.vw_MarketComparableList AS
SELECT mc.Id,
       mc.ComparableNumber,
       mc.PropertyType,
       mc.Province,
       mc.District,
       mc.SubDistrict,
       mc.[Address],
       mc.TransactionType,
       mc.TransactionDate,
       mc.TransactionPrice,
       mc.PricePerUnit,
       mc.UnitType,
       mc.DataSource,
       mc.DataConfidence,
       mc.IsVerified,
       mc.Status,
       mc.SurveyDate,
       mc.CreatedAt
FROM appraisal.MarketComparables mc
WHERE mc.IsDeleted = 0
