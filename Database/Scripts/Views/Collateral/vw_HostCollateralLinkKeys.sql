-- The COLLATLINK feed with the keys every reader has to derive from it.
--
-- Two outbound interfaces read collateral.HostCollateralLinks — the regulatory snapshot
-- (vw_RegulatoryExport) and the AS400 result file (vw_CollateralResultExport) — and both have to
-- turn AS400's fields into the same three things before they can join to anything: the appraisal
-- number as CAS spells it, and the two tokens that name a block-project unit. Deriving that twice
-- meant the two views could disagree about which unit a collateral is, silently and in only one of
-- the two files. It lives here once instead.
--
-- No filtering happens here. Each reader has its own row set — the regulatory file reports only
-- master-title collateral, the result file does not care about the flag — so the columns are
-- exposed raw and the WHERE stays with the consumer. IsActive is the one exception: it is the same
-- rule for everyone and is the easiest thing in the world to forget.
CREATE OR ALTER VIEW collateral.vw_HostCollateralLinkKeys
AS
SELECT
    h.HostCollateralId,
    h.AppraisalNumber,
    h.CollateralName,
    h.Address1,
    h.MasterTitle,
    h.IsRedeemed,
    h.LastSeenFileDate,
    h.PropertyType,
    h.PropertyTypeDesc,

    -- AS400 prefixes a block project's appraisal number with 'B'; CAS stores it without. All 107
    -- prefixed numbers on the 2026-08-03 feed match a CAS appraisal once the letter is removed — no
    -- exceptions — and the bank's own file carries them unprefixed too (not one of its 63,095 rows
    -- starts with 'B'). Not every block is prefixed, so this normalises rather than detects.
    --
    -- Normalised here rather than in each join predicate: `OR h.AppraisalNumber = 'B' + a.Number`
    -- cannot use an index.
    CASE WHEN LEFT(h.AppraisalNumber, 1) = 'B'
         THEN SUBSTRING(h.AppraisalNumber, 2, LEN(h.AppraisalNumber))
         ELSE h.AppraisalNumber
    END AS CasAppraisalNumber,

    -- ── Two keys for one unit ──────────────────────────────────────────────────────────────────
    -- A block-project collateral is one unit of a development, and an export has to find that unit
    -- in appraisal.ProjectUnits to price it. AS400 states the unit twice, in two fields that fail on
    -- different rows, so both are carried and either may make the match.
    --
    -- AddrToken — the leading word of Address1, the field AS400 added on 2026-08-26. Addresses open
    -- with the house or room number ("129/517 โครงการเพอร์เฟคเพลส"), and for a HOUSE in a
    -- development this is the ONLY workable key: CollateralName there is a deed number
    -- ("ฉ.26892 ร.5036 II 5018-5") and no deed number appears anywhere in the unit table. It found
    -- the unit for all 55 such collateral that were reporting zero.
    LTRIM(RTRIM(
        CASE WHEN CHARINDEX(' ', LTRIM(h.Address1)) > 0
             THEN LEFT(LTRIM(h.Address1), CHARINDEX(' ', LTRIM(h.Address1)) - 1)
             ELSE LTRIM(h.Address1)
        END)) AS AddrToken,

    -- NameToken — the key out of "CONDO.<key> <deeds>". Still needed: 19 collateral have an Address1
    -- that opens with a word rather than a number ("ติด…", "ภายในอาคาร…", "โครงการ…") and would lose
    -- the unit they match today.
    --
    -- Read leniently. AS400 writes the prefix four ways — "CONDO.47/18", "CONDO. 59/38",
    -- "CONDO 159/262", "CONDO138/133" — and the strict 'CONDO.%' + pos-7 read produced an EMPTY key
    -- for the 52 rows that are not the first spelling, which then matched nothing. Dropping a leading
    -- '.', '/' and any spaces covers all four, and leaves digits alone so "CONDO138/133" yields
    -- "138/133".
    CASE WHEN h.CollateralName LIKE 'CONDO%' THEN
        CASE WHEN CHARINDEX(' ', v.Rest) > 0
             THEN LEFT(v.Rest, CHARINDEX(' ', v.Rest) - 1)
             ELSE v.Rest
        END
    END AS NameToken,

    -- Only collateral the newest COLLATLINK file still lists. The feed is a full monthly replace, so
    -- a row left on an older LastSeenFileDate is collateral AS400 has stopped reporting. Those rows
    -- are kept rather than deleted — a truncated file would otherwise be unrecoverable — so being
    -- present in the table is not the same as being held, and every reader has to say which it means.
    CAST(CASE WHEN h.LastSeenFileDate =
                   (SELECT MAX(l.LastSeenFileDate) FROM collateral.HostCollateralLinks l)
              THEN 1 ELSE 0 END AS bit) AS IsActive

FROM collateral.HostCollateralLinks h
-- Everything after the literal 'CONDO', with a leading '.' or '/' and any spaces removed.
CROSS APPLY (SELECT LTRIM(
    CASE WHEN SUBSTRING(h.CollateralName, 6, 1) IN ('.', '/')
         THEN SUBSTRING(h.CollateralName, 7, 200)
         ELSE SUBSTRING(h.CollateralName, 6, 200)
    END) AS Rest) v;
