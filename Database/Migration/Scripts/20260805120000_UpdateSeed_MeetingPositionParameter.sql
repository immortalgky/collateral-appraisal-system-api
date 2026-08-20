-- ============================================================
-- CA: Re-seed parameter.Parameters group 'MeetingPosition' (EN=4, TH=4)
--
-- Feeds the POSITION dropdown on the meeting roster and the Committee Admin page.
-- Three changes against the original seed in 20260317002600_SeedData_GeneralParameter.sql:
--   1. Re-keyed from ordinals ('01'/'02'/'03') to the CommitteeMemberPosition enum NAMES. The
--      meeting/committee APIs bind that enum by name, so a dropdown posting '01' would not bind.
--   2. Added the missing 'UW' — it is required by the COMMITTEE_WITH_MEETING RoleRequired condition
--      yet was absent from the group, so it could never be picked in the UI.
--   3. Replaced the English placeholders in the TH rows with real Thai labels.
--
-- Retired positions (Risk / Appraisal / Credit / Member) are deliberately NOT seeded: they remain
-- on the C# enum so existing rows and historical ApprovalVote.MemberRole values still materialize,
-- but may no longer be assigned. See CommitteeMemberPositions.Selectable.
--
-- DELETE-then-INSERT rather than guarded per-row inserts: the group is being re-keyed, so the old
-- '01'/'02'/'03' rows have to go. Nothing references parameter.Parameters by ParId, and the group
-- had no consumers before this change, so removing the rows is safe.
--
-- Re-runnable: the DELETE makes the INSERT idempotent if this is ever replayed.
--
-- NOTE: CachedParameterRepository caches with no TTL and no eviction on write — restart the API
-- after running this or the dropdown will keep serving the old rows.
-- ============================================================
SET NOCOUNT ON;

DELETE FROM parameter.Parameters WHERE [group] = N'MeetingPosition';

INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'MeetingPosition', N'TH', N'EN', N'Chairman', N'Chairman', 1, 1),
    (N'MeetingPosition', N'TH', N'TH', N'Chairman', N'ประธาน', 1, 1),
    (N'MeetingPosition', N'TH', N'EN', N'Director', N'Director', 1, 2),
    (N'MeetingPosition', N'TH', N'TH', N'Director', N'กรรมการ', 1, 2),
    (N'MeetingPosition', N'TH', N'EN', N'Secretary', N'Secretary', 1, 3),
    (N'MeetingPosition', N'TH', N'TH', N'Secretary', N'เลขานุการฯ', 1, 3),
    (N'MeetingPosition', N'TH', N'EN', N'UW', N'UW', 1, 4),
    (N'MeetingPosition', N'TH', N'TH', N'UW', N'ผู้พิจารณาสินเชื่อ', 1, 4);
GO
