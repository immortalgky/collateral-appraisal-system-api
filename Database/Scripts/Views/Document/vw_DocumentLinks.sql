-- One row per live link from a record to a file in document.Documents. A file with no row here is
-- unreferenced. Read by the delete path (409 while any row exists) and meant for the orphan-cleanup job.
--
-- Links are hard-deleted when removed, so a row existing means the link is live. Branches whose owner
-- can be soft-deleted also check it (request.Requests, appraisal.Appraisals, collateral.CollateralMasters):
-- soft-deleting the owner leaves its link rows behind, and those must not pin the file. Branches with
-- no soft-deletable owner on the path (MeetingDocuments, PricingAnalysisDocuments,
-- SupportingDataDetailImages) are direct.
--
-- Not included on purpose: dbo.Logs.DocumentId (log correlation, not a reference); the gallery
-- photo-mapping tables (GalleryPhotoId points at appraisal.AppraisalGallery, already listed);
-- parameter.DocumentRequirements.DocumentTypeId (a document type, not a file).
-- LinkId is the row's Id (QuotationSharedDocuments has a composite key: its QuotationRequestId).
-- New table with a DocumentId column: add it here.
CREATE
OR ALTER
VIEW document.vw_DocumentLinks
AS
SELECT rd.DocumentId, 'request.RequestDocuments' AS LinkSource, rd.Id AS LinkId
FROM request.RequestDocuments rd
         INNER JOIN request.Requests r ON r.Id = rd.RequestId AND r.IsDeleted = 0
WHERE rd.DocumentId IS NOT NULL
UNION ALL
SELECT td.DocumentId, 'request.RequestTitleDocuments', td.Id
FROM request.RequestTitleDocuments td
         INNER JOIN request.RequestTitles t ON t.Id = td.TitleId
         INNER JOIN request.Requests r ON r.Id = t.RequestId AND r.IsDeleted = 0
WHERE td.DocumentId IS NOT NULL
UNION ALL
SELECT d.DocumentId, 'appraisal.AppraisalDocuments', d.Id
FROM appraisal.AppraisalDocuments d
         INNER JOIN appraisal.Appraisals a ON a.Id = d.AppraisalId AND a.IsDeleted = 0
WHERE d.DocumentId IS NOT NULL
UNION ALL
SELECT d.DocumentId, 'appraisal.AppendixDocuments', d.Id
FROM appraisal.AppendixDocuments d
         INNER JOIN appraisal.AppraisalAppendices ap ON ap.Id = d.AppraisalAppendixId
         INNER JOIN appraisal.Appraisals a ON a.Id = ap.AppraisalId AND a.IsDeleted = 0
WHERE d.DocumentId IS NOT NULL
UNION ALL
SELECT d.DocumentId, 'appraisal.AppraisalGallery', d.Id
FROM appraisal.AppraisalGallery d
         INNER JOIN appraisal.Appraisals a ON a.Id = d.AppraisalId AND a.IsDeleted = 0
WHERE d.DocumentId IS NOT NULL
UNION ALL
SELECT d.DocumentId, 'appraisal.ConstructionInspections', d.Id
FROM appraisal.ConstructionInspections d
         INNER JOIN appraisal.AppraisalProperties p ON p.Id = d.AppraisalPropertyId
         INNER JOIN appraisal.Appraisals a ON a.Id = p.AppraisalId AND a.IsDeleted = 0
WHERE d.DocumentId IS NOT NULL
UNION ALL
SELECT DocumentId, 'appraisal.PricingAnalysisDocuments', Id
FROM appraisal.PricingAnalysisDocuments
WHERE DocumentId IS NOT NULL
UNION ALL
SELECT d.DocumentId, 'appraisal.ProjectUnitUploads', d.Id
FROM appraisal.ProjectUnitUploads d
         INNER JOIN appraisal.Projects p ON p.Id = d.ProjectId
         INNER JOIN appraisal.Appraisals a ON a.Id = p.AppraisalId AND a.IsDeleted = 0
WHERE d.DocumentId IS NOT NULL
UNION ALL
-- QuotationRequests.RequestId is nullable: no request means nothing to check, and a request row that is
-- missing (r.Id IS NULL) cannot be soft-deleted, so the link stays live.
SELECT d.DocumentId, 'appraisal.QuotationDocuments', d.Id
FROM appraisal.QuotationDocuments d
         INNER JOIN appraisal.QuotationRequests q ON q.Id = d.QuotationRequestId
         LEFT JOIN request.Requests r ON r.Id = q.RequestId
WHERE d.DocumentId IS NOT NULL
  AND (q.RequestId IS NULL OR r.Id IS NULL OR r.IsDeleted = 0)
UNION ALL
SELECT d.DocumentId, 'appraisal.QuotationSharedDocuments', d.QuotationRequestId
FROM appraisal.QuotationSharedDocuments d
         INNER JOIN appraisal.Appraisals a ON a.Id = d.AppraisalId AND a.IsDeleted = 0
WHERE d.DocumentId IS NOT NULL
UNION ALL
SELECT DocumentId, 'appraisal.SupportingDataDetailImages', Id
FROM appraisal.SupportingDataDetailImages
WHERE DocumentId IS NOT NULL
UNION ALL
SELECT d.DocumentId, 'collateral.CollateralDocuments', d.Id
FROM collateral.CollateralDocuments d
         INNER JOIN collateral.CollateralMasters m ON m.Id = d.CollateralMasterId AND m.IsDeleted = 0
WHERE d.DocumentId IS NOT NULL
UNION ALL
SELECT DocumentId, 'workflow.MeetingDocuments', Id
FROM workflow.MeetingDocuments
WHERE DocumentId IS NOT NULL
UNION ALL
-- Follow-up line items keep the DocumentId in the LineItems JSON array ({"id", "documentId", ...}); LinkId is the line item's id.
SELECT j.DocumentId, 'workflow.DocumentFollowups.LineItems', j.LineItemId
FROM workflow.DocumentFollowups f
         INNER JOIN request.Requests r ON r.Id = f.RequestId AND r.IsDeleted = 0
         CROSS APPLY OPENJSON(CASE WHEN ISJSON(f.LineItems) = 1 THEN f.LineItems ELSE '[]' END)
                          WITH (LineItemId uniqueidentifier '$.id', DocumentId uniqueidentifier '$.documentId') j
WHERE j.DocumentId IS NOT NULL
