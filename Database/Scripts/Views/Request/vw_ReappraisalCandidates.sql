CREATE
OR ALTER
VIEW request.vw_ReappraisalCandidates AS
SELECT
    c.Id,
    c.SourceFileName,
    c.SourceFileDate,
    c.EffectiveDate,
    c.IngestedAt,
    c.Status,
    c.ReviewType,
    c.ReviewDate,
    -- AppraisalDate / RemainingDay / DaysSinceLastAppraisal are all derived from the matched
    -- in-system appraisal's AppointmentDateTime (see OUTER APPLY `last_appr` below):
    --   AppraisalDate            = AppointmentDateTime
    --   RemainingDay             = (AppointmentDateTime + 5 years) − today
    --   DaysSinceLastAppraisal   = today − AppointmentDateTime
    -- NULL when SurveyNumber doesn't resolve to any in-system appraisal.
    CAST(last_appr.AppointmentDateTime AS DATE)                                            AS AppraisalDate,
    DATEDIFF(DAY,
        CAST(GETDATE() AS DATE),
        DATEADD(YEAR, 5, CAST(last_appr.AppointmentDateTime AS DATE)))                     AS RemainingDay,
    DATEDIFF(DAY,
        CAST(last_appr.AppointmentDateTime AS DATE),
        CAST(GETDATE() AS DATE))                                                           AS DaysSinceLastAppraisal,
    c.CollateralId,
    c.SurveyNumber          AS OldAppraisalReportNumber,
    c.CollateralCode,
    c.CollateralCategory,
    c.CollateralName,
    c.CollateralAddress,
    c.CifNumber,
    c.CifName               AS CustomerName,
    c.AoCode,
    c.AoName,
    c.TitleNumber,
    c.CurrentValue,
    c.ValuationDate,
    c.InternalExternal,
    c.BusinessSize,
    c.BusinessSizeDesc,
    c.MortgageAmount,
    c.PastDueDay,
    c.ApplicationNumber,
    c.FacilityCode,
    c.FacilitySequence,
    c.CpNumber,
    c.CarCode,
    c.FacilityLimit,
    c.FlagLessAge4Y,
    c.FlagGreaterAge4Y,
    c.CountAgeingDate,
    c.CollateralDescription,
    c.ExternalValuerName,
    c.InternalValuerName,
    c.SllOver100M,
    c.SllDescription,
    -- Trailing extension fields (pos 641–660 in the input file).
    c.Stage,
    c.IBGRetail,
    c.[Group],
    c.EffectiveDateAppraisal,
    c.Latitude,
    c.Longitude,
    -- "In Progress" indicator, sourced PURELY from the Appraisal table — no dependency on
    -- request.Requests. TRUE when an in-flight (non-terminal, non-deleted) reappraisal Appraisal
    -- points back at this candidate's prior appraisal via PrevAppraisalId. The OpenAppraisal*
    -- columns carry that open appraisal's Id / AppraisalNumber (UI badge renders "→ <number>").
    CAST(CASE WHEN open_req.OpenAppraisalId IS NOT NULL THEN 1 ELSE 0 END AS BIT) AS HasOpenAppraisal,
    open_req.OpenAppraisalId       AS OpenAppraisalId,
    open_req.OpenAppraisalNumber   AS OpenAppraisalNumber,
    open_req.OpenAppraisalGroupTag AS OpenAppraisalGroupTag
FROM request.ReappraisalCandidates c
-- Any non-terminal `appraisal.Appraisals` row whose PrevAppraisalId points at the prior appraisal
-- (matched via SurveyNumber = AppraisalNumber) means a reappraisal cycle is in flight.
-- OUTER APPLY collapses multiple historical opens to the most recent.
OUTER APPLY (
    SELECT TOP 1
        openA.Id              AS OpenAppraisalId,
        openA.AppraisalNumber AS OpenAppraisalNumber,
        openA.GroupTag        AS OpenAppraisalGroupTag
    FROM appraisal.Appraisals prev
    JOIN appraisal.Appraisals openA ON openA.PrevAppraisalId = prev.Id
    WHERE prev.AppraisalNumber = c.SurveyNumber
      AND openA.Status NOT IN ('Completed', 'Cancelled')
      AND openA.IsDeleted = 0
    ORDER BY openA.CreatedAt DESC
) open_req
-- Last in-system appraisal date for this candidate (matched via SurveyNumber = AppraisalNumber).
-- Drives AppraisalDate / RemainingDay / DaysSinceLastAppraisal above. NULL when unmatched.
OUTER APPLY (
    SELECT TOP 1 al.AppointmentDateTime
    FROM appraisal.Appraisals a
    JOIN appraisal.vw_AppraisalList al ON al.Id = a.Id
    WHERE a.AppraisalNumber = c.SurveyNumber
    ORDER BY al.AppointmentDateTime DESC
) last_appr
WHERE c.Status <> 'Deleted'
