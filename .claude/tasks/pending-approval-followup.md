# Pending Approval Followup — Backend Redesign

## Goal
Repurpose the "Meeting Followup" monitoring section into a "Pending Approval Followup" view
showing pending committee-approval tasks across all 3 tiers (one row per appraisal).

## Todo

- [x] 1. Create `Database/Scripts/Views/Common/vw_MonitoringPendingApprovals.sql`
- [x] 2. Update `MeetingFollowupDto.cs` to new columns
- [x] 3. Update `MeetingFollowupFilter.cs` — add `int[]? Tier`, `string[]? SlaStatus`, `string[]? SlaBucket`
- [x] 4. Update `GetMeetingFollowupsEndpoint.cs` — add new query params + description
- [x] 5. Update `GetMeetingFollowupsSummaryEndpoint.cs` — add new query params + description
- [x] 6. Rewrite `GetMeetingFollowupsQueryHandler.cs` — new view, new sort fields, new filters
- [x] 7. Rewrite `GetMeetingFollowupsSummaryQueryHandler.cs` — bucket counts from WorstSlaStatus
- [x] 8. Update `AuthDataSeed.cs` — update MONITORING:MEETING_FOLLOWUP description
- [x] 9. Build and verify 0 errors

## Review

Files created/modified:
1. `Database/Scripts/Views/Common/vw_MonitoringPendingApprovals.sql` — new view, one row per appraisal
2. `MeetingFollowupDto.cs` — all new columns replacing old meeting-centric shape
3. `MeetingFollowupFilter.cs` — added Tier, SlaStatus, SlaBucket
4. `GetMeetingFollowupsEndpoint.cs` — updated params + description
5. `GetMeetingFollowupsSummaryEndpoint.cs` — updated params + description
6. `GetMeetingFollowupsQueryHandler.cs` — rewritten to query new view
7. `GetMeetingFollowupsSummaryQueryHandler.cs` — now returns real bucket counts
8. `AuthDataSeed.cs` — updated description text
