using Appraisal.Application.Features.Shared;
using Appraisal.Domain.Appraisals.Exceptions;
using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalBrief;

/// <summary>
/// Backs the credit-side brief screen.
///
/// This handler is the ONE place the release rule is written. Everywhere else that rule is
/// expressed as "blank these columns" (<see cref="AppraisalFieldScope"/>); here it decides
/// whether a whole section exists at all, because a locked screen must not ship the values or
/// the document ids in the payload for someone to read out of the network tab.
///
/// The rule itself is deliberately dull: the price is released once the committee has approved
/// it, which <c>Appraisal.MarkApprovedByCommittee</c> records as Status = Completed. A cancelled
/// appraisal never qualifies — it closed without a price, so there is nothing to release.
///
/// NO COMPANY ROW SCOPE, deliberately — it reads like every other by-id appraisal endpoint.
/// -------------------------------------------------------------------------------------
/// Cross-company separation in this system happens at the DISCOVERY layer, not the read layer:
/// <c>AppraisalFilterBuilder</c> and <c>QuickSearchQueryHandler</c> both force
/// <c>AssigneeCompanyId = @ScopedCompanyId</c> for an external caller, so one firm never sees
/// another's appraisals in the list, the quick search or the export. The by-id reads —
/// <c>GET /appraisals/{id}</c>, the workspace tabs, <c>/appraisals/{id}/documents</c> — carry no
/// such predicate, and the appraisal id is a v7 GUID.
///
/// A row scope WAS written here and removed on 2026-09-15 after five attempts, each of which
/// locked out someone entitled: matching the user code missed the whole quotation fan-out; reusing
/// PoolTaskAccess opened a cross-company read (a bare group name is not company-specific); a strict
/// company column 404'd the negotiation round; and the team-qualified form died the moment a task
/// was claimed or completed, because ClaimTaskCommandHandler and TaskCompletedDomainEventHandler
/// rewrite AssignedTo to a bare username while leaving AssigneeCompanyId NULL. The link between a
/// firm and a job simply moves — invitation, negotiation, assignment — and no single predicate
/// tracks it.
///
/// It also bought nothing: the neighbouring by-id endpoints already return the facility limit, the
/// appraised value and the same document ids to any APPRAISAL_VIEW holder, which every external
/// firm's roles hold. Scoping ONE door in an unlocked corridor cost real users access and closed
/// nothing. Doing it properly means task-level authorisation across all 13 by-id call sites at
/// once — recorded in the backlog, and not this change's job.
/// </summary>
public class GetAppraisalBriefQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser
) : IQueryHandler<GetAppraisalBriefQuery, GetAppraisalBriefResult>
{
    /// <summary>The two documents credit actually files. Everything else rides along behind them.</summary>
    private static readonly string[] PrimaryDocumentCodes = ["D043", "D001"];

    public async Task<GetAppraisalBriefResult> Handle(
        GetAppraisalBriefQuery query,
        CancellationToken cancellationToken)
    {
        // One round trip. The result sets are independent, so batching them keeps this at
        // the cost of the header query alone — the pattern the reporting providers already use.
        const string sql = """
                           SELECT  a.Id,
                                   a.RequestId,
                                   a.AppraisalNumber,
                                   r.RequestNumber,
                                   rd.LoanApplicationNumber,
                                   v.Status,
                                   a.AppraisalType,
                                   a.Purpose,
                                   a.Channel,
                                   v.PropertyTypes,
                                   v.CustomerName,
                                   v.CustomerCount,
                                   c.ContactNumber          AS CustomerContactNumber,
                                   a.RequestedBy,
                                   NULLIF(LTRIM(RTRIM(CONCAT(NULLIF(u.FirstName, N''), N' ',
                                                             NULLIF(u.LastName, N'')))), N'')
                                                            AS RequestedByName,
                                   a.RequestedAt,
                                   v.AppointmentDateTime,
                                   a.CompletedAt,
                                   a.SLADueDate,
                                   a.FacilityLimit,
                                   a.ApprovedByCommittee,
                                   va.AppraisedValue,
                                   va.ForcedSaleValue,
                                   va.InsuranceValue,
                                   va.ValuationDate
                           FROM appraisal.Appraisals a
                                    JOIN appraisal.vw_AppraisalList v ON v.Id = a.Id
                                    LEFT JOIN request.Requests r ON r.Id = a.RequestId
                                    LEFT JOIN request.RequestDetails rd ON rd.RequestId = a.RequestId
                                    LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = a.Id
                                    LEFT JOIN auth.AspNetUsers u ON u.UserName = a.RequestedBy
                                    OUTER APPLY (SELECT TOP 1 rc.ContactNumber
                                                 FROM request.RequestCustomers rc
                                                 WHERE rc.RequestId = a.RequestId
                                                 ORDER BY rc.Id) c
                           WHERE a.Id = @AppraisalId AND a.IsDeleted = 0;

                           -- Collateral items. Title/area/location are assembled from whichever
                           -- detail table the property actually has; a machine or vehicle has
                           -- none of them and simply reports its description.
                           SELECT  ap.Id,
                                   ap.SequenceNumber,
                                   ap.PropertyType,
                                   COALESCE(lt.TitleNumber, cd.RoomNumber, ap.Description)  AS Title,
                                   -- From `la`, the SUM over every title, not from `lt`: `lt` is
                                   -- the TOP 1 title and now carries only TitleNumber.
                                   COALESCE(
                                       CASE WHEN la.AreaRai IS NOT NULL OR la.AreaNgan IS NOT NULL
                                                 OR la.AreaSquareWa IS NOT NULL
                                            THEN CONCAT(ISNULL(la.AreaRai, 0), N'-', ISNULL(la.AreaNgan, 0), N'-',
                                                        ISNULL(la.AreaSquareWa, 0), N' ไร่') END,
                                       CASE WHEN cd.UsableArea IS NOT NULL
                                            THEN CONCAT(CONVERT(NVARCHAR(30), cd.UsableArea), N' ตร.ม.') END)
                                                                                            AS Area,
                                   -- AssetRow is a POSITIONAL record, so Dapper binds these by
                                   -- column order. The group/sequence columns the ORDER BY uses
                                   -- are deliberately not selected: appending them here would be
                                   -- harmless today but the next column inserted above them would
                                   -- silently shift every field by one.
                                   COALESCE(ld.SubDistrict, cd.SubDistrict)                 AS Location,
                                   bd.BuildingType,
                                   bd.NumberOfFloors,
                                   -- Land area as raw components, not the formatted string above:
                                   -- the client totals a group's parcels, and rai-ngan-wa carry
                                   -- across at 4 ngan and 100 wa, so it cannot add display text.
                                   la.AreaRai, la.AreaNgan, la.AreaSquareWa,
                                   -- A machine has no title, area or location, so its own name is
                                   -- the only thing that identifies it on the summary row.
                                   COALESCE(NULLIF(md.MachineName, N''), NULLIF(md.PropertyName, N''))
                                                                                            AS MachineName,
                                   -- The free text behind BuildingType '99' (อื่นๆ). Appended at
                                   -- the END of both the SELECT and AssetRow, not beside
                                   -- BuildingType: the record is positional, so inserting in the
                                   -- middle silently shifts every field after it.
                                   NULLIF(bd.BuildingTypeOther, N'')                         AS BuildingTypeOther,
                                   -- Condo: the tower's storey count and the floor the unit is
                                   -- on. FloorNumber is nvarchar and carries junk on the dev
                                   -- database ('-' on rows where nobody filled it in), so it is
                                   -- passed through as text and parsed by the caller rather than
                                   -- coerced here, where a failure would be silent.
                                   cd.NumberOfFloors                                        AS CondoFloors,
                                   NULLIF(cd.FloorNumber, N'')                               AS CondoFloorNumber
                           FROM appraisal.AppraisalProperties ap
                                    -- One group row per property (verified: no property belongs to
                                    -- two groups), so this cannot multiply rows. LEFT, not INNER:
                                    -- 76 of 105,660 properties have no group row and an inner join
                                    -- would drop them from the panel without a trace.
                                    LEFT JOIN appraisal.PropertyGroupItems pgi
                                              ON pgi.AppraisalPropertyId = ap.Id
                                    LEFT JOIN appraisal.PropertyGroups pg
                                              ON pg.Id = pgi.PropertyGroupId
                                    LEFT JOIN appraisal.LandAppraisalDetails ld ON ld.AppraisalPropertyId = ap.Id
                                    LEFT JOIN appraisal.CondoAppraisalDetails cd ON cd.AppraisalPropertyId = ap.Id
                                    LEFT JOIN appraisal.BuildingAppraisalDetails bd
                                              ON bd.AppraisalPropertyId = ap.Id
                                    LEFT JOIN appraisal.MachineryAppraisalDetails md
                                              ON md.AppraisalPropertyId = ap.Id
                                    OUTER APPLY (SELECT TOP 1 t.TitleNumber
                                                 FROM appraisal.LandTitles t
                                                 WHERE t.LandAppraisalDetailId = ld.Id
                                                 ORDER BY t.Id) lt
                                    -- Area is SUMMED over every title, not taken from the first.
                                    -- One land property can hold several deeds — 16 of them do on
                                    -- the dev database — and TOP 1 silently reported one parcel's
                                    -- area as the property's, which then understated the
                                    -- application's total. The title NUMBER above is still TOP 1:
                                    -- that one is a display name, not a quantity.
                                    OUTER APPLY (SELECT SUM(t.AreaRai)      AS AreaRai,
                                                        SUM(t.AreaNgan)     AS AreaNgan,
                                                        SUM(t.AreaSquareWa) AS AreaSquareWa
                                                 FROM appraisal.LandTitles t
                                                 WHERE t.LandAppraisalDetailId = ld.Id) la
                           WHERE ap.AppraisalId = @AppraisalId
                           -- Group then sequence-in-group: the order the Property Information
                           -- screen lists them in. AppraisalProperties.SequenceNumber is a
                           -- different axis (order of entry on the appraisal) and put the types in
                           -- an order that matched no other screen. Ungrouped properties sort last
                           -- rather than first, which is where a NULL would otherwise land them.
                           ORDER BY CASE WHEN pg.GroupNumber IS NULL THEN 1 ELSE 0 END,
                                    pg.GroupNumber, pgi.SequenceInGroup, ap.SequenceNumber, ap.Id;

                           -- Only rows that actually have a file: the checklist's empty types are
                           -- the appraiser's to-do list, not something credit can open.
                           SELECT  ad.DocumentId,
                                   dt.Code                                           AS TypeCode,
                                   dt.Name                                           AS TypeName,
                                   dt.NameTh                                         AS TypeNameTh,
                                   COALESCE(d.FileName, ad.FileName)                 AS FileName,
                                   COALESCE(d.FileSizeBytes, ad.FileSizeBytes)       AS FileSizeBytes,
                                   ad.CreatedAt                                      AS UploadedAt
                           FROM appraisal.AppraisalDocuments ad
                                    JOIN parameter.DocumentTypes dt ON dt.Code = ad.DocumentTypeCode
                                    LEFT JOIN document.Documents d ON d.Id = ad.DocumentId
                           WHERE ad.AppraisalId = @AppraisalId
                             AND ad.DocumentId IS NOT NULL
                             AND dt.Category IN ('VAL_DOC', 'VAL_REPORT')
                           ORDER BY dt.SortOrder, dt.Code, ad.SortOrder, ad.Id;

                           -- Who is holding it right now. PendingTasks.CorrelationId is the
                           -- REQUEST id, not the appraisal id (see GetAppraisalWorkflowProgress).
                           -- Reading a workflow table from this module follows the precedent set by
                           -- GetQuotationsQueryHandler; the alternative is a second HTTP hop for one
                           -- row. AssignedType '1' means a named user — anything else is a firm, and
                           -- then AssigneeCompanyId carries the contact instead.
                           SELECT TOP 1
                                   pt.TaskName                                   AS ActivityName,
                                   pt.ActivityId,
                                   -- AssignedType is the authority on person-vs-pool.
                                   --
                                   -- An earlier version tested "does a user with this name exist"
                                   -- instead, to catch pool names the engine mislabels as '1'. That
                                   -- was worse: a GROUP name can equal a USERNAME — the dev database
                                   -- has 13 live pool rows assigned to "Admin" while auth.AspNetUsers
                                   -- holds "admin", and the collation is case-insensitive — so the
                                   -- card named a person who did not hold the work and published the
                                   -- break-glass account's contact details. The mislabelling is a
                                   -- WRITER bug (see the backlog) and is fixed there, not worked
                                   -- around here.
                                   CASE WHEN pt.AssignedType = '1' THEN pt.AssignedTo END
                                                                                 AS AssignedTo,
                                   NULLIF(LTRIM(RTRIM(CONCAT(NULLIF(hu.FirstName, N''), N' ',
                                                             NULLIF(hu.LastName, N'')))), N'')
                                                                                 AS Name,
                                   hu.Email, hu.PhoneNumber, hu.Department, hu.Position,
                                   COALESCE(ac.Name, pc.Name, hc.Name)           AS CompanyName,
                                   COALESCE(ac.NameLocal, pc.NameLocal, hc.NameLocal)
                                                                                 AS CompanyNameLocal,
                                   COALESCE(ac.Phone, pc.Phone, hc.Phone)        AS CompanyPhone,
                                   COALESCE(ac.Email, pc.Email, hc.Email)        AS CompanyEmail,
                                   COALESCE(ac.ContactPerson, pc.ContactPerson, hc.ContactPerson)
                                                                                 AS CompanyContactPerson,
                                   pt.AssigneeAssignedAt                         AS HeldSince,
                                   -- Partitioned by ACTIVITY: the label this feeds says "under
                                   -- joint review by N members", which is a claim about THIS step.
                                   -- Counting every pending row on the request made one stale task
                                   -- left behind by a closed instance — which the CurrentHolder
                                   -- comment below says to expect — turn a single-holder step into
                                   -- a two-member committee.
                                   COUNT(*) OVER (PARTITION BY pt.ActivityId)    AS PendingCount,
                                   -- Appended at the END: BriefHolder binds positionally.
                                   -- The POOL the task sits in, when it is not held by a named
                                   -- person. AssignedType '1' is a person and is already read into
                                   -- AssignedTo above; anything else is a group — "IntAdmin",
                                   -- "ExtAdmin:Team_<companyId>" — and without this the panel had
                                   -- no one to name at all for a queued task and rendered
                                   -- "unassigned", which is not what "waiting in the admin pool"
                                   -- means to a credit officer.
                                   -- The complement of AssignedTo above.
                                   CASE WHEN pt.AssignedType <> '1' THEN pt.AssignedTo END
                                                                                 AS PoolName
                           FROM workflow.PendingTasks pt
                                    -- The AssignedType guard is load-bearing: without it a pool
                                    -- row whose group name equals a username ("Admin" vs "admin")
                                    -- joins to that user and the card reports the wrong holder.
                                    LEFT JOIN auth.AspNetUsers hu ON pt.AssignedType = '1'
                                                                 AND hu.UserName = pt.AssignedTo
                                    LEFT JOIN auth.Companies hc ON hc.Id = hu.CompanyId
                                                               AND hc.IsDeleted = 0
                                    LEFT JOIN auth.Companies ac ON ac.Id = pt.AssigneeCompanyId
                                                               AND ac.IsDeleted = 0
                                    -- The company a TEAM-SCOPED pool row belongs to.
                                    --
                                    -- PoolAssigneeSelector writes "<group>:Team_<teamId>" whenever
                                    -- the pipeline resolved a team, and it goes out through
                                    -- TaskAssignedEventHandler, which never sets AssigneeCompanyId
                                    -- — so `ac` above is NULL and the card had a group name and no
                                    -- way to contact anyone. CompanyTeamService returns an external
                                    -- user's CompanyId as their TeamId — but ONLY as a fallback:
                                    -- it reads auth.TeamMembers first, so for an internal holder
                                    -- (and for an external user who was added to an internal team)
                                    -- the guid is a real auth.Teams id and NOT a company at all.
                                    -- That is why this is a JOIN and not an assumption: a team id
                                    -- matches no row in auth.Companies, the company columns stay
                                    -- NULL, and the card shows the group with no firm attached —
                                    -- which is the correct answer for an internal pool. Verified
                                    -- against a real auth.Teams id, and the two tables share no id.
                                    --
                                    -- Resolved from the ROW rather than from the appraisal's
                                    -- external assignment because the assignment is not always
                                    -- there. Once the appraisal IS assigned the two agree — the
                                    -- team on the task is the firm on the assignment — but a
                                    -- team-scoped task exists BEFORE that, while the firm is only
                                    -- quoting or negotiating and no AppraisalAssignments row has
                                    -- been created yet. Reading the row answers both cases with
                                    -- the same expression and needs no invariant to hold.
                                    --
                                    -- No activity in appraisal-workflow.json emits this shape today
                                    -- (its only pool is IntAdmin, unteamed) — but a workflow
                                    -- definition is data an admin edits, so this must not depend on
                                    -- today's JSON.
                                    LEFT JOIN auth.Companies pc
                                           ON pt.AssigneeCompanyId IS NULL
                                          AND CHARINDEX(':Team_', pt.AssignedTo) > 0
                                          AND pc.Id = TRY_CONVERT(UNIQUEIDENTIFIER,
                                                  SUBSTRING(pt.AssignedTo,
                                                            CHARINDEX(':Team_', pt.AssignedTo) + 6,
                                                            36))
                                          AND pc.IsDeleted = 0
                           WHERE pt.CorrelationId = (SELECT a2.RequestId
                                                     FROM appraisal.Appraisals a2
                                                     WHERE a2.Id = @AppraisalId)
                           ORDER BY pt.AssigneeAssignedAt DESC, pt.Id DESC;

                           -- The committee sitting this appraisal is tabled at, if any. Latest
                           -- first: an appraisal routed back from one meeting is re-tabled at the
                           -- next, and the current sitting is the one a reader is asking about.
                           SELECT TOP 1 m.Title, m.StartAt, m.Status
                           FROM workflow.MeetingItems mi
                                    JOIN workflow.Meetings m ON m.Id = mi.MeetingId
                           WHERE mi.AppraisalId = @AppraisalId
                           ORDER BY m.StartAt DESC, m.CreatedAt DESC;

                           -- The valuation firm, if this went outside, plus its admin.
                           --
                           -- Resolves THE assignment first, then looks the company up off it.
                           -- Filtering and ordering inside one query picked a different row than
                           -- the scope predicate above whenever the newest live assignment had no
                           -- company (reassigned back to an in-house appraiser) or its company row
                           -- was soft-deleted: the extra `AssigneeCompanyId IS NOT NULL` and the
                           -- INNER JOIN silently fell through to an older firm, so the panel named
                           -- a firm that no longer holds the work — to a reader from a different
                           -- firm.
                           --
                           -- The JOIN below is INNER on purpose, and that is what makes an
                           -- in-house appraisal render no block at all: a LEFT JOIN would emit one
                           -- all-NULL row, and the client would draw an empty external-firm card
                           -- on every appraisal the bank handled itself.
                           SELECT TOP 1
                                   co.Name, co.NameLocal, co.Phone, co.Email, co.ContactPerson,
                                   NULLIF(LTRIM(RTRIM(CONCAT(NULLIF(adm.FirstName, N''), N' ',
                                                             NULLIF(adm.LastName, N'')))), N'')
                                                                          AS AdminName,
                                   adm.PhoneNumber                        AS AdminPhone,
                                   adm.Email                              AS AdminEmail
                           FROM (SELECT TOP 1 aa.AssigneeCompanyId
                                 FROM appraisal.AppraisalAssignments aa
                                 WHERE aa.AppraisalId = @AppraisalId
                                   AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
                                 ORDER BY aa.AssignedAt DESC, aa.CreatedAt DESC, aa.Id DESC) live
                                    JOIN auth.Companies co
                                         ON co.Id = TRY_CAST(live.AssigneeCompanyId AS uniqueidentifier)
                                        AND co.IsDeleted = 0
                                    -- TOP 1 on the admin too: the role is one-per-firm in practice
                                    -- but nothing in the schema enforces it, and a second admin
                                    -- would otherwise duplicate the company row.
                                    OUTER APPLY (SELECT TOP 1 u.FirstName, u.LastName,
                                                        u.PhoneNumber, u.Email
                                                 FROM auth.AspNetUsers u
                                                          JOIN auth.AspNetUserRoles ur ON ur.UserId = u.Id
                                                          JOIN auth.AspNetRoles r ON r.Id = ur.RoleId
                                                                                 AND r.Name = N'ExtAdmin'
                                                 WHERE u.CompanyId = co.Id AND u.IsActive = 1
                                                 ORDER BY u.UserName) adm;

                           -- The appraisal department's own admins — the desk credit rings when
                           -- the holder is not the right person to ask. Not appraisal-specific:
                           -- it is the same desk for every request, and it is here rather than in
                           -- a separate endpoint because the panel already makes this one call.
                           --
                           -- Membership comes from the IntAdmin GROUP, not the IntAdmin role, and
                           -- the two are not the same set: the role is an access grant handed out
                           -- wherever someone needs admin rights, so on the dev database its six
                           -- holders span Collateral Appraisal, Credit Management and an external
                           -- QA account — none of which is a desk to publish to credit. The group
                           -- is the curated list, and it resolves to the appraisal admin alone.
                           SELECT  NULLIF(LTRIM(RTRIM(CONCAT(NULLIF(u.FirstName, N''), N' ',
                                                             NULLIF(u.LastName, N'')))), N'')
                                                          AS Name,
                                   -- Appended at the END: BriefContact is a POSITIONAL record, so
                                   -- Dapper binds it by column order and inserting above this line
                                   -- would shift every field by one.
                                   u.Email, u.PhoneNumber, u.Department, u.UserName
                           FROM auth.Groups g
                                    JOIN auth.GroupUsers gu ON gu.GroupId = g.Id
                                    JOIN auth.AspNetUsers u ON u.Id = gu.UserId
                           WHERE g.Name = N'IntAdmin'
                             AND g.IsDeleted = 0
                             AND u.IsActive = 1
                           ORDER BY u.Department, u.UserName;

                           -- A BLOCK appraisal values a whole project, and it has ZERO
                           -- AppraisalProperties — the collateral lives on appraisal.Projects
                           -- instead. The assets query above therefore returns nothing for one,
                           -- and the panel was showing "no collateral on this request" for an
                           -- entire class of appraisal (12 on the dev database, ~1,600 in the
                           -- bank's data). One row at most: Projects is 1:1 with the appraisal.
                           SELECT TOP 1
                                   p.ProjectName, p.ProjectType, p.Developer,
                                   p.UnitForSaleCount,
                                   p.LandAreaRai, p.LandAreaNgan, p.LandAreaSquareWa,
                                   p.SubDistrict, p.NumberOfPhase,
                                   (SELECT COUNT(*) FROM appraisal.ProjectTowers pt
                                    WHERE pt.ProjectId = p.Id)                    AS TowerCount,
                                   -- Storeys, from whichever source actually has them.
                                   -- ProjectTowers.NumberOfFloors is the natural home and is the
                                   -- first choice, but it is NULL on every row of the dev database
                                   -- — the height only exists as the highest floor any unit was
                                   -- uploaded on. ProjectUnits.Floor is free text, hence
                                   -- TRY_CONVERT: a tower named "A" in that column must not
                                   -- abort the query.
                                   COALESCE(
                                       (SELECT MAX(pt.NumberOfFloors) FROM appraisal.ProjectTowers pt
                                        WHERE pt.ProjectId = p.Id),
                                       (SELECT MAX(TRY_CONVERT(int, pu.Floor)) FROM appraisal.ProjectUnits pu
                                        WHERE pu.ProjectId = p.Id))               AS MaxFloor,
                                   -- A HORIZONTAL project (a housing estate) has no towers at
                                   -- all. Its shape comes from the units instead: how many, and
                                   -- how many storeys each house has. Note this is
                                   -- ProjectUnits.NumberOfFloors — the storeys OF a house —
                                   -- and not ProjectUnits.Floor, which is the floor a condo unit
                                   -- sits ON. Two different facts in two similarly named columns.
                                   (SELECT COUNT(*) FROM appraisal.ProjectUnits pu
                                    WHERE pu.ProjectId = p.Id)                    AS UnitCount,
                                   (SELECT MAX(pu.NumberOfFloors) FROM appraisal.ProjectUnits pu
                                    WHERE pu.ProjectId = p.Id)                    AS UnitStoreys
                           FROM appraisal.Projects p
                           WHERE p.AppraisalId = @AppraisalId;
                           """;

        var connection = connectionFactory.GetOpenConnection();
        await using var grid = await connection.QueryMultipleAsync(new CommandDefinition(
            sql, new { query.AppraisalId }, cancellationToken: cancellationToken));

        // Read order must match SELECT order.
        var header = await grid.ReadFirstOrDefaultAsync<HeaderRow>();
        // No such row: an id that does not exist, or one that was soft-deleted. There is no company
        // scope on this read (see the class doc), so this is the only thing a 404 from here means.
        if (header is null) throw new AppraisalNotFoundException(query.AppraisalId);

        var assets = (await grid.ReadAsync<AssetRow>()).ToList();
        var docs = (await grid.ReadAsync<DocumentRow>()).ToList();
        var holder = await grid.ReadFirstOrDefaultAsync<BriefHolder>();
        var meeting = await grid.ReadFirstOrDefaultAsync<BriefMeeting>();
        var externalCompany = await grid.ReadFirstOrDefaultAsync<BriefExternalCompany>();
        var appraisalAdmins = (await grid.ReadAsync<BriefContact>()).ToList();
        var project = await grid.ReadFirstOrDefaultAsync<BriefProject>();

        // The appraisal's own state, independent of who is asking.
        var approved = AppraisalFieldScope.IsReleased(header.Status);
        var cancelled = string.Equals(header.Status, "Cancelled", StringComparison.OrdinalIgnoreCase);

        // Whether THIS caller may have the figures. The release rule is a credit-side disclosure
        // control, so it fires only for a tracking-only caller — never for the appraisal team.
        //
        // This panel is not the credit slide-over alone: ActivityTrackingPage mounts the same
        // component as a tab inside the appraisal workspace and inside the task tree, so applying
        // the rule unconditionally took the price off an existing screen and told an internal
        // appraiser "the committee has not approved it yet" about work they are doing themselves.
        // `!cancelled`, not plain true: cancelled work closed WITHOUT an approved price, so there is
        // nothing to release to anyone. Dropping that term made a cancelled appraisal come back as
        // IsReleased for every internal caller, and the panel reads `isCancelled` only inside the
        // locked branch — so the ban icon, the cancelled notice and DocumentList's cancelled state
        // all became unreachable, leaving the normal three-figure block over numbers nobody approved.
        var released = approved || (!AppraisalFieldScope.IsTrackingOnly(currentUser) && !cancelled);

        // Documents follow a LOOSER rule than the money: a non-credit caller gets them whatever
        // the status. The `!cancelled` term above exists so the panel can say "this was cancelled
        // before a price was issued" instead of showing figures nobody approved — that is an
        // argument about the PRICE. Applied to the file list it also took the appraisal folder off
        // an internal appraiser opening the tracking tab of a cancelled appraisal, who was looking
        // at the same files on the Documents tab one click away.
        var documentsReleased = approved || !AppraisalFieldScope.IsTrackingOnly(currentUser);

        return new GetAppraisalBriefResult
        {
            Id = header.Id,
            AppraisalNumber = header.AppraisalNumber,
            RequestNumber = header.RequestNumber,
            LoanApplicationNumber = header.LoanApplicationNumber,
            Status = header.Status,
            AppraisalType = header.AppraisalType,
            Purpose = header.Purpose,
            Channel = header.Channel,
            PropertyTypes = header.PropertyTypes,
            CustomerName = header.CustomerName,
            CustomerCount = header.CustomerCount,
            CustomerContactNumber = header.CustomerContactNumber,
            RequestedBy = header.RequestedBy,
            RequestedByName = header.RequestedByName,
            RequestedAt = header.RequestedAt,
            AppointmentDateTime = header.AppointmentDateTime,
            CompletedAt = header.CompletedAt,
            DueDate = header.SLADueDate,
            FacilityLimit = header.FacilityLimit,

            IsReleased = released,
            DocumentsReleased = documentsReleased,

            // Withheld rather than zeroed: the screen distinguishes "no price yet" from "zero".
            AppraisalValue = released ? header.AppraisedValue : null,
            ForcedSaleValue = released ? header.ForcedSaleValue : null,
            InsuranceValue = released ? header.InsuranceValue : null,
            ValuationDate = released ? header.ValuationDate : null,
            ApprovedByCommittee = released ? header.ApprovedByCommittee : null,

            // Only while the work is actually still moving. A finished or cancelled appraisal has
            // nobody to chase, and naming the last person to touch it would invite exactly the
            // phone call this is meant to save.
            //
            // The status test is here rather than in the SQL because a pending task is not proof
            // the work is live: the engine archives the previous task when it assigns the next
            // one, so the LAST task of a workflow is only archived if something archives it, and a
            // row left behind by a crashed or manually closed instance would otherwise put a
            // holder card on a finished appraisal.
            CurrentHolder = approved || cancelled ? null : holder,
            Meeting = meeting,
            ExternalCompany = externalCompany,
            AppraisalAdmins = appraisalAdmins,
            Project = project,

            // The item list is fine to show while locked — it is the collateral the credit officer
            // put on the request in the first place, so nothing here is the appraiser's to release.
            Assets = assets.Select(a => new BriefAsset(
                a.Id, a.SequenceNumber, a.PropertyType, a.Title, a.Area, a.Location,
                a.BuildingType, a.NumberOfFloors,
                a.AreaRai, a.AreaNgan, a.AreaSquareWa, a.MachineName,
                a.BuildingTypeOther, a.CondoFloors, a.CondoFloorNumber)).ToList(),

            // Documents are withheld whole: a DocumentId is a working download link on its own
            // (GET /documents/{id}/download), so shipping the ids of a locked appraisal would
            // hand over the files no matter what the client renders.
            Documents = documentsReleased
                ? docs.Select(d => new BriefDocument(
                    d.DocumentId, d.TypeCode, d.TypeName, d.TypeNameTh,
                    PrimaryDocumentCodes.Contains(d.TypeCode),
                    d.FileName, d.FileSizeBytes, d.UploadedAt)).ToList()
                : []
        };
    }

    private sealed record HeaderRow
    {
        public Guid Id { get; init; }
        public Guid RequestId { get; init; }
        public string? AppraisalNumber { get; init; }
        public string? RequestNumber { get; init; }
        public string? LoanApplicationNumber { get; init; }
        public string Status { get; init; } = null!;
        public string? AppraisalType { get; init; }
        public string? Purpose { get; init; }
        public string? Channel { get; init; }
        public string? PropertyTypes { get; init; }
        public string? CustomerName { get; init; }
        public int CustomerCount { get; init; }
        public string? CustomerContactNumber { get; init; }
        public string? RequestedBy { get; init; }
        public string? RequestedByName { get; init; }
        public DateTime? RequestedAt { get; init; }
        public DateTime? AppointmentDateTime { get; init; }
        public DateTime? CompletedAt { get; init; }
        public DateTime? SLADueDate { get; init; }
        public decimal? FacilityLimit { get; init; }
        public string? ApprovedByCommittee { get; init; }
        public decimal? AppraisedValue { get; init; }
        public decimal? ForcedSaleValue { get; init; }
        public decimal? InsuranceValue { get; init; }
        public DateTime? ValuationDate { get; init; }
    }

    // POSITIONAL record: Dapper binds these by the SELECT's column order, not by name. Append
    // new fields at the end and add the column in the same position in the SELECT.
    private sealed record AssetRow(
        Guid Id, int SequenceNumber, string? PropertyType,
        string? Title, string? Area, string? Location,
        string? BuildingType, decimal? NumberOfFloors,
        decimal? AreaRai, decimal? AreaNgan, decimal? AreaSquareWa,
        string? MachineName, string? BuildingTypeOther,
        decimal? CondoFloors, string? CondoFloorNumber);

    private sealed record DocumentRow(
        Guid DocumentId, string TypeCode, string? TypeName, string? TypeNameTh,
        string? FileName, long? FileSizeBytes, DateTime? UploadedAt);
}
