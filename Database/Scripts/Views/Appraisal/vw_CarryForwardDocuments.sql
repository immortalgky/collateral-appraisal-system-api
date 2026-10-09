-- The files a new request referencing a prior appraisal may reuse, one row per file, keyed by the PRIOR
-- appraisal (AppraisalId). Read by GetCarryForwardDocumentsQueryHandler, which backs both
-- GET /appraisals/{id}/carry-forward-documents and the Integration GET /api/v1/appraisals/{no}/documents.
--
-- Level 'Request' / 'Title': every file the prior request holds, except the summary codes D036 / D042 /
--   D043. Those are the summaries of the appraisal BEFORE this one (or an earlier round's, attached to the
--   request), and carrying them would pile stale summaries up a reappraisal chain; the only summary a new
--   request gets is this appraisal's own, below, re-typed D036.
-- Level 'Summary': this appraisal's D042 / D043 summary report, ALL files of each code. DocumentType keeps
--   the original code; the caller keeps the one that matches the appraisal's type (D042 for a
--   construction inspection, else D043) and re-types it as D036.
--
-- Title rows carry CollateralType and TitleNumber (the per-type number) as the title's KEY. The number is the
-- one rule the request page uses (titleList.ts movableIdentity), kept identical in CarriedDocuments.NumberOf
-- and its PlaceholderSql: vehicle (10) plate, else vehicle registration; machine (11) registration number only
-- when registered; vessel (12) vessel registration, else HIN; anything else the deed TitleNumber.
-- PriorTitleId is information only — callers match copied titles by the key.
--
-- Files are shared by DocumentId, so rows with no file yet (empty checklist placeholders), rows with no
-- document type (cannot be placed into a section) and files soft-deleted in document.Documents are
-- excluded. The link tables have no IsDeleted: unlinking removes
-- the row. CarryForwardByDefault is NULL for 'Summary' rows (always used).
-- No ORDER BY here (not allowed in a view): callers order by GroupOrder, Seq, UploadedAt, RowId.
CREATE OR ALTER VIEW appraisal.vw_CarryForwardDocuments
AS
SELECT a.Id                                    AS AppraisalId,
       'Request'                               AS Level,
       0                                       AS GroupOrder,
       0                                       AS Seq,
       rd.DocumentId,
       rd.DocumentType,
       CAST(NULL AS uniqueidentifier)          AS PriorTitleId,
       CAST(NULL AS nvarchar(10))              AS CollateralType,
       CAST(NULL AS nvarchar(100))             AS TitleNumber,
       COALESCE(rd.FileName, d.FileName)       AS FileName,
       rd.FilePath,
       rd.Prefix,
       CAST(rd.[Set] AS int)                   AS [Set],
       rd.Notes,
       rd.UploadedBy,
       rd.UploadedByName,
       rd.UploadedAt,
       dt.CarryForwardByDefault,
       rd.Id                                   AS RowId
FROM appraisal.Appraisals a
         JOIN request.Requests r ON r.Id = a.RequestId AND r.IsDeleted = 0
         JOIN request.RequestDocuments rd ON rd.RequestId = r.Id
         JOIN document.Documents d ON d.Id = rd.DocumentId AND d.IsDeleted = 0
         LEFT JOIN parameter.DocumentTypes dt ON dt.Code = rd.DocumentType
WHERE a.IsDeleted = 0
  AND rd.DocumentId IS NOT NULL
  AND LTRIM(RTRIM(rd.DocumentType)) <> ''
  AND rd.DocumentType NOT IN ('D036', 'D042', 'D043')

UNION ALL

SELECT a.Id,
       'Title',
       1,
       t.SequenceNumber,
       td.DocumentId,
       td.DocumentType,
       t.Id,
       t.CollateralType,
       CASE
           WHEN t.CollateralType = '10' THEN COALESCE(NULLIF(t.LicensePlateNumber, ''), t.VehicleRegistrationNumber)
           WHEN t.CollateralType = '11' THEN CASE WHEN t.RegistrationStatus = 1 THEN t.RegistrationNumber END
           WHEN t.CollateralType = '12' THEN COALESCE(NULLIF(t.VesselRegistrationNumber, ''), t.HIN)
           ELSE t.TitleNumber
           END,
       COALESCE(td.FileName, d.FileName),
       td.FilePath,
       td.Prefix,
       td.[Set],
       td.Notes,
       td.UploadedBy,
       td.UploadedByName,
       td.UploadedAt,
       dt.CarryForwardByDefault,
       td.Id
FROM appraisal.Appraisals a
         JOIN request.Requests r ON r.Id = a.RequestId AND r.IsDeleted = 0
         JOIN request.RequestTitles t ON t.RequestId = r.Id
         JOIN request.RequestTitleDocuments td ON td.TitleId = t.Id
         JOIN document.Documents d ON d.Id = td.DocumentId AND d.IsDeleted = 0
         LEFT JOIN parameter.DocumentTypes dt ON dt.Code = td.DocumentType
WHERE a.IsDeleted = 0
  AND td.DocumentId IS NOT NULL
  AND LTRIM(RTRIM(td.DocumentType)) <> ''
  AND td.DocumentType NOT IN ('D036', 'D042', 'D043')

UNION ALL

SELECT a.Id,
       'Summary',
       2,
       ad.SortOrder,
       ad.DocumentId,
       ad.DocumentTypeCode,
       NULL,
       NULL,
       NULL,
       COALESCE(d.FileName, ad.FileName),
       NULL,
       NULL,
       1,
       ad.Notes,
       ad.CreatedBy,
       ad.UploadedByName,
       ad.CreatedAt,
       NULL,
       ad.Id
FROM appraisal.Appraisals a
         JOIN appraisal.AppraisalDocuments ad ON ad.AppraisalId = a.Id
         JOIN document.Documents d ON d.Id = ad.DocumentId AND d.IsDeleted = 0
WHERE a.IsDeleted = 0
  AND ad.DocumentTypeCode IN ('D042', 'D043');
