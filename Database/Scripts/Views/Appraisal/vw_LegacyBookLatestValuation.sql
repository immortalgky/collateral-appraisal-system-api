-- A legacy AS400 book's latest valuation in the bank's listing — one row per book (ApplicationId), date and
-- price from the same row. The single definition of "the prior appraisal of a 99A… book", read by
-- collateral.vw_ReappraisalCandidates (last appraisal / prior source), the reappraisal detail page's nearby
-- query, Initiate (the request's prior value/date) and the request page's legacy chain arm, so they agree.
--
-- 1900-01-01 is the source's "no date" placeholder (As400LegacyImporter skips it too): such a row, like one
-- with no date at all, is not a valuation. A book with only those rows has no row here.
-- BookNumber is the raw, char-padded ApplicationId: `=` ignores trailing spaces, so callers compare it as is
-- and the predicate reaches the listing's index; RTRIM only for display or as a dictionary key.
-- The regulatory export's origination is a different rule (the EARLIEST row) and is not read from here.
CREATE OR ALTER VIEW appraisal.vw_LegacyBookLatestValuation
AS
-- Types fixed here: the table is the bank's (our script only creates a placeholder when it is missing),
-- so its column types are not ours — As400LegacyImporter casts the same way.
SELECT x.BookNumber,
       CAST(x.ValuationDate AS date)                  AS ValuationDate,
       CAST(x.ValuationPriceInBaht AS decimal(18, 2)) AS ValuationPriceInBaht
FROM (
    SELECT l.ApplicationId AS BookNumber,
           l.ValuationDate,
           l.ValuationPriceInBaht,
           ROW_NUMBER() OVER (PARTITION BY l.ApplicationId
                              ORDER BY l.ValuationDate DESC, l.ValuationPriceInBaht DESC) AS Rn
    FROM appraisal.AS400ReportListing l
    WHERE l.ApplicationId IS NOT NULL
      AND l.ValuationDate > '1901-01-01'
) x
WHERE x.Rn = 1;
