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
       mc.SourceInfo         AS ComparableSourceInfo,
       mc.OfferPrice         AS ComparableOfferPrice,
       mc.OfferPriceAdjustmentPercent AS ComparableOfferPriceAdjustmentPercent,
       mc.OfferPriceAdjustmentAmount  AS ComparableOfferPriceAdjustmentAmount,
       mc.SalePrice          AS ComparableSalePrice,
       mc.SaleDate           AS ComparableSaleDate,
       mc.OfferPriceUnit     AS ComparableOfferPriceUnit,
       mc.SalePriceUnit      AS ComparableSalePriceUnit,
       -- For the Markets tab's rows and map: where the comparable is (distance to the subject is
       -- worked out client-side against the appraisal's own map pins), how big, what kind of plot,
       -- a photo, and which pricing methods of THIS appraisal already use it.
       mc.Latitude           AS ComparableLatitude,
       mc.Longitude          AS ComparableLongitude,
       fa.LandAreaSqWa       AS ComparableLandAreaSqWa,
       fp.PlotLocation       AS ComparablePlotLocation,
       img.DocumentId        AS ComparableThumbnailDocumentId,
       used.UsedInMethods
FROM appraisal.AppraisalComparables ac
         INNER JOIN appraisal.MarketComparables mc ON mc.Id = ac.MarketComparableId
    -- Factor 02 (totalLandAreaInSqWa) is stored as text in the EAV table.
    OUTER APPLY (SELECT TOP 1 TRY_CAST(d.Value AS DECIMAL(18, 2)) AS LandAreaSqWa
                 FROM appraisal.MarketComparableData d
                          INNER JOIN appraisal.MarketComparableFactors f ON f.Id = d.FactorId
                 WHERE d.MarketComparableId = mc.Id
                   AND f.FactorCode = '02') fa
    -- Factor 52 (plotLocationType) is a JSON array of PlotLocation codes, e.g. ["01","03"]; the
    -- client parses and labels it.
    OUTER APPLY (SELECT TOP 1 NULLIF(d.Value, '') AS PlotLocation
                 FROM appraisal.MarketComparableData d
                          INNER JOIN appraisal.MarketComparableFactors f ON f.Id = d.FactorId
                 WHERE d.MarketComparableId = mc.Id
                   AND f.FactorCode = '52') fp
    OUTER APPLY (SELECT TOP 1 g.DocumentId
                 FROM appraisal.MarketComparableImages i
                          INNER JOIN appraisal.AppraisalGallery g ON g.Id = i.GalleryPhotoId
                 WHERE i.MarketComparableId = mc.Id
                 ORDER BY i.DisplaySequence) img
    -- Pricing methods that link this comparable, counted only when the analysis belongs to this
    -- appraisal: a comparable can be linked to more than one appraisal (14 are, on dev), and
    -- another appraisal's WQS says nothing about this one. A reference analysis (machinery cost,
    -- income land, leasehold…) is owned by a method of a host analysis, so it resolves through
    -- the host; group, project-model and appraisal-property anchors resolve directly.
    OUTER APPLY (SELECT STRING_AGG(x.MethodType, ',') WITHIN GROUP (ORDER BY x.MethodType) AS UsedInMethods
                 FROM (SELECT DISTINCT m.MethodType
                       FROM appraisal.PricingComparableLinks l
                                INNER JOIN appraisal.PricingAnalysisMethods m ON m.Id = l.PricingMethodId
                                INNER JOIN appraisal.PricingAnalysisApproaches a ON a.Id = m.ApproachId
                                INNER JOIN appraisal.PricingAnalysis pa ON pa.Id = a.PricingAnalysisId
                                LEFT JOIN appraisal.PricingAnalysisMethods hm ON hm.Id = pa.HostMethodId
                                LEFT JOIN appraisal.PricingAnalysisApproaches ha ON ha.Id = hm.ApproachId
                                LEFT JOIN appraisal.PricingAnalysis hpa ON hpa.Id = ha.PricingAnalysisId
                                CROSS APPLY (SELECT COALESCE(hpa.SubjectType, pa.SubjectType) AS SubjectType,
                                                    COALESCE(hpa.AnchorId, pa.AnchorId)       AS AnchorId) o
                                LEFT JOIN appraisal.PropertyGroups pg ON o.SubjectType = 0 AND pg.Id = o.AnchorId
                                LEFT JOIN appraisal.ProjectModels pm ON o.SubjectType = 1 AND pm.Id = o.AnchorId
                                LEFT JOIN appraisal.Projects pr ON pr.Id = pm.ProjectId
                                LEFT JOIN appraisal.AppraisalProperties ap ON o.SubjectType = 2 AND ap.Id = o.AnchorId
                       WHERE l.MarketComparableId = mc.Id
                         AND COALESCE(pg.AppraisalId, pr.AppraisalId, ap.AppraisalId) = ac.AppraisalId) x) used
WHERE mc.IsDeleted = 0
