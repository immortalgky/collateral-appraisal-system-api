-- The project unit a block-project COLLATREV candidate (IsBlockUnit = 1) is about. One row per such
-- candidate; ProjectUnitId is NULL unless exactly one unit matched.
--
-- How the unit is found, strongest first — the same keys and spellings as the regulatory export reads
-- out of COLLATLINK (collateral.vw_HostCollateralLinkKeys; keep the two readings identical):
--   Ticket            — the number AS400 sent is a unit ticket CAS issued: the units it covers, outright.
--   CollateralName    — the key out of "CONDO.<key> <deeds>" (four spellings of the prefix).
--   CollateralAddress — the leading word of the address: a house or room number.
-- A parsed key is compared with appraisal.vw_ProjectUnitKeys ranks 0–2 (registration, room, house
-- number) of the candidate's project; never rank 3 (plot number), which is short enough to collide with
-- numbers read out of free text. Within the strongest source and rank that matched anything, more than
-- one distinct unit is ambiguous: no unit, MatchedUnits says how many.
CREATE OR ALTER VIEW collateral.vw_ReappraisalCandidateUnits
AS
WITH cand AS (
    SELECT
        c.Id                                    AS CandidateId,
        UPPER(LTRIM(RTRIM(c.SurveyNumber)))     AS RawNumber,
        c.CollateralName,
        c.CollateralAddress,
        pj.ProjectId,
        pj.AppraisalId                          AS ProjectAppraisalId
    FROM collateral.ReappraisalCandidates c
    CROSS APPLY (
        SELECT TOP 1 p.Id AS ProjectId, a.Id AS AppraisalId
        FROM appraisal.Appraisals a
        JOIN appraisal.Projects p ON p.AppraisalId = a.Id
        WHERE a.AppraisalNumber = c.NormalizedSurveyNumber
          AND a.IsDeleted = 0
        ORDER BY a.Id
    ) pj
    WHERE c.IsBlockUnit = 1
),
hits AS (
    SELECT k.CandidateId, -1 AS Source, 0 AS KeyRank, tu.ProjectUnitId
    FROM cand k
    JOIN appraisal.UnitTickets t ON t.TicketNumber = k.RawNumber AND t.AppraisalId = k.ProjectAppraisalId
    JOIN appraisal.UnitTicketUnits tu ON tu.UnitTicketId = t.Id

    UNION ALL

    SELECT k.CandidateId, s.Source, pk.KeyRank, pk.ProjectUnitId
    FROM cand k
    -- Everything after the literal 'CONDO', with a leading '.' or '/' and any spaces removed.
    CROSS APPLY (SELECT LTRIM(
        CASE WHEN SUBSTRING(k.CollateralName, 6, 1) IN ('.', '/')
             THEN SUBSTRING(k.CollateralName, 7, 200)
             ELSE SUBSTRING(k.CollateralName, 6, 200)
        END) AS Rest) v
    CROSS APPLY (VALUES
        (0, CASE WHEN k.CollateralName LIKE 'CONDO%' THEN
                CASE WHEN CHARINDEX(' ', v.Rest) > 0 THEN LEFT(v.Rest, CHARINDEX(' ', v.Rest) - 1) ELSE v.Rest END
            END),
        (1, LTRIM(RTRIM(
                CASE WHEN CHARINDEX(' ', LTRIM(k.CollateralAddress)) > 0
                     THEN LEFT(LTRIM(k.CollateralAddress), CHARINDEX(' ', LTRIM(k.CollateralAddress)) - 1)
                     ELSE LTRIM(k.CollateralAddress)
                END)))
    ) AS s(Source, Token)
    CROSS APPLY STRING_SPLIT(ISNULL(s.Token, ''), ',') part
    JOIN appraisal.vw_ProjectUnitKeys pk
      ON pk.ProjectId = k.ProjectId
     AND pk.UnitKey = LTRIM(RTRIM(part.value))
     AND pk.KeyRank < 3
    WHERE LTRIM(RTRIM(part.value)) <> ''
),
best AS (
    SELECT h.CandidateId, h.Source, h.ProjectUnitId,
           DENSE_RANK() OVER (PARTITION BY h.CandidateId ORDER BY h.Source, h.KeyRank) AS Tier
    FROM hits h
)
SELECT
    k.CandidateId,
    k.ProjectId,
    k.ProjectAppraisalId,
    CASE WHEN m.Units = 1 THEN one.ProjectUnitId END AS ProjectUnitId,
    ISNULL(m.Units, 0)                               AS MatchedUnits,
    CASE m.Source WHEN -1 THEN 'Ticket' WHEN 0 THEN 'CollateralName' WHEN 1 THEN 'CollateralAddress' END AS MatchedBy
FROM cand k
OUTER APPLY (
    SELECT COUNT(DISTINCT b.ProjectUnitId) AS Units, MIN(b.Source) AS Source
    FROM best b
    WHERE b.CandidateId = k.CandidateId AND b.Tier = 1
) m
OUTER APPLY (
    SELECT TOP 1 b.ProjectUnitId
    FROM best b
    WHERE b.CandidateId = k.CandidateId AND b.Tier = 1
) one;
