-- ============================================================
-- Data fix: PricingAnalysisMethods.UseSystemCalc backfill.
--
-- Item D (ระบบ/ป้อนเอง per method): the group-level PricingAnalysis.UseSystemCalc flag now has a
-- per-method mirror. A group that was already manual must stay manual on every one of its
-- methods rather than silently flipping to system just because the new column defaults to 1 —
-- so every method is set to its own group's current flag.
--
-- PricingAnalysis.UseSystemCalc is NOT NULL (default 1), so there is no null-flag case to handle.
--
-- Idempotent: only rows whose value disagrees with their group are touched, so a second run
-- touches nothing further.
-- ============================================================

UPDATE pam
SET pam.[UseSystemCalc] = pa.[UseSystemCalc]
FROM [appraisal].[PricingAnalysisMethods] pam
JOIN [appraisal].[PricingAnalysisApproaches] paa ON paa.[Id] = pam.[ApproachId]
JOIN [appraisal].[PricingAnalysis] pa ON pa.[Id] = paa.[PricingAnalysisId]
WHERE pam.[UseSystemCalc] <> pa.[UseSystemCalc];
