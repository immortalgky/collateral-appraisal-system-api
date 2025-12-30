CREATE
OR ALTER
VIEW request.vw_RequestDocuments AS
SELECT Id,
       RequestId,
       DocumentId,
       DocumentType,
       FileName,
       Prefix, 
       [Set], 
       Notes, 
       UploadedBy, 
       UploadedByName, 
       UploadedAt
FROM request.RequestDocuments