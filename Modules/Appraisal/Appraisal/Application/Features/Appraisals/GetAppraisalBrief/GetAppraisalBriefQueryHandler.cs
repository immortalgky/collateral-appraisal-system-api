using System.Text.Json;
using Appraisal.Application.Features.Shared;
using Appraisal.Domain.Appraisals.Exceptions;
using Dapper;
using Shared.CQRS;
using Shared.DDD;
using Shared.Data;
using Shared.Extensions;

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
                                   -- AssetRow binds by column NAME. The group/sequence columns the ORDER BY
                                   -- uses are not selected: nothing reads them.
                                   COALESCE(ld.SubDistrict, cd.SubDistrict)                 AS Location,
                                   bd.BuildingType,
                                   bd.NumberOfFloors,
                                   -- Land area as raw components, not the formatted string above:
                                   -- the client totals a group's parcels, and rai-ngan-wa carry
                                   -- across at 4 ngan and 100 wa, so it cannot add display text.
                                   la.AreaRai, la.AreaNgan, la.AreaSquareWa,
                                   -- A machine has no title, area or location, so its own name is
                                   -- the only thing that identifies it on the summary row. PropertyName
                                   -- first, then the legacy MachineName: the order the report uses.
                                   COALESCE(NULLIF(LTRIM(RTRIM(md.PropertyName)), N''),
                                            NULLIF(LTRIM(RTRIM(md.MachineName)), N''))
                                                                                            AS MachineName,
                                   -- The free text behind BuildingType '99' (อื่นๆ), raw: the display
                                   -- rule (CodeOrOtherDisplay) needs the value the report's COALESCE sees.
                                   bd.BuildingTypeOther,
                                   -- Condo: the tower's storey count and the floor the unit is
                                   -- on. FloorNumber is nvarchar and carries junk on the dev
                                   -- database ('-' on rows where nobody filled it in), so it is
                                   -- passed through as text and parsed by the caller rather than
                                   -- coerced here, where a failure would be silent.
                                   cd.NumberOfFloors                                        AS CondoFloors,
                                   NULLIF(cd.FloorNumber, N'')                               AS CondoFloorNumber,
                                   -- Descriptive detail for the 3D scene. The five list
                                   -- columns are JSON nvarchar and come back as raw text: they are
                                   -- parsed in C#, where a bad value can be skipped instead of
                                   -- aborting the whole query.
                                   -- The code columns hold '' (not NULL) wherever the form was saved
                                   -- without a choice, so blanks are folded to NULL: "no code" is
                                   -- one thing on the wire, and COALESCE below must fall through
                                   -- to the condo's value rather than stop at an empty land one.
                                   NULLIF(ld.LandShapeType, N'')                            AS LandShapeType,
                                   ld.RoadFrontage,
                                   ld.SoilLevel,
                                   -- Land, else the condo's own record of the land it stands on — the
                                   -- same fall-through as the road and the legal flags below.
                                   COALESCE(NULLIF(ld.LandFillType, N''), NULLIF(cd.LandFillType, N''))
                                                                                            AS LandFillType,
                                   COALESCE(NULLIF(ld.LandUseType, N''), cd.LandUseType)    AS LandUseType,
                                   ld.PondArea,
                                   ld.PondDepth,
                                   -- True means FAR from electricity; renamed on the way out.
                                   ld.HasElectricity,
                                   ld.ElectricityDistance,
                                   COALESCE(ld.AccessRoadWidth, cd.AccessRoadWidth)         AS AccessRoadWidth,
                                   COALESCE(NULLIF(ld.RoadSurfaceType, N''), NULLIF(cd.RoadSurfaceType, N''))
                                                                                            AS RoadSurfaceType,
                                   ld.NorthAdjacentArea, ld.NorthBoundaryLength,
                                   ld.SouthAdjacentArea, ld.SouthBoundaryLength,
                                   ld.EastAdjacentArea,  ld.EastBoundaryLength,
                                   ld.WestAdjacentArea,  ld.WestBoundaryLength,
                                   -- A condo unit records the three legal flags it has (expropriated,
                                   -- in an expropriation line, forest boundary) on its own detail, so
                                   -- they fall through from land to condo like the road columns.
                                   -- Landlocked and encroached exist on land only.
                                   ld.IsLandlocked,
                                   COALESCE(ld.IsExpropriated, cd.IsExpropriated)           AS IsExpropriated,
                                   COALESCE(ld.IsInExpropriationLine, cd.IsInExpropriationLine)
                                                                                            AS IsInExpropriationLine,
                                   ld.IsEncroached,
                                   COALESCE(ld.IsForestBoundary, cd.IsForestBoundary)       AS IsForestBoundary,
                                   COALESCE(bd.TotalBuildingArea, NULLIF(cd.TotalBuildingArea, 0)) AS TotalBuildingArea,
                                   COALESCE(NULLIF(bd.RoofType, N''), cd.RoofType)          AS RoofTypeJson,
                                   bd.FenceType                                             AS FenceTypeJson,
                                   COALESCE(bd.IsUnderConstruction, cd.IsUnderConstruction) AS IsUnderConstruction,
                                   cd.UsableArea                                            AS CondoUsableArea,
                                   cd.FacilityType                                          AS FacilityTypeJson,
                                   cd.LocationViewType                                      AS LocationViewTypeJson,
                                   -- Raw fields for the appraisal summary's property description,
                                   -- Location as NAMES: the
                                   -- stored column is a geocode, resolved through the Title masters the
                                   -- summary report's title clause uses. NULL when a code has no master
                                   -- row — a code is never sent as a name; the client resolves `Location`.
                                   tsub.NameTh                                              AS SubDistrictName,
                                   tdist.NameTh                                             AS DistrictName,
                                   tprov.NameTh                                             AS ProvinceName,
                                   bd.PropertyName                                          AS BuildingPropertyName,
                                   -- A property is a building or a condo, never both.
                                   COALESCE(bd.BuildingConditionType, cd.BuildingConditionType)
                                                                                            AS ConditionCode,
                                   COALESCE(bd.BuildingConditionTypeOther, cd.BuildingConditionTypeOther)
                                                                                            AS ConditionOther,
                                   COALESCE(bd.BuildingAge, cd.BuildingAge)                 AS BuildingAge,
                                   NULLIF(LTRIM(RTRIM(cd.RoomNumber)), N'')                 AS RoomNumber,
                                   cd.BuildingNumber,
                                   NULLIF(LTRIM(RTRIM(cd.CondoName)), N'')                  AS CondoName,
                                   NULLIF(LTRIM(RTRIM(cd.CondoRegistrationNumber)), N'')    AS CondoRegistrationNumber,
                                   -- The condo deed number moved to TitleNumber; BuiltOnTitleNumber
                                   -- is the pre-rename fallback (as in the summary report).
                                   COALESCE(NULLIF(LTRIM(RTRIM(cd.TitleNumber)), N''),
                                            NULLIF(LTRIM(RTRIM(cd.BuiltOnTitleNumber)), N''))
                                                                                            AS BuiltOnTitleNumber,
                                   cd.GovernmentPricePerSqm,
                                   cd.IsMissingFromSurvey,
                                   -- The free text behind code '99' (อื่นๆ) of each multi-select, so the
                                   -- client can show what the appraiser typed instead of "other".
                                   COALESCE(NULLIF(LTRIM(RTRIM(ld.LandUseTypeOther)), N''),
                                            NULLIF(LTRIM(RTRIM(cd.LandUseTypeOther)), N''))  AS LandUseTypeOther,
                                   COALESCE(NULLIF(LTRIM(RTRIM(bd.RoofTypeOther)), N''),
                                            NULLIF(LTRIM(RTRIM(cd.RoofTypeOther)), N''))     AS RoofTypeOther,
                                   NULLIF(LTRIM(RTRIM(bd.FenceTypeOther)), N'')             AS FenceTypeOther,
                                   NULLIF(LTRIM(RTRIM(cd.FacilityTypeOther)), N'')          AS FacilityTypeOther,
                                   NULLIF(LTRIM(RTRIM(cd.LocationViewTypeOther)), N'')      AS LocationViewTypeOther,
                                   COALESCE(NULLIF(LTRIM(RTRIM(ld.RoadSurfaceTypeOther)), N''),
                                            NULLIF(LTRIM(RTRIM(cd.RoadSurfaceTypeOther)), N''))
                                                                                            AS RoadSurfaceTypeOther,
                                   COALESCE(NULLIF(LTRIM(RTRIM(ld.LandFillTypeOther)), N''),
                                            NULLIF(LTRIM(RTRIM(cd.LandFillTypeOther)), N''))  AS LandFillTypeOther,
                                   NULLIF(LTRIM(RTRIM(ld.LandShapeTypeOther)), N'')         AS LandShapeTypeOther,
                                   -- The property's location through the DOPA masters — what the report's
                                   -- ที่ตั้งทรัพย์สิน shows, and NOT the deed address above. NULL when a
                                   -- code has no master row.
                                   dsub.NameTh                                              AS DopaSubDistrictName,
                                   ddist.NameTh                                             AS DopaDistrictName,
                                   dprov.NameTh                                             AS DopaProvinceName
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
                                    -- Land and condo both store their location against the Title
                                    -- masters. At most one of ld / cd exists, so the COALESCE picks it.
                                    LEFT JOIN parameter.TitleSubDistricts tsub
                                              ON tsub.Code = COALESCE(ld.SubDistrict, cd.SubDistrict)
                                    LEFT JOIN parameter.TitleDistricts tdist
                                              ON tdist.Code = COALESCE(ld.District, cd.District)
                                    LEFT JOIN parameter.TitleProvinces tprov
                                              ON tprov.Code = COALESCE(ld.Province, cd.Province)
                                    LEFT JOIN parameter.DopaSubDistricts dsub
                                              ON dsub.Code = COALESCE(ld.DopaSubDistrict, cd.DopaSubDistrict)
                                    LEFT JOIN parameter.DopaDistricts ddist
                                              ON ddist.Code = COALESCE(ld.DopaDistrict, cd.DopaDistrict)
                                    LEFT JOIN parameter.DopaProvinces dprov
                                              ON dprov.Code = COALESCE(ld.DopaProvince, cd.DopaProvince)
                                    OUTER APPLY (SELECT TOP 1 t.TitleNumber
                                                 FROM appraisal.LandTitles t
                                                 WHERE t.LandAppraisalDetailId = ld.Id
                                                 ORDER BY t.SequenceNumber, t.Id) lt
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
                                    WHERE pu.ProjectId = p.Id)                    AS UnitStoreys,
                                   -- The rest of the project's own columns, for the summary's project
                                   -- description. Facilities and Utilities are JSON arrays of codes,
                                   -- parsed in C#.
                                   p.IsUnderConstruction, p.ConstructionProgressPercent, p.Facilities,
                                   NULLIF(LTRIM(RTRIM(p.HouseNumber)), N'')       AS HouseNumber,
                                   NULLIF(LTRIM(RTRIM(p.Soi)), N'')               AS Soi,
                                   NULLIF(LTRIM(RTRIM(p.Road)), N'')              AS Road,
                                   -- Location as NAMES from both the DOPA and the Title masters. The
                                   -- project picker is DOPA, but projects saved before it switched
                                   -- (2026-09-01) hold Title codes; which master a row uses is decided in
                                   -- C# (PickLocationNames) so the rule is testable. A code that resolves
                                   -- nowhere stays NULL: a code is never sent as a name.
                                   dsub.NameTh  AS DopaSubDistrictName,
                                   ddist.NameTh AS DopaDistrictName,
                                   dprov.NameTh AS DopaProvinceName,
                                   tsub.NameTh  AS TitleSubDistrictName,
                                   tdist.NameTh AS TitleDistrictName,
                                   tprov.NameTh AS TitleProvinceName,
                                   p.Latitude, p.Longitude,
                                   NULLIF(LTRIM(RTRIM(p.ProjectSaleLaunchDate)), N'') AS ProjectSaleLaunchDate,
                                   p.Utilities,
                                   NULLIF(LTRIM(RTRIM(p.UtilitiesOther)), N'')    AS UtilitiesOther,
                                   NULLIF(LTRIM(RTRIM(p.FacilitiesOther)), N'')   AS FacilitiesOther
                           FROM appraisal.Projects p
                                    LEFT JOIN parameter.DopaSubDistricts  dsub  ON dsub.Code  = p.SubDistrict
                                    LEFT JOIN parameter.DopaDistricts     ddist ON ddist.Code = p.District
                                    LEFT JOIN parameter.DopaProvinces     dprov ON dprov.Code = p.Province
                                    LEFT JOIN parameter.TitleSubDistricts tsub  ON tsub.Code  = p.SubDistrict
                                    LEFT JOIN parameter.TitleDistricts    tdist ON tdist.Code = p.District
                                    LEFT JOIN parameter.TitleProvinces    tprov ON tprov.Code = p.Province
                           WHERE p.AppraisalId = @AppraisalId
                           ORDER BY p.Id;

                           -- Depreciation rows of every building on this appraisal, ONE query for all
                           -- of them (grouped by property in C#) rather than one per asset. Both
                           -- IsBuilding values come back: the improvements list wants the 0s, and the
                           -- development-only test needs to see that no 1 exists.
                           --
                           -- The table has no order column, so entry order cannot be recovered. The
                           -- order is the summary report's (RS18): building rows before ส่วนพัฒนา rows,
                           -- then Id — so the improvements read in the same sequence as in the book.
                           SELECT  bad.AppraisalPropertyId, bdd.IsBuilding, bdd.AreaDescription, bdd.Area
                           FROM appraisal.BuildingDepreciationDetails bdd
                                    JOIN appraisal.BuildingAppraisalDetails bad
                                         ON bad.Id = bdd.BuildingAppraisalDetailId
                                    JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
                           WHERE ap.AppraisalId = @AppraisalId
                           ORDER BY bad.AppraisalPropertyId, bdd.IsBuilding DESC, bdd.Id;

                           -- Towers of a condo project, for the project scene. Empty for a horizontal
                           -- project and for a non-block appraisal (no Projects row to join).
                           SELECT  t.Id AS TowerId, t.TowerName, t.NumberOfFloors, t.NumberOfUnits
                           FROM appraisal.ProjectTowers t
                                    JOIN appraisal.Projects p ON p.Id = t.ProjectId
                           WHERE p.AppraisalId = @AppraisalId
                           ORDER BY t.TowerName, t.Id;

                           -- Units per tower and floor. ALL of a project's units count, whatever the
                           -- upload batch: Project.AddUnits appends batches rather than replacing
                           -- them, and the UnitCount subquery above counts them all the same way.
                           -- A unit with no ProjectTowerId is kept (ProjectTowerId comes back NULL):
                           -- it is counted into UnassignedUnits, so towers + unassigned still add up
                           -- to UnitCount instead of silently dropping them.
                           SELECT  pu.ProjectTowerId, pu.Floor,
                                   COUNT(*)                                  AS Units,
                                   SUM(CASE WHEN pu.IsSold = 1 THEN 1 ELSE 0 END) AS SoldUnits
                           FROM appraisal.ProjectUnits pu
                                    JOIN appraisal.Projects p ON p.Id = pu.ProjectId
                           WHERE p.AppraisalId = @AppraisalId
                           GROUP BY pu.ProjectTowerId, pu.Floor
                           ORDER BY pu.ProjectTowerId, pu.Floor;

                           -- The project's land (ProjectLands is 1:1 with Projects). Code columns hold
                           -- '' where the form was saved without a choice, so blanks become NULL.
                           SELECT  NULLIF(l.LandShapeType, N'')                      AS LandShapeType,
                                   l.RoadFrontage,
                                   l.AccessRoadWidth,
                                   NULLIF(l.RoadSurfaceType, N'')                    AS RoadSurfaceType,
                                   l.LandUseType                                     AS LandUseTypeJson,
                                   l.IsLandlocked, l.IsExpropriated, l.IsInExpropriationLine,
                                   l.IsEncroached, l.IsForestBoundary,
                                   NULLIF(LTRIM(RTRIM(l.LandUseTypeOther)), N'')     AS LandUseTypeOther,
                                   NULLIF(LTRIM(RTRIM(l.RoadSurfaceTypeOther)), N'') AS RoadSurfaceTypeOther
                           FROM appraisal.ProjectLands l
                                    JOIN appraisal.Projects p ON p.Id = l.ProjectId
                           WHERE p.AppraisalId = @AppraisalId;

                           -- House models of a HORIZONTAL project. Condo projects keep their models per
                           -- tower and have no use for them here, hence the ProjectType filter. Units
                           -- per model come from ProjectUnits.ProjectModelId; OUTER APPLY keeps a model
                           -- nobody has bought a plot of (0 units) in the list.
                           -- Horizontal = 'LB' or 'L', the same pair ProjectType.IsLandAndBuildingLike
                           -- treats alike. A land-only 'L' project may have plots and no models.
                           SELECT  m.Id,
                                   NULLIF(m.ModelName, N'')                          AS ModelName,
                                   NULLIF(m.BuildingType, N'')                       AS BuildingType,
                                   NULLIF(m.BuildingTypeOther, N'')                  AS BuildingTypeOther,
                                   m.NumberOfFloors,
                                   m.StandardLandArea,
                                   m.StandardUsableArea,
                                   m.RoofType                                        AS RoofTypeJson,
                                   m.FenceType                                       AS FenceTypeJson,
                                   COALESCE(uc.Units, 0)                             AS Units,
                                   COALESCE(uc.SoldUnits, 0)                         AS SoldUnits,
                                   -- Appended for the summary (positional row).
                                   m.NumberOfHouse,
                                   NULLIF(LTRIM(RTRIM(m.RoofTypeOther)), N'')        AS RoofTypeOther,
                                   NULLIF(LTRIM(RTRIM(m.FenceTypeOther)), N'')       AS FenceTypeOther,
                                   m.UsableAreaMin
                           FROM appraisal.Projects p
                                    JOIN appraisal.ProjectModels m ON m.ProjectId = p.Id
                                    OUTER APPLY (SELECT COUNT(*)                                       AS Units,
                                                        SUM(CASE WHEN pu.IsSold = 1 THEN 1 ELSE 0 END) AS SoldUnits
                                                 FROM appraisal.ProjectUnits pu
                                                 WHERE pu.ProjectModelId = m.Id) uc
                           WHERE p.AppraisalId = @AppraisalId
                             AND p.ProjectType IN ('LB', 'L')
                           ORDER BY m.ModelName, m.Id;

                           -- The plots of a HORIZONTAL project, capped: a scene cannot usefully draw
                           -- thousands of houses, and the payload should not carry them. The client
                           -- reads how many there are in total from UnitCount.
                           --
                           -- Upload order is the batch's UploadedAt, THEN SequenceNumber: the Excel
                           -- parser numbers every batch from 1 and Project.AddUnits appends batches
                           -- without renumbering, so SequenceNumber alone interleaves them. LEFT JOIN
                           -- because a few units carry a batch id with no upload row (5 on the dev
                           -- database); they sort first. Id only makes the cut deterministic.
                           SELECT TOP (500)
                                   NULLIF(pu.PlotNumber, N'')                        AS PlotNumber,
                                   NULLIF(pu.HouseNumber, N'')                       AS HouseNumber,
                                   pu.ProjectModelId                                 AS ModelId,
                                   NULLIF(pu.ModelType, N'')                         AS ModelType,
                                   pu.NumberOfFloors,
                                   pu.LandArea,
                                   pu.UsableArea,
                                   pu.IsSold
                           FROM appraisal.Projects p
                                    JOIN appraisal.ProjectUnits pu ON pu.ProjectId = p.Id
                                    LEFT JOIN appraisal.ProjectUnitUploads up ON up.Id = pu.UploadBatchId
                           WHERE p.AppraisalId = @AppraisalId
                             AND p.ProjectType IN ('LB', 'L')
                           ORDER BY up.UploadedAt, pu.SequenceNumber, pu.Id;

                           -- Land titles of every land property, ONE query for all of them (grouped by
                           -- property in C#). The government rate is the only price on this screen's
                           -- descriptive side and is a published rate, not an appraisal. Text columns
                           -- are trimmed and blank becomes NULL: the report skips blanks.
                           SELECT  ap.Id                                              AS AppraisalPropertyId,
                                   NULLIF(LTRIM(RTRIM(lt.TitleType)), N'')            AS TitleType,
                                   NULLIF(LTRIM(RTRIM(lt.TitleNumber)), N'')          AS TitleNumber,
                                   NULLIF(LTRIM(RTRIM(lt.BookNumber)), N'')           AS BookNumber,
                                   NULLIF(LTRIM(RTRIM(lt.PageNumber)), N'')           AS PageNumber,
                                   NULLIF(LTRIM(RTRIM(lt.LandParcelNumber)), N'')     AS LandParcelNumber,
                                   NULLIF(LTRIM(RTRIM(lt.SurveyNumber)), N'')         AS SurveyNumber,
                                   NULLIF(LTRIM(RTRIM(lt.Rawang)), N'')               AS Rawang,
                                   lt.AreaRai, lt.AreaNgan, lt.AreaSquareWa,
                                   lt.GovernmentPricePerSqWa,
                                   ISNULL(lt.IsMissingFromSurvey, 0)                  AS IsMissingFromSurvey
                           FROM appraisal.LandTitles lt
                                    JOIN appraisal.LandAppraisalDetails ld ON ld.Id = lt.LandAppraisalDetailId
                                    JOIN appraisal.AppraisalProperties ap ON ap.Id = ld.AppraisalPropertyId
                           WHERE ap.AppraisalId = @AppraisalId
                           ORDER BY ap.Id, lt.SequenceNumber, lt.Id;

                           -- Area components of every condo unit. Same order as the summary report: the
                           -- appraiser's own Sequence (NULL on old rows, which sort first), then the
                           -- Guid's text form to break ties by creation time.
                           SELECT  ap.Id                                              AS AppraisalPropertyId,
                                   NULLIF(LTRIM(RTRIM(cada.AreaDescription)), N'')    AS AreaDescription,
                                   cada.AreaSize
                           FROM appraisal.CondoAppraisalAreaDetails cada
                                    JOIN appraisal.CondoAppraisalDetails cad ON cad.Id = cada.CondoAppraisalDetailsId
                                    JOIN appraisal.AppraisalProperties ap ON ap.Id = cad.AppraisalPropertyId
                           WHERE ap.AppraisalId = @AppraisalId
                             AND cada.AreaSize > 0
                           ORDER BY ap.Id, cada.Sequence, CONVERT(char(36), cada.Id);

                           -- Machines, with the group so the section order can follow the report (the
                           -- report prints one set of sections per property group). Sections and the
                           -- final order are worked out in C# (BuildMachines). The name is PropertyName
                           -- first, as the report: the form now writes only PropertyName and rows
                           -- from before carry the same text in MachineName.
                           SELECT  ap.Id                                              AS AssetId,
                                   pg.GroupNumber,
                                   COALESCE(NULLIF(LTRIM(RTRIM(md.PropertyName)), N''),
                                            NULLIF(LTRIM(RTRIM(md.MachineName)), N''))  AS MachineName,
                                   NULLIF(LTRIM(RTRIM(md.Brand)), N'')                AS Brand,
                                   NULLIF(LTRIM(RTRIM(md.Model)), N'')                AS Model,
                                   NULLIF(LTRIM(RTRIM(md.RegistrationNumber)), N'')   AS RegistrationNumber,
                                   NULLIF(LTRIM(RTRIM(md.SerialNo)), N'')             AS SerialNo,
                                   md.YearOfManufacture,
                                   NULLIF(LTRIM(RTRIM(md.MachineCondition)), N'')     AS MachineCondition,
                                   NULLIF(md.ConditionUse, N'')                       AS ConditionUse,
                                   md.IsPriceCertified,
                                   md.RegistrationStatus,
                                   NULLIF(md.InstallationStatus, N'')                 AS InstallationStatus
                           FROM appraisal.AppraisalProperties ap
                                    JOIN appraisal.MachineryAppraisalDetails md ON md.AppraisalPropertyId = ap.Id
                                    LEFT JOIN appraisal.PropertyGroupItems pgi ON pgi.AppraisalPropertyId = ap.Id
                                    LEFT JOIN appraisal.PropertyGroups pg ON pg.Id = pgi.PropertyGroupId
                           WHERE ap.AppraisalId = @AppraisalId
                           ORDER BY CASE WHEN pg.GroupNumber IS NULL THEN 1 ELSE 0 END,
                                    pg.GroupNumber, pgi.SequenceInGroup, ap.SequenceNumber, ap.Id;

                           -- Construction progress, one row per property that has an inspection. Same
                           -- arithmetic as the summary report (RS24): full-detail sums the work details'
                           -- already-weighted CurrentProportionPct, summary mode reads
                           -- SummaryCurrentProgressPct.
                           -- KEEP IN SYNC with ConstructionInspection.OverallCurrentProgressPercent
                           -- (Appraisal.Domain) and the report's RS24 in
                           -- AppraisalSummaryLandBuildingDataProvider: three copies of one formula.
                           -- Per PROPERTY on purpose: vw_AppraisalConstructionProgress is per appraisal
                           -- (a weighted blend across inspections, clamped and rounded to 2 places), so
                           -- it cannot give each property its own figure. The inspection's TotalValue /
                           -- CurrentValue are money and deliberately not selected.
                           SELECT  ci.AppraisalPropertyId,
                                   CASE WHEN ci.IsFullDetail = 1
                                        THEN ISNULL(wd.CurrentProportionPctSum, 0)
                                        ELSE ISNULL(ci.SummaryCurrentProgressPct, 0) END AS ProgressPct
                           FROM appraisal.ConstructionInspections ci
                                    JOIN appraisal.AppraisalProperties ap ON ap.Id = ci.AppraisalPropertyId
                                    OUTER APPLY (SELECT SUM(d.CurrentProportionPct) AS CurrentProportionPctSum
                                                 FROM appraisal.ConstructionWorkDetails d
                                                 WHERE d.ConstructionInspectionId = ci.Id) wd
                           WHERE ap.AppraisalId = @AppraisalId;

                           -- Thai labels for every code list the brief shows as text: building types and
                           -- conditions (assets, house models) and a project's utilities and facilities.
                           -- One small master read mapped in C# (LabelMap), rather than a lookup per
                           -- column: which of code / remark / label wins is decided there
                           -- (CodeOrOtherDisplay), so the code-99 rule is testable. Ordered so a
                           -- duplicate row (the master is unique per Country) resolves the same way every time.
                           SELECT  p.[Group] AS [Group], p.Code, p.Description
                           FROM parameter.Parameters p
                           WHERE p.[Group] IN (N'BuildingType', N'BuildingCondition', N'PublicUtility', N'Facilities')
                             AND p.[Language] = N'TH' AND p.IsActive = 1
                           ORDER BY p.[Group], p.Code, p.ParId;
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
        var project = await grid.ReadFirstOrDefaultAsync<ProjectRow>();
        var depreciationRows = (await grid.ReadAsync<DepreciationRow>()).ToList();
        var towerRows = (await grid.ReadAsync<TowerRow>()).ToList();
        var towerFloorRows = (await grid.ReadAsync<TowerFloorRow>()).ToList();
        var unassigned = SumUnassigned(towerRows, towerFloorRows);
        var projectLand = await grid.ReadFirstOrDefaultAsync<ProjectLandRow>();
        var modelRows = (await grid.ReadAsync<HouseModelRow>()).ToList();
        var plotRows = (await grid.ReadAsync<PlotRow>()).ToList();
        var titleRows = (await grid.ReadAsync<LandTitleRow>()).ToList();
        var condoAreaRows = (await grid.ReadAsync<CondoAreaRow>()).ToList();
        var machineRows = (await grid.ReadAsync<MachineRow>()).ToList();
        var progressRows = (await grid.ReadAsync<ProgressRow>()).ToList();
        var labelRows = (await grid.ReadAsync<ParamLabelRow>()).ToList();

        var facilityCodes = ParseCodes(project?.Facilities);
        var location = project is null
            ? default
            : PickLocationNames(
                (project.DopaSubDistrictName, project.DopaDistrictName, project.DopaProvinceName),
                (project.TitleSubDistrictName, project.TitleDistrictName, project.TitleProvinceName));

        var titlesByProperty = titleRows.ToLookup(t => t.AppraisalPropertyId);
        var condoAreasByProperty = condoAreaRows.ToLookup(c => c.AppraisalPropertyId);
        // One inspection per property: ConstructionInspections is unique on AppraisalPropertyId.
        var progressByProperty = progressRows.ToDictionary(r => r.AppraisalPropertyId, r => r.ProgressPct);
        var buildingTypeLabels = LabelMap(labelRows, "BuildingType");
        var conditionLabels = LabelMap(labelRows, "BuildingCondition");
        var utilityLabels = LabelMap(labelRows, "PublicUtility");
        var facilityLabels = LabelMap(labelRows, "Facilities");

        var depreciationByProperty = depreciationRows.ToLookup(d => d.AppraisalPropertyId);

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
            Project = project is null
                ? null
                : new BriefProject(
                    project.ProjectName, project.ProjectType, project.Developer, project.UnitForSaleCount,
                    project.LandAreaRai, project.LandAreaNgan, project.LandAreaSquareWa, project.SubDistrict,
                    project.NumberOfPhase, project.TowerCount, project.MaxFloor, project.UnitCount,
                    project.UnitStoreys)
                {
                    Towers = BuildTowers(towerRows, towerFloorRows),
                    UnassignedUnits = unassigned.Units,
                    UnassignedSoldUnits = unassigned.SoldUnits,
                    IsUnderConstruction = project.IsUnderConstruction,
                    ConstructionProgressPercent = project.IsUnderConstruction == true
                        ? ProgressPercent(project.ConstructionProgressPercent)
                        : null,
                    Facilities = facilityCodes,
                    // Dash placeholders ("-") are what the form saves for "none": the report drops them.
                    HouseNumber = PlaceholderText.Stated(project.HouseNumber),
                    Soi = PlaceholderText.Stated(project.Soi),
                    Road = PlaceholderText.Stated(project.Road),
                    SubDistrictName = location.Sub,
                    DistrictName = location.District,
                    ProvinceName = location.Province,
                    Latitude = project.Latitude,
                    Longitude = project.Longitude,
                    ProjectSaleLaunchDate = project.ProjectSaleLaunchDate,
                    Utilities = ToLabels(ParseCodes(project.Utilities), utilityLabels),
                    UtilitiesOther = project.UtilitiesOther,
                    FacilityLabels = ToLabels(facilityCodes, facilityLabels),
                    FacilitiesOther = project.FacilitiesOther,
                    Land = projectLand?.ToLand(),
                    HouseModels = modelRows.Select(m => m.ToModel(buildingTypeLabels)).ToList(),
                    Plots = plotRows.Select(p => p.ToPlot()).ToList()
                },

            // GovernmentPricePerSqWa / GovernmentPricePerSqm are sent whether or not the price is
            // released: DELIBERATE, a user decision (2026-10-19). They are the Treasury's public
            // ราคาประเมินราชการ, not the appraisal outcome, and the user asked for them on the land and
            // condo in the brief. Do not gate them on `released`.
            //
            // The item list is shown while locked, DESCRIPTIVE FIELDS INCLUDED, by product decision
            // (2026-10-18). That covers what the appraiser records on the property — legal flags
            // (landlocked, expropriated, encroached, forest), boundaries, improvements, pond,
            // roof/fence, under construction — not only what the credit officer entered on the
            // request. The release rule withholds the PRICE and the DOCUMENTS and nothing else; do
            // not gate these fields on `released`.
            Assets = assets.Select(a =>
            {
                var depRows = depreciationByProperty[a.Id];
                return new BriefAsset(
                    Id: a.Id,
                    SequenceNumber: a.SequenceNumber,
                    PropertyType: a.PropertyType,
                    Title: a.Title,
                    Area: a.Area,
                    Location: a.Location,
                    BuildingType: a.BuildingType,
                    NumberOfFloors: PositiveOrNull(a.NumberOfFloors),
                    AreaRai: a.AreaRai,
                    AreaNgan: a.AreaNgan,
                    AreaSquareWa: a.AreaSquareWa,
                    MachineName: a.MachineName,
                    BuildingTypeOther: TrimmedOrNull(a.BuildingTypeOther),
                    CondoFloors: PositiveOrNull(a.CondoFloors),
                    // The report prints a condo floor only when it is stated (not blank, dashes or 0).
                    CondoFloorNumber: PlaceholderText.StatedUnlessZero(a.CondoFloorNumber),
                    LandShapeType: a.LandShapeType,
                    RoadFrontage: PositiveOrNull(a.RoadFrontage),
                    SoilLevel: a.SoilLevel,
                    LandFillType: a.LandFillType,
                    LandUseType: ParseCodes(a.LandUseType),
                    PondArea: PositiveOrNull(a.PondArea),
                    PondDepth: PositiveOrNull(a.PondDepth),
                    FarFromElectricity: a.HasElectricity,
                    ElectricityDistance: DistanceIfFar(a.HasElectricity, a.ElectricityDistance),
                    AccessRoadWidth: PositiveOrNull(a.AccessRoadWidth),
                    RoadSurfaceType: a.RoadSurfaceType,
                    Boundaries: BuildBoundaries(a),
                    IsLandlocked: a.IsLandlocked,
                    IsExpropriated: a.IsExpropriated,
                    IsInExpropriationLine: a.IsInExpropriationLine,
                    IsEncroached: a.IsEncroached,
                    IsForestBoundary: a.IsForestBoundary,
                    TotalBuildingArea: a.TotalBuildingArea,
                    RoofType: ParseCodes(a.RoofTypeJson),
                    FenceType: ParseCodes(a.FenceTypeJson),
                    IsUnderConstruction: a.IsUnderConstruction,
                    // The same rule the appraisal summary report uses to move a building under ส่วนพัฒนา.
                    IsDevelopmentOnly: DevelopmentOnlyBuilding.IsDevelopmentOnly(
                        depRows.Select(d => d.IsBuilding)),
                    Improvements: depRows.Where(d => !d.IsBuilding)
                        .Select(d => new BriefImprovement(d.AreaDescription, d.Area)).ToList(),
                    UsableArea: PositiveOrNull(a.CondoUsableArea),
                    FacilityType: ParseCodes(a.FacilityTypeJson),
                    LocationViewType: ParseCodes(a.LocationViewTypeJson))
                {
                    LandTitles = titlesByProperty[a.Id].Select(t => t.ToTitle()).ToList(),
                    DeedSubDistrictName = a.SubDistrictName,
                    DeedDistrictName = a.DistrictName,
                    DeedProvinceName = a.ProvinceName,
                    DopaSubDistrictName = a.DopaSubDistrictName,
                    DopaDistrictName = a.DopaDistrictName,
                    DopaProvinceName = a.DopaProvinceName,
                    BuildingDisplayName = BuildingDisplayName(
                        a.BuildingType, a.BuildingTypeOther, LabelOf(buildingTypeLabels, a.BuildingType),
                        a.BuildingPropertyName),
                    BuildingAge = PositiveOrNull(a.BuildingAge),
                    BuildingCondition = CodeOrOtherDisplay(
                        a.ConditionCode, a.ConditionOther, LabelOf(conditionLabels, a.ConditionCode)),
                    ConstructionProgressPercent = progressByProperty.TryGetValue(a.Id, out var pct)
                        ? ProgressPercent(pct)
                        : null,
                    RoomNumber = a.RoomNumber,
                    // The condo report prints a building number when anything is left after trimming - – — at the ends:
                    // the same narrow rule as a house number, which is not the shared placeholder rule.
                    BuildingNumber = DevelopmentOnlyBuilding.HasHouseNumber(a.BuildingNumber) ? a.BuildingNumber!.Trim() : null,
                    CondoName = a.CondoName,
                    CondoRegistrationNumber = a.CondoRegistrationNumber,
                    BuiltOnTitleNumber = a.BuiltOnTitleNumber,
                    CondoAreas = condoAreasByProperty[a.Id]
                        .Select(c => new BriefCondoArea(c.AreaDescription, c.AreaSize)).ToList(),
                    GovernmentPricePerSqm = a.GovernmentPricePerSqm,
                    IsMissingFromSurvey = a.IsMissingFromSurvey,
                    LandUseTypeOther = a.LandUseTypeOther,
                    RoofTypeOther = a.RoofTypeOther,
                    FenceTypeOther = a.FenceTypeOther,
                    FacilityTypeOther = a.FacilityTypeOther,
                    LocationViewTypeOther = a.LocationViewTypeOther,
                    RoadSurfaceTypeOther = a.RoadSurfaceTypeOther,
                    LandFillTypeOther = a.LandFillTypeOther,
                    LandShapeTypeOther = a.LandShapeTypeOther
                };
            }).ToList(),
            // IsPriceCertified is the valuation outcome: it follows the figures, not the descriptive fields.
            Machines = BuildMachines(machineRows, released),

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

    /// <summary>
    /// The list columns are JSON arrays of codes in nvarchar. Anything unreadable — null, blank,
    /// not JSON, not an array of strings — is an empty list: one malformed row must not take the
    /// whole brief down, and the client treats "no codes" and "unreadable" the same way.
    /// </summary>
    internal static IReadOnlyList<string> ParseCodes(string? json) => json.ParseJsonStringArray();

    /// <summary>
    /// The one rule for a count, age or area: it is a fact only when positive. The form stores 0 for
    /// "not entered", and the report omits 0 everywhere (floors, age, house count, usable area). It is
    /// strict about negatives where the report is not (its floors and age test is <c>!= 0</c>, the
    /// model fields' is <c>&gt; 0</c>): a negative count is a data error, never a fact. No negative
    /// exists on the dev database in any of these columns, so no output differs from the report today.
    /// </summary>
    internal static T? PositiveOrNull<T>(T? value) where T : struct, System.Numerics.INumber<T> =>
        value is { } v && v > T.Zero ? v : null;

    /// <summary>
    /// A code's display text, picked the way the summary report picks it: for code '99' (อื่นๆ) the
    /// free-text remark IS the answer, otherwise the master's label, then the remark, then the raw
    /// code. Null when all of that is blank.
    ///
    /// The report picks the remark with <c>NULLIF(other, '')</c>, and SQL Server compares strings
    /// padded with trailing spaces, so a remark of only spaces ("  " = '') counts as empty there and
    /// the label wins; only a remark with some other character in it (a tab, say) is picked, and
    /// then reads as blank. <see cref="IsEmptyRemark"/> reproduces that comparison. Verified on the
    /// dev database: <c>NULLIF('  ', '')</c> is NULL.
    /// </summary>
    internal static string? CodeOrOtherDisplay(string? code, string? other, string? label)
    {
        var display = code == "99" && !IsEmptyRemark(other) ? other : label ?? other ?? code;
        return string.IsNullOrWhiteSpace(display) ? null : display.Trim();
    }

    /// <summary>
    /// The distance to the power line, only for a parcel marked far from electricity. The column is named
    /// HasElectricity but records "far" (the land form only opens the distance field for a far parcel),
    /// so a distance on a parcel not marked far is stale data. Null when 0 or negative.
    /// </summary>
    internal static decimal? DistanceIfFar(bool? farFromElectricity, decimal? distance) =>
        farFromElectricity == true ? PositiveOrNull(distance) : null;

    /// <summary>Trimmed, or null when nothing is left (a remark of tabs or newlines only).</summary>
    internal static string? TrimmedOrNull(string? value) => value?.Trim() is { Length: > 0 } t ? t : null;

    /// <summary>SQL Server's <c>NULLIF(other, '')</c> test: empty means null, empty or spaces only (padded compare).</summary>
    internal static bool IsEmptyRemark(string? other) => other is null || other.TrimEnd(' ').Length == 0;

    /// <summary>
    /// How a building names itself — its type (<see cref="CodeOrOtherDisplay"/>), with the free-text
    /// property name as the last resort. The report's BuildingDisplayName.
    /// </summary>
    internal static string? BuildingDisplayName(
        string? buildingType, string? buildingTypeOther, string? buildingTypeLabel, string? propertyName)
    {
        var name = CodeOrOtherDisplay(buildingType, buildingTypeOther, buildingTypeLabel) ?? propertyName;
        return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
    }

    /// <summary>
    /// Percent complete as the report prints it: nothing at 100 or more (finished, reads as an
    /// ordinary building), "not started" at 0, otherwise the figure. A negative sum is data error and
    /// reads as not started.
    /// </summary>
    internal static decimal? ProgressPercent(decimal? pct) => pct switch
    {
        null => null,
        >= 100m => null,
        <= 0m => 0m,
        _ => pct
    };

    /// <summary>
    /// KEEP IN SYNC with BuildMachineSections / DescribeMachine in the Reporting module's
    /// AppraisalSummaryMachineDataProvider: the section rule, the order and the "nothing to describe"
    /// test (<see cref="MachineRow.IsDescribable"/>) are copies of the report's.
    ///
    /// Machines in the report's order: per property group, then the three sections the form prints,
    /// then the appraiser's own order. Machines in no group come last — the report does not print
    /// them, but the brief is read before grouping is finished, so they are kept. The rows arrive in
    /// group and sequence order, and OrderBy is stable, so sorting on (group, section) leaves the
    /// sequence intact. A machine with nothing the report would describe is dropped, as the report
    /// drops it.
    /// </summary>
    /// <param name="priceVisible">
    /// Whether this caller may see the valuation outcome. When not, <c>IsPriceCertified</c> is null
    /// and is also left out of the "is there anything to describe" test, so a machine's presence in
    /// the list cannot reveal it either.
    /// </param>
    internal static List<BriefMachine> BuildMachines(IEnumerable<MachineRow> rows, bool priceVisible) => rows
        .Where(m => m.IsDescribable(priceVisible))
        .OrderBy(m => m.GroupNumber ?? int.MaxValue)
        .ThenBy(m => Array.IndexOf(MachineSections, m.Section))
        .Select(m => m.ToMachine(m.Section, priceVisible))
        .ToList();

    internal static readonly string[] MachineSections = ["registered", "notRegistered", "notInstalled"];

    /// <summary>InstallationStatus code for a machine bought from a quotation and not yet installed.</summary>
    private const string UnderProcurementStatus = "2";

    /// <summary>ConditionUse code for "surveyed but not found".</summary>
    private const string NotFoundCondition = "03";

    // ponytail: mirrors TitleDeedLabel in the Reporting module, which Appraisal cannot reference.
    // Add a code in both places; an unknown code falls back to the generic wording.
    private static readonly Dictionary<string, (string Noun, string NumberPrefix)> TitleLabels =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["DEED"] = ("โฉนดที่ดิน", "โฉนดที่ดินเลขที่"),
            ["NS3"] = ("น.ส.3", "น.ส.3 เลขที่"),
            ["NS3K"] = ("น.ส.3 ก.", "น.ส.3 ก. เลขที่"),
            ["NS3KO"] = ("น.ส.3 ข.", "น.ส.3 ข. เลขที่"),
            ["POSR"] = ("ตราจอง", "ตราจองเลขที่")
        };

    internal static (string Noun, string NumberPrefix) TitleLabel(string? titleTypeCode) =>
        titleTypeCode is not null && TitleLabels.TryGetValue(titleTypeCode.Trim(), out var label)
            ? label
            : ("เอกสารสิทธิ์", "เอกสารสิทธิ์เลขที่");

    private static string? LabelOf(IReadOnlyDictionary<string, string?> labels, string? code) =>
        code is not null && labels.TryGetValue(code, out var label) ? label : null;

    private static Dictionary<string, string?> LabelMap(List<ParamLabelRow> rows, string group) => rows
        .Where(r => string.Equals(r.Group, group, StringComparison.OrdinalIgnoreCase))
        .GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First().Description, StringComparer.OrdinalIgnoreCase);

    /// <summary>Codes in a JSON list as labels; a code with no label stays as the code.</summary>
    internal static IReadOnlyList<string> ToLabels(
        IEnumerable<string> codes, IReadOnlyDictionary<string, string?> labels) =>
        codes
            .Select(c => labels.TryGetValue(c, out var d) && !string.IsNullOrWhiteSpace(d) ? d : c)
            .ToList();

    /// <summary>
    /// Only sides that carry something: the form always has four rows, and most are left empty.
    /// A dash-only adjacent area is a placeholder, not text. A distance alone counts only when
    /// positive — the column defaults to 0.00, so on the dev database an untouched side reads as
    /// "0 m to nothing", which says nothing.
    /// </summary>
    private static List<BriefBoundary> BuildBoundaries(AssetRow a) => BuildBoundaries(
    [
        ("north", a.NorthAdjacentArea, a.NorthBoundaryLength),
        ("south", a.SouthAdjacentArea, a.SouthBoundaryLength),
        ("east", a.EastAdjacentArea, a.EastBoundaryLength),
        ("west", a.WestAdjacentArea, a.WestBoundaryLength)
    ]);

    internal static List<BriefBoundary> BuildBoundaries(
        IEnumerable<(string Direction, string? Area, decimal? Distance)> sides)
    {
        return sides
            .Select(s => (s.Direction, Area: PlaceholderText.Stated(s.Area), s.Distance))
            .Where(s => s.Area is not null || s.Distance is > 0)
            .Select(s => new BriefBoundary(s.Direction, s.Area, s.Distance))
            .ToList();
    }

    /// <summary>
    /// A project's location names from ONE master: whichever resolves MORE of the project's three codes,
    /// DOPA on a tie. A legacy project whose codes are Title-only gets all three Title names, and one
    /// with a blank sub-district but a valid district still gets names. The two masters are never mixed
    /// within a row, and names the chosen master cannot resolve stay null — a code is never sent as a name.
    /// </summary>
    internal static (string? Sub, string? District, string? Province) PickLocationNames(
        (string? Sub, string? District, string? Province) dopa,
        (string? Sub, string? District, string? Province) title)
    {
        static int Resolved((string? Sub, string? District, string? Province) n) =>
            (n.Sub is null ? 0 : 1) + (n.District is null ? 0 : 1) + (n.Province is null ? 0 : 1);

        return Resolved(title) > Resolved(dopa) ? title : dopa;
    }

    /// <summary>
    /// The units that belong to no tower, and how many of them are sold: those with no tower id AND
    /// those whose tower id matches no tower (the tower was deleted), so towers + unassigned still add
    /// up to the project's unit count.
    /// </summary>
    internal static (int Units, int SoldUnits) SumUnassigned(
        IReadOnlyCollection<TowerRow> towers, IEnumerable<TowerFloorRow> floors)
    {
        var towerIds = towers.Select(t => t.TowerId).ToHashSet();
        var unassigned = floors.Where(f => f.ProjectTowerId is not { } id || !towerIds.Contains(id)).ToList();
        return (unassigned.Sum(f => f.Units), unassigned.Sum(f => f.SoldUnits));
    }

    /// <summary>
    /// Real towers only. Units with no ProjectTowerId are not a tower and are reported as
    /// <c>UnassignedUnits</c> on the project instead, so towers' units + unassigned = UnitCount.
    /// </summary>
    internal static List<BriefTower> BuildTowers(List<TowerRow> towers, List<TowerFloorRow> floors)
    {
        var floorsByTower = floors.ToLookup(f => f.ProjectTowerId);
        return towers.Select(t =>
        {
            var towerFloors = floorsByTower[t.TowerId]
                .Select(f => new BriefTowerFloor(f.Floor, f.Units, f.SoldUnits)).ToList();
            // NumberOfFloors is sent as recorded, null included (and a stored 0, which means "not
            // recorded", is null too). It is NULL on the dev
            // database, and the highest uploaded unit floor is NOT a substitute: Floor is typed by
            // hand (a stray 1205 exists), so passing it off as the tower's height would present a
            // typo as a recorded fact. The client reads the floors list and applies its own cap.
            // The unit count does fall back to the uploaded units: that is a count of real rows,
            // not an inference.
            return new BriefTower(
                t.TowerName,
                PositiveOrNull(t.NumberOfFloors),
                PositiveOrNull(t.NumberOfUnits) ?? (towerFloors.Count > 0 ? towerFloors.Sum(f => f.Units) : null),
                towerFloors);
        }).ToList();
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

    // Bound BY NAME (like HeaderRow): each property is the SELECT column of the same name. A column with
    // no property is silently ignored, so a renamed alias leaves its property null rather than throwing —
    // keep the two in step. (The smoke test in Tests/Integration runs the batch and asserts a handful of
    // seeded values, so a renamed alias fails there.)
    private sealed class AssetRow
    {
        public Guid Id { get; init; }
        public int SequenceNumber { get; init; }
        public string? PropertyType { get; init; }
        public string? Title { get; init; }
        public string? Area { get; init; }
        public string? Location { get; init; }
        public string? BuildingType { get; init; }
        public decimal? NumberOfFloors { get; init; }
        public decimal? AreaRai { get; init; }
        public decimal? AreaNgan { get; init; }
        public decimal? AreaSquareWa { get; init; }
        public string? MachineName { get; init; }
        public string? BuildingTypeOther { get; init; }
        public decimal? CondoFloors { get; init; }
        public string? CondoFloorNumber { get; init; }
        public string? LandShapeType { get; init; }
        public decimal? RoadFrontage { get; init; }
        public decimal? SoilLevel { get; init; }
        public string? LandFillType { get; init; }
        public string? LandUseType { get; init; }
        public decimal? PondArea { get; init; }
        public decimal? PondDepth { get; init; }
        public bool? HasElectricity { get; init; }
        public decimal? ElectricityDistance { get; init; }
        public decimal? AccessRoadWidth { get; init; }
        public string? RoadSurfaceType { get; init; }
        public string? NorthAdjacentArea { get; init; }
        public decimal? NorthBoundaryLength { get; init; }
        public string? SouthAdjacentArea { get; init; }
        public decimal? SouthBoundaryLength { get; init; }
        public string? EastAdjacentArea { get; init; }
        public decimal? EastBoundaryLength { get; init; }
        public string? WestAdjacentArea { get; init; }
        public decimal? WestBoundaryLength { get; init; }
        public bool? IsLandlocked { get; init; }
        public bool? IsExpropriated { get; init; }
        public bool? IsInExpropriationLine { get; init; }
        public bool? IsEncroached { get; init; }
        public bool? IsForestBoundary { get; init; }
        public decimal? TotalBuildingArea { get; init; }
        public string? RoofTypeJson { get; init; }
        public string? FenceTypeJson { get; init; }
        public bool? IsUnderConstruction { get; init; }
        public decimal? CondoUsableArea { get; init; }
        public string? FacilityTypeJson { get; init; }
        public string? LocationViewTypeJson { get; init; }
        public string? SubDistrictName { get; init; }
        public string? DistrictName { get; init; }
        public string? ProvinceName { get; init; }
        public string? BuildingPropertyName { get; init; }
        public string? ConditionCode { get; init; }
        public string? ConditionOther { get; init; }
        public int? BuildingAge { get; init; }
        public string? RoomNumber { get; init; }
        public string? BuildingNumber { get; init; }
        public string? CondoName { get; init; }
        public string? CondoRegistrationNumber { get; init; }
        public string? BuiltOnTitleNumber { get; init; }
        public decimal? GovernmentPricePerSqm { get; init; }
        public bool? IsMissingFromSurvey { get; init; }
        public string? LandUseTypeOther { get; init; }
        public string? RoofTypeOther { get; init; }
        public string? FenceTypeOther { get; init; }
        public string? FacilityTypeOther { get; init; }
        public string? LocationViewTypeOther { get; init; }
        public string? RoadSurfaceTypeOther { get; init; }
        public string? LandFillTypeOther { get; init; }
        public string? LandShapeTypeOther { get; init; }
        public string? DopaSubDistrictName { get; init; }
        public string? DopaDistrictName { get; init; }
        public string? DopaProvinceName { get; init; }
    }

    internal sealed record LandTitleRow(
        Guid AppraisalPropertyId, string? TitleType, string? TitleNumber, string? BookNumber,
        string? PageNumber, string? LandParcelNumber, string? SurveyNumber, string? Rawang,
        decimal? AreaRai, decimal? AreaNgan, decimal? AreaSquareWa,
        decimal? GovernmentPricePerSqWa, bool IsMissingFromSurvey)
    {
        public BriefLandTitle ToTitle()
        {
            var (noun, prefix) = TitleLabel(TitleType);
            return new BriefLandTitle(
                TitleType, noun, prefix, TitleNumber, BookNumber, PageNumber, LandParcelNumber,
                SurveyNumber, Rawang, AreaRai, AreaNgan, AreaSquareWa, GovernmentPricePerSqWa,
                IsMissingFromSurvey);
        }
    }

    private sealed record CondoAreaRow(Guid AppraisalPropertyId, string? AreaDescription, decimal AreaSize);

    internal sealed record MachineRow(
        Guid AssetId, int? GroupNumber, string? MachineName, string? Brand, string? Model,
        string? RegistrationNumber, string? SerialNo, int? YearOfManufacture, string? MachineCondition,
        string? ConditionUse, bool IsPriceCertified, bool RegistrationStatus, string? InstallationStatus)
    {
        /// <summary>
        /// Decided by installation status ALONE and ahead of registration, so a registered machine
        /// still under procurement is "not installed" rather than "registered".
        /// </summary>
        public string Section => InstallationStatus == UnderProcurementStatus
            ? "notInstalled"
            : RegistrationStatus ? "registered" : "notRegistered";

        /// <summary>
        /// False when the report would print an empty line for it: no name, brand, model,
        /// registration, serial, year or condition, and neither the "not found" nor "price not
        /// certified" remark. The report drops those rather than count a machine it cannot describe.
        /// </summary>
        public bool IsDescribable(bool priceVisible) =>
            !string.IsNullOrWhiteSpace(MachineName) || !string.IsNullOrWhiteSpace(Brand)
            || !string.IsNullOrWhiteSpace(Model) || !string.IsNullOrWhiteSpace(RegistrationNumber)
            || !string.IsNullOrWhiteSpace(SerialNo) || YearOfManufacture.HasValue
            || !string.IsNullOrWhiteSpace(MachineCondition)
            || ConditionUse == NotFoundCondition || (priceVisible && !IsPriceCertified);

        public BriefMachine ToMachine(string section, bool priceVisible) => new(
            AssetId, GroupNumber, section, MachineName, Brand, Model, RegistrationNumber, SerialNo,
            YearOfManufacture, MachineCondition, ConditionUse, priceVisible ? IsPriceCertified : null,
            RegistrationStatus, InstallationStatus);
    }

    private sealed record ProgressRow(Guid AppraisalPropertyId, decimal? ProgressPct);

    private sealed record ParamLabelRow(string Group, string Code, string? Description);

    internal sealed record DepreciationRow(
        Guid AppraisalPropertyId, bool IsBuilding, string? AreaDescription, decimal? Area);

    // Bound BY NAME; see AssetRow. The first thirteen are BriefProject's constructor arguments.
    private sealed class ProjectRow
    {
        public string? ProjectName { get; init; }
        public string? ProjectType { get; init; }
        public string? Developer { get; init; }
        public int? UnitForSaleCount { get; init; }
        public decimal? LandAreaRai { get; init; }
        public decimal? LandAreaNgan { get; init; }
        public decimal? LandAreaSquareWa { get; init; }
        public string? SubDistrict { get; init; }
        public int? NumberOfPhase { get; init; }
        public int TowerCount { get; init; }
        public int? MaxFloor { get; init; }
        public int UnitCount { get; init; }
        public int? UnitStoreys { get; init; }
        public bool? IsUnderConstruction { get; init; }
        public decimal? ConstructionProgressPercent { get; init; }
        public string? Facilities { get; init; }
        public string? HouseNumber { get; init; }
        public string? Soi { get; init; }
        public string? Road { get; init; }
        public string? DopaSubDistrictName { get; init; }
        public string? DopaDistrictName { get; init; }
        public string? DopaProvinceName { get; init; }
        public string? TitleSubDistrictName { get; init; }
        public string? TitleDistrictName { get; init; }
        public string? TitleProvinceName { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
        public string? ProjectSaleLaunchDate { get; init; }
        public string? Utilities { get; init; }
        public string? UtilitiesOther { get; init; }
        public string? FacilitiesOther { get; init; }
    }

    internal sealed record ProjectLandRow(
        string? LandShapeType, decimal? RoadFrontage, decimal? AccessRoadWidth, string? RoadSurfaceType,
        string? LandUseTypeJson,
        bool? IsLandlocked, bool? IsExpropriated, bool? IsInExpropriationLine,
        bool? IsEncroached, bool? IsForestBoundary, string? LandUseTypeOther,
        string? RoadSurfaceTypeOther)
    {
        public BriefProjectLand ToLand() => new(
            LandShapeType, PositiveOrNull(RoadFrontage), PositiveOrNull(AccessRoadWidth), RoadSurfaceType,
            ParseCodes(LandUseTypeJson),
            IsLandlocked, IsExpropriated, IsInExpropriationLine, IsEncroached, IsForestBoundary)
        {
            LandUseTypeOther = LandUseTypeOther,
            RoadSurfaceTypeOther = RoadSurfaceTypeOther
        };
    }

    internal sealed record HouseModelRow(
        Guid Id, string? ModelName, string? BuildingType, string? BuildingTypeOther,
        decimal? NumberOfFloors, decimal? StandardLandArea, decimal? StandardUsableArea,
        string? RoofTypeJson, string? FenceTypeJson, int Units, int SoldUnits,
        int? NumberOfHouse, string? RoofTypeOther, string? FenceTypeOther, decimal? UsableAreaMin)
    {
        public BriefHouseModel ToModel(IReadOnlyDictionary<string, string?> buildingTypeLabels) => new(
            Id, ModelName, BuildingType, TrimmedOrNull(BuildingTypeOther),
            PositiveOrNull(NumberOfFloors), PositiveOrNull(StandardLandArea),
            PositiveOrNull(StandardUsableArea), ParseCodes(RoofTypeJson), ParseCodes(FenceTypeJson), Units,
            SoldUnits)
        {
            NumberOfHouse = PositiveOrNull(NumberOfHouse),
            UsableAreaMin = PositiveOrNull(UsableAreaMin),
            RoofTypeOther = RoofTypeOther,
            FenceTypeOther = FenceTypeOther,
            BuildingTypeDisplay = CodeOrOtherDisplay(
                BuildingType, BuildingTypeOther, LabelOf(buildingTypeLabels, BuildingType))
        };
    }

    // Bound BY NAME (see AssetRow). Plots are not in the report: the scene labels pins with the plot and
    // house numbers, so dash placeholders are dropped, and an unset 0 area or storey count is null.
    private sealed class PlotRow
    {
        public string? PlotNumber { get; init; }
        public string? HouseNumber { get; init; }
        public Guid? ModelId { get; init; }
        public string? ModelType { get; init; }
        public int? NumberOfFloors { get; init; }
        public decimal? LandArea { get; init; }
        public decimal? UsableArea { get; init; }
        public bool IsSold { get; init; }

        public BriefPlot ToPlot() => new(
            PlaceholderText.Stated(PlotNumber), PlaceholderText.Stated(HouseNumber), ModelId, ModelType,
            PositiveOrNull(NumberOfFloors), PositiveOrNull(LandArea), PositiveOrNull(UsableArea), IsSold);
    }

    internal sealed record TowerRow(Guid TowerId, string? TowerName, int? NumberOfFloors, int? NumberOfUnits);

    internal sealed record TowerFloorRow(Guid? ProjectTowerId, int? Floor, int Units, int SoldUnits);

    private sealed record DocumentRow(
        Guid DocumentId, string TypeCode, string? TypeName, string? TypeNameTh,
        string? FileName, long? FileSizeBytes, DateTime? UploadedAt);
}
