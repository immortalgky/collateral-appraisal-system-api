IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'QuotationEmails' AND schema_id = SCHEMA_ID('appraisal'))
BEGIN
    CREATE TABLE [appraisal].[QuotationEmails] (
        [Id]                 UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_QuotationEmails PRIMARY KEY,
        [QuotationRequestId] UNIQUEIDENTIFIER NOT NULL,
        [From]               NVARCHAR(500)    NOT NULL,
        [To]                 NVARCHAR(500)    NOT NULL,
        [Cc]                 NVARCHAR(500)    NULL,
        [Subject]            NVARCHAR(500)    NOT NULL,
        [Content]            NVARCHAR(4000)   NULL
    );
    CREATE INDEX IX_QuotationEmails_QuotationRequestId ON [appraisal].[QuotationEmails]([QuotationRequestId]);
END
