CREATE
OR ALTER
VIEW appraisal.vw_MarketComparableList AS
SELECT mc.Id,
       mc.ComparableNumber,
       mc.PropertyType,
       mc.SurveyName,
       mc.InfoDateTime,
       mc.Notes,
       mc.TemplateId,
       mc.CreatedAt
FROM appraisal.MarketComparables mc
WHERE mc.IsDeleted = 0