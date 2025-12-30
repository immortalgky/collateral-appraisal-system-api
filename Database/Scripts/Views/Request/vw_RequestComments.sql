CREATE
OR ALTER
VIEW request.vw_RequestComments AS
SELECT Id,
       RequestId,
       Comment,
       CommentedBy,
       CommentedByName,
       CommentedAt,
       LastModifiedAt
FROM request.RequestComments