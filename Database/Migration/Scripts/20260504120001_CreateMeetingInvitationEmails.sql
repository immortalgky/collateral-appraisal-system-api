IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MeetingInvitationEmails' AND schema_id = SCHEMA_ID('workflow'))
BEGIN
    CREATE TABLE [workflow].[MeetingInvitationEmails] (
        [Id]          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_MeetingInvitationEmails PRIMARY KEY,
        [MeetingId]   UNIQUEIDENTIFIER NOT NULL,
        [From]        NVARCHAR(500)    NOT NULL,
        [To]          NVARCHAR(500)    NOT NULL,
        [Subject]     NVARCHAR(500)    NOT NULL,
        [Content]     NVARCHAR(4000)   NULL,
        [Attachments] NVARCHAR(2000)   NULL
    );
    CREATE INDEX IX_MeetingInvitationEmails_MeetingId ON [workflow].[MeetingInvitationEmails]([MeetingId]);
END
