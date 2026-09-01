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
    -- A ticket resolves to the appraisal it was issued from, so every reader downstream keeps
    -- working off an appraisal number exactly as it does for a collateral AS400 named the old way.
    --
    -- ⚠ ta is a plain LEFT JOIN, deliberately, not the OUTER APPLY below. This column is the key
    -- vw_RegulatoryExport and vw_CollateralResultExport join to appraisal.Appraisals on, and a
    -- COALESCE over a correlated APPLY is opaque to the optimiser: it stops seeking
    -- IX_Appraisals_AppraisalNumber and scans all ~59k appraisals once PER LINK ROW. On the U3 set
    -- that is ~1.9 billion rows, and it took the regulatory export from under a second to over ten
    -- minutes. Whatever feeds this column has to stay resolvable to a seek — the same reason the 'B'
    -- prefix is stripped here rather than in each reader's join predicate.
    -- ⚠ NO ticket resolution in this column, deliberately. Both outbound views join
    -- appraisal.Appraisals on it, and a COALESCE spanning two tables cannot be resolved before the
    -- ticket join completes: the optimiser abandons the hash join it used before ticketing existed
    -- and replays a full scan of all ~59k appraisals per link row. Measured on U3 with every column
    -- read, that is the difference between 23 seconds and never finishing inside the job's 600s
    -- command timeout. Readers resolve a ticket through TicketAppraisalId below instead, as a
    -- separate seekable branch.
    CASE WHEN LEFT(h.AppraisalNumber, 1) = 'B'
         THEN SUBSTRING(h.AppraisalNumber, 2, LEN(h.AppraisalNumber))
         ELSE h.AppraisalNumber
    END AS CasAppraisalNumber,

    -- The appraisal a ticket was issued from, when the feed named one. NULL for every collateral
    -- from before ticketing, which is what makes the two branches a clean split rather than an OR.
    tj.AppraisalId AS TicketAppraisalId,
    ta.AppraisalNumber AS TicketAppraisalNumber,

    -- ── The unit ticket, when the feed named one ───────────────────────────────────────────────
    -- CAS issues a ticket when LOS pulls a block unit's result, LOS carries it into AS400, and AS400
    -- writes it back here in the field that otherwise holds an appraisal number. Eight characters,
    -- digits either side of a literal 'U' at position 3 — appraisal numbers are all digits, so the
    -- marker tells them apart. Length cannot: legacy appraisal numbers such as "2560100004" are ten
    -- characters too.
    --
    -- This is the key the two systems agreed on, so it outranks both parsed tokens below. Those are
    -- read out of AS400 free text; this one we issued ourselves and it names the units outright.
    -- A collateral from before ticketing resolves to nothing here and falls through to exactly the
    -- behaviour it has today.
    tk.TicketToken,

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
-- ── The ticket, and the appraisal it was issued from ───────────────────────────────────────────
-- Ordinary joins, and they must stay ordinary: CasAppraisalNumber above is a join key, and folding
-- this lookup into the APPLY below is what made it unseekable. UX_UnitTickets_TicketNumber serves
-- the first seek, PK_Appraisals the second. Both are LEFT: a collateral from before ticketing
-- resolves to nothing here and falls through to the parsed behaviour it has today.
--
-- Shape test, not length. A ticket is eight characters with a literal 'U' at position 3, and
-- appraisal numbers are all digits, so the marker tells them apart. Length cannot — legacy appraisal
-- numbers such as "2560100004" are ten characters too.
LEFT JOIN appraisal.UnitTickets tj
       ON  LEN(LTRIM(RTRIM(h.AppraisalNumber))) = 8
       AND SUBSTRING(LTRIM(RTRIM(h.AppraisalNumber)), 3, 1) = 'U'
       AND tj.TicketNumber = LTRIM(RTRIM(h.AppraisalNumber))
LEFT JOIN appraisal.Appraisals ta
       ON  ta.Id = tj.AppraisalId
       AND ta.IsDeleted = 0
-- The rooms that ticket covers, as one comma list — the same shape the parsed tokens arrive in, so
-- every reader can split it the same way. This one stays an APPLY because it aggregates, and it is
-- not a join key.
--
-- Each unit contributes only the FIRST part of its own key. CAS stores a multi-room unit as a single
-- row whose key reads "1198/831,1198/832"; passing that through whole would split into two parts
-- that both match the same unit row, and a reader summing per part would count its value twice.
OUTER APPLY (
    SELECT STUFF((
               SELECT ',' + CASE WHEN CHARINDEX(',', tu2.UnitKey) > 0
                                 THEN LEFT(tu2.UnitKey, CHARINDEX(',', tu2.UnitKey) - 1)
                                 ELSE tu2.UnitKey
                            END
               FROM appraisal.UnitTicketUnits tu2
               WHERE tu2.UnitTicketId = tj.Id
               ORDER BY tu2.UnitKey
               FOR XML PATH(''), TYPE).value('.', 'nvarchar(400)'), 1, 1, '') AS TicketToken
) tk
-- Everything after the literal 'CONDO', with a leading '.' or '/' and any spaces removed.
CROSS APPLY (SELECT LTRIM(
    CASE WHEN SUBSTRING(h.CollateralName, 6, 1) IN ('.', '/')
         THEN SUBSTRING(h.CollateralName, 7, 200)
         ELSE SUBSTRING(h.CollateralName, 6, 200)
    END) AS Rest) v;
