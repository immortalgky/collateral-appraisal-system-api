-- ============================================================
-- Seed: Meeting Configuration defaults
-- Schema: workflow
-- ============================================================

-- Skip if already seeded
IF EXISTS (SELECT 1 FROM workflow.MeetingConfigurations)
BEGIN
    PRINT 'Meeting configurations already seeded, skipping...';
    RETURN;
END

INSERT INTO workflow.MeetingConfigurations ([Key], [Value], [Description], [UpdatedAt]) VALUES
    (N'MeetingDefaults.TitleTemplate',
     N'ขออนุมัติราคาประเมิน ครั้งที่ {meetingNo}',
     N'Title template applied to new meetings; {meetingNo} is substituted at creation.',
     SYSUTCDATETIME()),
    (N'MeetingDefaults.Location',
     N'ห้องประชุม 32/1',
     N'Default meeting room.',
     SYSUTCDATETIME()),
    (N'MeetingDefaults.AgendaFromText',
     N'เลขานุการคณะกรรมการฯ',
     N'Default "From" addressee on the agenda.',
     SYSUTCDATETIME()),
    (N'MeetingDefaults.AgendaToText',
     N'คณะกรรมการกำหนดราคาประเมินหลักประกันและทรัพย์สิน',
     N'Default "To" addressee on the agenda.',
     SYSUTCDATETIME());

PRINT 'Seeded 4 meeting configuration defaults';
GO
