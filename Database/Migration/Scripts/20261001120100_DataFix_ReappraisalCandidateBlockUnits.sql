-- ============================================================
-- Data fix: block-project units among the COLLATREV candidates.
--
-- A COLLATREV number that names a block-project appraisal — with or without AS400's 'B', or a unit
-- ticket CAS issued (eight characters, 'U' at position 3) — is ONE UNIT of the project; each
-- collateral listing it is a different unit, reviewed on its own (ReappraisalCandidate.IsBlockUnit).
-- The ingestor resolves this for every row it writes; this applies it to rows written before:
--   1. a ticket is stored under the appraisal it was issued from (the project), so the unit keys the
--      same whichever way AS400 sends it; the ticket stays in SurveyNumber to find the unit;
--   2. IsBlockUnit = 1 for tickets and for numbers naming a block-project appraisal;
--   3. the stale-copy step of 20260928120000 again, now that ticket rows carry the project number: a
--      unit consumed under "B…" whose later files arrived as its ticket is the same unit, and its
--      Pending copy must not come back as to-do.
--
-- Idempotent: a second run changes nothing.
-- ============================================================

UPDATE c
SET c.[NormalizedSurveyNumber] = a.[AppraisalNumber],
    c.[IsBlockUnit] = 1
FROM [collateral].[ReappraisalCandidates] c
JOIN [appraisal].[UnitTickets] t ON t.[TicketNumber] = c.[NormalizedSurveyNumber]
JOIN [appraisal].[Appraisals] a ON a.[Id] = t.[AppraisalId] AND a.[IsDeleted] = 0
WHERE LEN(c.[NormalizedSurveyNumber]) = 8
  AND SUBSTRING(c.[NormalizedSurveyNumber], 3, 1) = 'U';

UPDATE c
SET c.[IsBlockUnit] = 1
FROM [collateral].[ReappraisalCandidates] c
WHERE c.[IsBlockUnit] = 0
  AND EXISTS (SELECT 1
              FROM [appraisal].[Appraisals] a
              JOIN [appraisal].[Projects] pj ON pj.[AppraisalId] = a.[Id]
              WHERE a.[AppraisalNumber] = c.[NormalizedSurveyNumber]
                AND a.[IsDeleted] = 0);

-- Same rule as 20260928120000's last statement, per collateral for a block-project unit.
UPDATE p
SET p.[Status] = 'Consumed'
FROM [collateral].[ReappraisalCandidates] p
WHERE p.[Status] IN ('Pending', 'Deleted')
  AND EXISTS (SELECT 1
              FROM [collateral].[ReappraisalCandidates] c
              WHERE c.[NormalizedSurveyNumber] = p.[NormalizedSurveyNumber]
                AND c.[Status] = 'Consumed'
                AND c.[Id] <> p.[Id]
                AND (p.[IsBlockUnit] = 0 OR c.[CollateralId] = p.[CollateralId]));
