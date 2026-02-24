CREATE
OR ALTER
VIEW appraisal.vw_AppraisalComparableList AS
SELECT ac.Id,
       ac.AppraisalId,
       ac.MarketComparableId,
       ac.SequenceNumber,
       ac.Weight,
       ac.OriginalPricePerUnit,
       ac.AdjustedPricePerUnit,
       ac.TotalAdjustmentPct,
       ac.WeightedValue,
       ac.SelectionReason,
       ac.Notes,
       mc.ComparableNumber,
       mc.PropertyType      AS ComparablePropertyType,
       mc.SurveyName        AS ComparableSurveyName,
       mc.InfoDateTime       AS ComparableInfoDateTime,
       mc.SourceInfo         AS ComparableSourceInfo
FROM appraisal.AppraisalComparables ac
         INNER JOIN appraisal.MarketComparables mc ON mc.Id = ac.MarketComparableId
WHERE mc.IsDeleted = 0
