CREATE OR ALTER PROCEDURE [request].[sp_GetNextAppraisalNumber]
AS
BEGIN
    SELECT NEXT VALUE FOR [request].[seq_AppraisalNo] AS NextNumber
END