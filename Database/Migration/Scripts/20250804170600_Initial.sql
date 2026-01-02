-- Drop the sequence if it exists
IF OBJECT_ID('request.seq_AppraisalNo', 'SO') IS NOT NULL
DROP SEQUENCE request.seq_AppraisalNo;
GO

-- Create the sequence
CREATE SEQUENCE request.seq_AppraisalNo
    AS INT
    START WITH 1
    INCREMENT BY 1
    CACHE 50;
GO