CREATE OR ALTER PROCEDURE [workflow].[sp_GetTaskList]
    -- Access scope
    @AssignedType     CHAR(1),               -- '1' = my (direct), '2' = pool (group/team)
    @Assignees        NVARCHAR(MAX),         -- CSV of candidate assignee ids (username/group/group:Team_<guid>)
    @CompanyGate      TINYINT      = 0,       -- 0 = no gate, 1 = (NULL OR = @CallerCompanyId), 2 = (NULL only)
    @CallerCompanyId  UNIQUEIDENTIFIER = NULL,
    -- Filters (all optional; NULL = ignore)
    @Status               NVARCHAR(50)  = NULL,
    @Priority             NVARCHAR(50)  = NULL,
    @TaskName             NVARCHAR(100) = NULL,
    @Search               NVARCHAR(200) = NULL,  -- already LIKE-escaped by caller; matched with ESCAPE '\'
    @AppraisalNumber      NVARCHAR(50)  = NULL,
    @CustomerName         NVARCHAR(200) = NULL,
    @TaskStatus           NVARCHAR(50)  = NULL,
    @TaskType             NVARCHAR(100) = NULL,
    @DateFrom             DATETIME2     = NULL,
    @DateTo               DATETIME2     = NULL,
    @AppointmentDateFrom  DATETIME2     = NULL,
    @AppointmentDateTo    DATETIME2     = NULL,
    @RequestedAtFrom      DATETIME2     = NULL,
    @RequestedAtTo        DATETIME2     = NULL,
    @SlaStatus            NVARCHAR(50)  = NULL,
    @ActivityId           NVARCHAR(100) = NULL,
    -- Sort + paging
    @SortBy       SYSNAME    = NULL,
    @SortDir      VARCHAR(4) = 'DESC',
    @PageNumber   INT        = 0,            -- 0-based
    @PageSize     INT        = 25
AS
BEGIN
    SET NOCOUNT ON;

    ----------------------------------------------------------------------------
    -- Normalise sort: whitelist + translate the C#-side ResolveOrderBy aliases.
    --   ElapsedHours  ASC  ≡ AssignedDate DESC   (least elapsed = most recent)
    --   RemainingHours ASC ≡ DueAt        ASC
    ----------------------------------------------------------------------------
    SET @SortDir = CASE WHEN UPPER(@SortDir) = 'ASC' THEN 'ASC' ELSE 'DESC' END;

    IF @SortBy = 'ElapsedHours'
    BEGIN
        SET @SortBy = 'AssignedDate';
        SET @SortDir = CASE WHEN @SortDir = 'ASC' THEN 'DESC' ELSE 'ASC' END;
    END
    ELSE IF @SortBy = 'RemainingHours'
        SET @SortBy = 'DueAt';
    -- RequestedAt carries the same value as RequestReceivedDate (both = request submit
    -- time), so reuse that sort branch rather than duplicating a column.
    ELSE IF @SortBy = 'RequestedAt'
        SET @SortBy = 'RequestReceivedDate';

    IF @SortBy IS NULL OR @SortBy NOT IN (
        'AppraisalNumber','RequestNumber','CustomerName','TaskType','Purpose','PropertyType',
        'Status','AppointmentDateTime','RequestedBy','RequestReceivedDate','AssignedDate',
        'Movement','InternalFollowupStaff','Appraiser','Priority','DueAt','SlaStatus')
        SET @SortBy = 'AssignedDate';

    DECLARE @Offset INT = @PageNumber * @PageSize;

    CREATE TABLE #page (Id UNIQUEIDENTIFIER PRIMARY KEY, Rn INT, Total INT);

    ----------------------------------------------------------------------------
    -- Does the active filter/sort need any ENRICHED (non-PendingTasks) column?
    -- If not, we can page straight off the base pool slice and skip resolving the
    -- 4 type branches + the display joins entirely (the browse case). Base-only
    -- sort columns: AssignedDate, DueAt, Movement, TaskType, SlaStatus.
    ----------------------------------------------------------------------------
    DECLARE @NeedEnrich BIT = CASE WHEN
           @Status IS NOT NULL OR @Priority IS NOT NULL OR @AppraisalNumber IS NOT NULL
        OR @CustomerName IS NOT NULL OR @Search IS NOT NULL
        OR @AppointmentDateFrom IS NOT NULL OR @AppointmentDateTo IS NOT NULL
        OR @RequestedAtFrom IS NOT NULL OR @RequestedAtTo IS NOT NULL
        OR @SortBy IN ('AppraisalNumber','RequestNumber','CustomerName','Purpose','PropertyType',
                       'Status','RequestedBy','AppointmentDateTime','RequestReceivedDate',
                       'InternalFollowupStaff','Appraiser','Priority')
      THEN 1 ELSE 0 END;

    ----------------------------------------------------------------------------
    -- Within the enriched case, does the active filter/sort need the HEAVY joins
    -- (RequestProperties STRING_AGG / AppraisalAssignments / Appointments / Companies)?
    -- If not, the "light" path joins only Appraisals + Requests and pushes the
    -- customer-name filter down to a one-time IX_RequestCustomer_Name scan + semi-join,
    -- pruning the pool BEFORE enrichment. NOTE the asymmetry: CustomerName-as-FILTER is
    -- light (semi-join), CustomerName-as-SORT needs the value -> heavy.
    ----------------------------------------------------------------------------
    DECLARE @NeedHeavy BIT = CASE WHEN
           @AppointmentDateFrom IS NOT NULL OR @AppointmentDateTo IS NOT NULL
        OR @SortBy IN ('PropertyType','AppointmentDateTime','InternalFollowupStaff','Appraiser','CustomerName')
      THEN 1 ELSE 0 END;

    ----------------------------------------------------------------------------
    -- Materialize matching RequestIds ONCE per text filter (the leading-wildcard
    -- LIKE scans run a single time here, NOT once per branch reference as a CTE would
    -- re-evaluate them). Each branch below filters against these tiny PK'd temps.
    -- The three filters are ANDed per branch; @Search alone = name OR appraisal-number.
    ----------------------------------------------------------------------------
    CREATE TABLE #f_cust   (RequestId UNIQUEIDENTIFIER PRIMARY KEY);  -- @CustomerName matches
    CREATE TABLE #f_appr   (RequestId UNIQUEIDENTIFIER PRIMARY KEY);  -- @AppraisalNumber matches
    CREATE TABLE #f_search (RequestId UNIQUEIDENTIFIER PRIMARY KEY);  -- @Search (name OR number) matches

    -- @CustomerName/@AppraisalNumber/@Search arrive as FULL LIKE patterns already built by the
    -- caller (TaskListFilterBuilder.BuildSearchPattern): default 'term%' (prefix, SEEKS
    -- IX_RequestCustomer_Name), or user-'*' translated to '%' for substring/suffix (scans).
    -- MAXDOP 1 keeps each to one core so concurrent searches queue cleanly (no oversubscription).
    IF @CustomerName IS NOT NULL
        INSERT INTO #f_cust (RequestId)
        SELECT DISTINCT RequestId FROM request.RequestCustomers
        WHERE RequestId IS NOT NULL AND Name LIKE @CustomerName ESCAPE '\'
        OPTION (RECOMPILE, MAXDOP 1);

    IF @AppraisalNumber IS NOT NULL
        INSERT INTO #f_appr (RequestId)
        SELECT DISTINCT RequestId FROM appraisal.Appraisals
        WHERE RequestId IS NOT NULL AND AppraisalNumber LIKE @AppraisalNumber ESCAPE '\'
        OPTION (RECOMPILE, MAXDOP 1);

    IF @Search IS NOT NULL
        INSERT INTO #f_search (RequestId)
        SELECT RequestId FROM request.RequestCustomers
          WHERE RequestId IS NOT NULL AND Name LIKE @Search ESCAPE '\'
        UNION
        SELECT RequestId FROM appraisal.Appraisals
          WHERE RequestId IS NOT NULL AND AppraisalNumber LIKE @Search ESCAPE '\'
        OPTION (RECOMPILE, MAXDOP 1);

    IF @NeedEnrich = 0
    BEGIN
        --------------------------------------------------------------------------
        -- CHEAP BROWSE PATH: no enriched filter, base-only sort. Page directly off
        -- workflow.PendingTasks; stage 2 enriches just the page. Counting/paging
        -- straight off PendingTasks is correct under the SAME invariant as
        -- TaskListFilterBuilder.BaseCountSource: every CorrelationId maps to exactly
        -- one of the 4 owning tables, so pending rows are 1:1 with vw_TaskList rows.
        -- If orphan-tolerance is ever introduced, add the matching
        -- WHERE EXISTS(...4 owning tables...) guard here too.
        --------------------------------------------------------------------------
        INSERT INTO #page (Id, Rn, Total)
        SELECT Id,
               ROW_NUMBER() OVER (ORDER BY
                   CASE WHEN @SortDir = 'ASC' THEN
                       CASE @SortBy
                           WHEN 'TaskType'  THEN TaskName
                           WHEN 'Movement'  THEN Movement
                           WHEN 'SlaStatus' THEN SlaStatus
                       END END ASC,
                   CASE WHEN @SortDir = 'DESC' THEN
                       CASE @SortBy
                           WHEN 'TaskType'  THEN TaskName
                           WHEN 'Movement'  THEN Movement
                           WHEN 'SlaStatus' THEN SlaStatus
                       END END DESC,
                   CASE WHEN @SortDir = 'ASC' THEN
                       CASE @SortBy
                           WHEN 'DueAt'        THEN DueAt
                           WHEN 'AssignedDate' THEN AssignedAt
                       END END ASC,
                   CASE WHEN @SortDir = 'DESC' THEN
                       CASE @SortBy
                           WHEN 'DueAt'        THEN DueAt
                           WHEN 'AssignedDate' THEN AssignedAt
                       END END DESC,
                   AssignedAt DESC, Id DESC) AS Rn,
               COUNT(*) OVER () AS Total
        FROM workflow.PendingTasks
        WHERE AssignedType = @AssignedType
          AND AssignedTo IN (SELECT value FROM STRING_SPLIT(@Assignees, ','))
          AND ( @CompanyGate = 0
                 OR (@CompanyGate = 1 AND (AssigneeCompanyId IS NULL OR AssigneeCompanyId = @CallerCompanyId))
                 OR (@CompanyGate = 2 AND AssigneeCompanyId IS NULL) )
          AND (@TaskName   IS NULL OR TaskName   = @TaskName)
          AND (@TaskType   IS NULL OR TaskName   = @TaskType)
          AND (@ActivityId IS NULL OR ActivityId = @ActivityId)
          AND (@TaskStatus IS NULL OR TaskStatus = @TaskStatus)
          AND (@SlaStatus  IS NULL OR SlaStatus  = @SlaStatus)
          AND (@DateFrom   IS NULL OR AssignedAt >= @DateFrom)
          AND (@DateTo     IS NULL OR AssignedAt <  DATEADD(day, 1, @DateTo))
        ORDER BY Rn
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
        OPTION (RECOMPILE);
    END
    ELSE IF @NeedHeavy = 1
    BEGIN

    ----------------------------------------------------------------------------
    -- FULL ENRICHED PATH: sort/filter needs the heavy joins (RequestProperties /
    -- AppraisalAssignments / Appointments / Companies). Seek the pool slice on the
    -- BASE table first (pt_filtered), resolve the type branch, enrich, then page.
    ----------------------------------------------------------------------------
    ;WITH pt_filtered AS (
        SELECT Id, CorrelationId, AssignedTo, AssignedType, AssigneeCompanyId,
               AssignedAt, DueAt, Movement, TaskName, TaskStatus, SlaStatus, ActivityId
        FROM   workflow.PendingTasks
        WHERE  AssignedType = @AssignedType
          AND  AssignedTo IN (SELECT value FROM STRING_SPLIT(@Assignees, ','))
          AND  ( @CompanyGate = 0
                 OR (@CompanyGate = 1 AND (AssigneeCompanyId IS NULL OR AssigneeCompanyId = @CallerCompanyId))
                 OR (@CompanyGate = 2 AND AssigneeCompanyId IS NULL) )
          AND  (@TaskName   IS NULL OR TaskName   = @TaskName)
          AND  (@TaskType   IS NULL OR TaskName   = @TaskType)
          AND  (@ActivityId IS NULL OR ActivityId = @ActivityId)
          AND  (@TaskStatus IS NULL OR TaskStatus = @TaskStatus)
          AND  (@SlaStatus  IS NULL OR SlaStatus  = @SlaStatus)
          AND  (@DateFrom   IS NULL OR AssignedAt >= @DateFrom)
          AND  (@DateTo     IS NULL OR AssignedAt <  DATEADD(day, 1, @DateTo))
    ),
    resolved AS (
        -- Each branch keeps only rows whose REQUEST matches the active text filter(s), applied
        -- BEFORE enrichment (ANDed across @CustomerName/@AppraisalNumber/@Search; @Search alone =
        -- customer-name OR appraisal-number). Semantics: matches ANY customer / ANY appraisal-number
        -- on the request. Branch keyed on its own request linkage; Normal prunes on the base column.
        -- Branch 1: QUOTATION (CorrelationId = QuotationRequests.Id)
        SELECT pt.Id, pt.AssignedAt, pt.DueAt, pt.Movement, pt.TaskName, pt.TaskStatus, pt.SlaStatus,
               qra.AppraisalId AS RAppraisalId, CAST(NULL AS uniqueidentifier) AS RRequestIdOverride
        FROM   pt_filtered pt
        JOIN   appraisal.QuotationRequests qr ON qr.Id = pt.CorrelationId
        OUTER APPLY (SELECT TOP 1 AppraisalId FROM appraisal.QuotationRequestAppraisals
                     WHERE QuotationRequestId = qr.Id ORDER BY AppraisalId) qra
        LEFT JOIN appraisal.Appraisals aq ON aq.Id = qra.AppraisalId
        WHERE  (@CustomerName    IS NULL OR aq.RequestId IN (SELECT RequestId FROM #f_cust))
          AND  (@AppraisalNumber IS NULL OR aq.RequestId IN (SELECT RequestId FROM #f_appr))
          AND  (@Search          IS NULL OR aq.RequestId IN (SELECT RequestId FROM #f_search))
        UNION ALL
        -- Branch 2: FEE-APPROVAL (CorrelationId = FeeAppointmentApprovals.Id)
        SELECT pt.Id, pt.AssignedAt, pt.DueAt, pt.Movement, pt.TaskName, pt.TaskStatus, pt.SlaStatus,
               faa.AppraisalId, CAST(NULL AS uniqueidentifier)
        FROM   pt_filtered pt
        JOIN   workflow.FeeAppointmentApprovals faa ON faa.Id = pt.CorrelationId
        LEFT JOIN appraisal.Appraisals af ON af.Id = faa.AppraisalId
        WHERE  (@CustomerName    IS NULL OR af.RequestId IN (SELECT RequestId FROM #f_cust))
          AND  (@AppraisalNumber IS NULL OR af.RequestId IN (SELECT RequestId FROM #f_appr))
          AND  (@Search          IS NULL OR af.RequestId IN (SELECT RequestId FROM #f_search))
        UNION ALL
        -- Branch 3: DOCUMENT-FOLLOWUP (CorrelationId = DocumentFollowups.Id)
        SELECT pt.Id, pt.AssignedAt, pt.DueAt, pt.Movement, pt.TaskName, pt.TaskStatus, pt.SlaStatus,
               df.AppraisalId, df.RequestId
        FROM   pt_filtered pt
        JOIN   workflow.DocumentFollowups df ON df.Id = pt.CorrelationId
        WHERE  (@CustomerName    IS NULL OR df.RequestId IN (SELECT RequestId FROM #f_cust))
          AND  (@AppraisalNumber IS NULL OR df.RequestId IN (SELECT RequestId FROM #f_appr))
          AND  (@Search          IS NULL OR df.RequestId IN (SELECT RequestId FROM #f_search))
        UNION ALL
        -- Branch 4: NORMAL (CorrelationId = Requests.Id). Prunes the 68k pool on the BASE
        -- CorrelationId column (= RequestId) BEFORE any join — the key performance win.
        SELECT pt.Id, pt.AssignedAt, pt.DueAt, pt.Movement, pt.TaskName, pt.TaskStatus, pt.SlaStatus,
               (SELECT TOP 1 a3.Id FROM appraisal.Appraisals a3 WHERE a3.RequestId = pt.CorrelationId ORDER BY a3.Id),
               pt.CorrelationId
        FROM   pt_filtered pt
        JOIN   request.Requests r4 ON r4.Id = pt.CorrelationId
        WHERE  (@CustomerName    IS NULL OR pt.CorrelationId IN (SELECT RequestId FROM #f_cust))
          AND  (@AppraisalNumber IS NULL OR pt.CorrelationId IN (SELECT RequestId FROM #f_appr))
          AND  (@Search          IS NULL OR pt.CorrelationId IN (SELECT RequestId FROM #f_search))
    ),
    enriched AS (
        SELECT
            resolved.Id,
            resolved.AssignedAt, resolved.DueAt, resolved.Movement,
            resolved.TaskName, resolved.TaskStatus, resolved.SlaStatus,
            a.AppraisalNumber,
            a.Status                                   AS AStatus,
            COALESCE(a.Priority, r.Priority)           AS Priority,
            COALESCE(a.RequestedBy, r.Requestor)       AS RequestedBy,
            COALESCE(a.RequestedAt, r.RequestedAt)     AS RequestReceivedDate,
            r.RequestNumber, r.Purpose,
            c.Name                                     AS CustomerName,
            p.PropertyType,
            ap.AppointmentDateTime,
            AA.InternalAppraiserId,
            -- Resolve to the same display name vw_TaskList shows, so sorting by these
            -- columns matches the returned values (fallback to the raw code).
            COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(ifs.FirstName, ' ', ifs.LastName))), ''),
                     AA.InternalAppraiserId)                            AS InternalFollowupStaff,
            CASE WHEN AA.AssignmentType = 'Internal' THEN COALESCE(
                     NULLIF(LTRIM(RTRIM(CONCAT(ifs.FirstName, ' ', ifs.LastName))), ''),
                     AA.InternalAppraiserId)
                 WHEN AA.AssignmentType = 'External' THEN comp.Name END AS Appraiser,
            r.Id                                       AS ReqId  -- for the any-customer name semi-join (filter only)
        FROM resolved
            LEFT JOIN appraisal.Appraisals a ON a.Id = resolved.RAppraisalId
            LEFT JOIN request.Requests     r ON r.Id = COALESCE(resolved.RRequestIdOverride, a.RequestId)
            OUTER APPLY (SELECT TOP 1 Name FROM request.RequestCustomers WHERE RequestId = r.Id) c
            OUTER APPLY (SELECT STRING_AGG(PropertyType, ',') AS PropertyType
                         FROM request.RequestProperties WHERE RequestId = r.Id) p
            OUTER APPLY (SELECT TOP 1 Id, AssignmentType, InternalAppraiserId, AssigneeCompanyId
                         FROM appraisal.AppraisalAssignments
                         WHERE AppraisalId = a.Id AND AssignmentStatus NOT IN ('Rejected','Cancelled')
                         ORDER BY AssignedAt DESC, CreatedAt DESC, Id DESC) AA
            OUTER APPLY (SELECT TOP 1 AppointmentDateTime FROM appraisal.Appointments
                         WHERE AssignmentId = AA.Id AND Status != 'Cancelled') ap
            LEFT JOIN auth.Companies comp ON comp.Id = TRY_CAST(AA.AssigneeCompanyId AS uniqueidentifier)
            LEFT JOIN auth.AspNetUsers ifs ON ifs.UserName = AA.InternalAppraiserId
    )
    INSERT INTO #page (Id, Rn, Total)
    SELECT Id,
           ROW_NUMBER() OVER (ORDER BY
               CASE WHEN @SortDir = 'ASC' THEN
                   CASE @SortBy
                       WHEN 'CustomerName'          THEN CustomerName
                       WHEN 'AppraisalNumber'       THEN AppraisalNumber
                       WHEN 'RequestNumber'         THEN RequestNumber
                       WHEN 'TaskType'              THEN TaskName
                       WHEN 'Purpose'               THEN Purpose
                       WHEN 'PropertyType'          THEN PropertyType
                       WHEN 'Status'                THEN AStatus
                       WHEN 'RequestedBy'           THEN RequestedBy
                       WHEN 'Movement'              THEN Movement
                       WHEN 'InternalFollowupStaff' THEN InternalFollowupStaff
                       WHEN 'Appraiser'             THEN Appraiser
                       WHEN 'Priority'              THEN Priority
                       WHEN 'SlaStatus'             THEN SlaStatus
                   END END ASC,
               CASE WHEN @SortDir = 'DESC' THEN
                   CASE @SortBy
                       WHEN 'CustomerName'          THEN CustomerName
                       WHEN 'AppraisalNumber'       THEN AppraisalNumber
                       WHEN 'RequestNumber'         THEN RequestNumber
                       WHEN 'TaskType'              THEN TaskName
                       WHEN 'Purpose'               THEN Purpose
                       WHEN 'PropertyType'          THEN PropertyType
                       WHEN 'Status'                THEN AStatus
                       WHEN 'RequestedBy'           THEN RequestedBy
                       WHEN 'Movement'              THEN Movement
                       WHEN 'InternalFollowupStaff' THEN InternalFollowupStaff
                       WHEN 'Appraiser'             THEN Appraiser
                       WHEN 'Priority'              THEN Priority
                       WHEN 'SlaStatus'             THEN SlaStatus
                   END END DESC,
               CASE WHEN @SortDir = 'ASC' THEN
                   CASE @SortBy
                       WHEN 'AppointmentDateTime'   THEN AppointmentDateTime
                       WHEN 'RequestReceivedDate'   THEN RequestReceivedDate
                       WHEN 'DueAt'                 THEN DueAt
                       WHEN 'AssignedDate'          THEN AssignedAt
                   END END ASC,
               CASE WHEN @SortDir = 'DESC' THEN
                   CASE @SortBy
                       WHEN 'AppointmentDateTime'   THEN AppointmentDateTime
                       WHEN 'RequestReceivedDate'   THEN RequestReceivedDate
                       WHEN 'DueAt'                 THEN DueAt
                       WHEN 'AssignedDate'          THEN AssignedAt
                   END END DESC,
               AssignedAt DESC, Id DESC) AS Rn,
           COUNT(*) OVER () AS Total
    FROM enriched
    WHERE (@Status          IS NULL OR AStatus = @Status)
      AND (@Priority        IS NULL OR Priority = @Priority)
      -- @AppraisalNumber / @CustomerName / @Search are applied per-branch in `resolved` (#f_cust/#f_appr/#f_search pushdown).
      AND (@AppointmentDateFrom IS NULL OR AppointmentDateTime >= @AppointmentDateFrom)
      AND (@AppointmentDateTo   IS NULL OR AppointmentDateTime <  DATEADD(day, 1, @AppointmentDateTo))
      AND (@RequestedAtFrom     IS NULL OR RequestReceivedDate >= @RequestedAtFrom)
      AND (@RequestedAtTo       IS NULL OR RequestReceivedDate <  DATEADD(day, 1, @RequestedAtTo))
    ORDER BY Rn
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE, MAXDOP 1);   -- RECOMPILE prunes unused predicates; MAXDOP 1 smooths 50-VUS oversubscription

    END  -- ELSE IF (@NeedHeavy = 1): full enriched path
    ELSE
    BEGIN

    ----------------------------------------------------------------------------
    -- LIGHT + NAME-PUSHDOWN PATH: enriched filter/sort, but NONE of the heavy joins
    -- are needed. Join only Appraisals + Requests, and push the customer-name filter
    -- down to a one-time IX_RequestCustomer_Name scan + semi-join on r.Id so a
    -- selective term prunes the pool BEFORE enrichment (display CustomerName comes
    -- from stage 2). Semantics: matches ANY customer on the request (not just TOP 1).
    -- Count/row parity holds by the same CorrelationId 1:1 invariant as the base path.
    ----------------------------------------------------------------------------
    ;WITH pt_filtered AS (
        SELECT Id, CorrelationId, AssignedTo, AssignedType, AssigneeCompanyId,
               AssignedAt, DueAt, Movement, TaskName, TaskStatus, SlaStatus, ActivityId
        FROM   workflow.PendingTasks
        WHERE  AssignedType = @AssignedType
          AND  AssignedTo IN (SELECT value FROM STRING_SPLIT(@Assignees, ','))
          AND  ( @CompanyGate = 0
                 OR (@CompanyGate = 1 AND (AssigneeCompanyId IS NULL OR AssigneeCompanyId = @CallerCompanyId))
                 OR (@CompanyGate = 2 AND AssigneeCompanyId IS NULL) )
          AND  (@TaskName   IS NULL OR TaskName   = @TaskName)
          AND  (@TaskType   IS NULL OR TaskName   = @TaskType)
          AND  (@ActivityId IS NULL OR ActivityId = @ActivityId)
          AND  (@TaskStatus IS NULL OR TaskStatus = @TaskStatus)
          AND  (@SlaStatus  IS NULL OR SlaStatus  = @SlaStatus)
          AND  (@DateFrom   IS NULL OR AssignedAt >= @DateFrom)
          AND  (@DateTo     IS NULL OR AssignedAt <  DATEADD(day, 1, @DateTo))
    ),
    resolved AS (
        -- Each branch keeps only rows whose REQUEST matches the active text filter(s), applied
        -- BEFORE enrichment (ANDed across @CustomerName/@AppraisalNumber/@Search; @Search alone =
        -- customer-name OR appraisal-number). Semantics: matches ANY customer / ANY appraisal-number
        -- on the request. Branch keyed on its own request linkage; Normal prunes on the base column.
        -- Branch 1: QUOTATION (CorrelationId = QuotationRequests.Id)
        SELECT pt.Id, pt.AssignedAt, pt.DueAt, pt.Movement, pt.TaskName, pt.TaskStatus, pt.SlaStatus,
               qra.AppraisalId AS RAppraisalId, CAST(NULL AS uniqueidentifier) AS RRequestIdOverride
        FROM   pt_filtered pt
        JOIN   appraisal.QuotationRequests qr ON qr.Id = pt.CorrelationId
        OUTER APPLY (SELECT TOP 1 AppraisalId FROM appraisal.QuotationRequestAppraisals
                     WHERE QuotationRequestId = qr.Id ORDER BY AppraisalId) qra
        LEFT JOIN appraisal.Appraisals aq ON aq.Id = qra.AppraisalId
        WHERE  (@CustomerName    IS NULL OR aq.RequestId IN (SELECT RequestId FROM #f_cust))
          AND  (@AppraisalNumber IS NULL OR aq.RequestId IN (SELECT RequestId FROM #f_appr))
          AND  (@Search          IS NULL OR aq.RequestId IN (SELECT RequestId FROM #f_search))
        UNION ALL
        -- Branch 2: FEE-APPROVAL (CorrelationId = FeeAppointmentApprovals.Id)
        SELECT pt.Id, pt.AssignedAt, pt.DueAt, pt.Movement, pt.TaskName, pt.TaskStatus, pt.SlaStatus,
               faa.AppraisalId, CAST(NULL AS uniqueidentifier)
        FROM   pt_filtered pt
        JOIN   workflow.FeeAppointmentApprovals faa ON faa.Id = pt.CorrelationId
        LEFT JOIN appraisal.Appraisals af ON af.Id = faa.AppraisalId
        WHERE  (@CustomerName    IS NULL OR af.RequestId IN (SELECT RequestId FROM #f_cust))
          AND  (@AppraisalNumber IS NULL OR af.RequestId IN (SELECT RequestId FROM #f_appr))
          AND  (@Search          IS NULL OR af.RequestId IN (SELECT RequestId FROM #f_search))
        UNION ALL
        -- Branch 3: DOCUMENT-FOLLOWUP (CorrelationId = DocumentFollowups.Id)
        SELECT pt.Id, pt.AssignedAt, pt.DueAt, pt.Movement, pt.TaskName, pt.TaskStatus, pt.SlaStatus,
               df.AppraisalId, df.RequestId
        FROM   pt_filtered pt
        JOIN   workflow.DocumentFollowups df ON df.Id = pt.CorrelationId
        WHERE  (@CustomerName    IS NULL OR df.RequestId IN (SELECT RequestId FROM #f_cust))
          AND  (@AppraisalNumber IS NULL OR df.RequestId IN (SELECT RequestId FROM #f_appr))
          AND  (@Search          IS NULL OR df.RequestId IN (SELECT RequestId FROM #f_search))
        UNION ALL
        -- Branch 4: NORMAL (CorrelationId = Requests.Id). Prunes the 68k pool on the BASE
        -- CorrelationId column (= RequestId) BEFORE any join — the key performance win.
        SELECT pt.Id, pt.AssignedAt, pt.DueAt, pt.Movement, pt.TaskName, pt.TaskStatus, pt.SlaStatus,
               (SELECT TOP 1 a3.Id FROM appraisal.Appraisals a3 WHERE a3.RequestId = pt.CorrelationId ORDER BY a3.Id),
               pt.CorrelationId
        FROM   pt_filtered pt
        JOIN   request.Requests r4 ON r4.Id = pt.CorrelationId
        WHERE  (@CustomerName    IS NULL OR pt.CorrelationId IN (SELECT RequestId FROM #f_cust))
          AND  (@AppraisalNumber IS NULL OR pt.CorrelationId IN (SELECT RequestId FROM #f_appr))
          AND  (@Search          IS NULL OR pt.CorrelationId IN (SELECT RequestId FROM #f_search))
    ),
    light AS (
        SELECT
            resolved.Id,
            resolved.AssignedAt, resolved.DueAt, resolved.Movement,
            resolved.TaskName, resolved.TaskStatus, resolved.SlaStatus,
            a.AppraisalNumber,
            a.Status                                   AS AStatus,
            COALESCE(a.Priority, r.Priority)           AS Priority,
            COALESCE(a.RequestedBy, r.Requestor)       AS RequestedBy,
            COALESCE(a.RequestedAt, r.RequestedAt)     AS RequestReceivedDate,
            r.RequestNumber, r.Purpose,
            r.Id                                       AS ReqId  -- = COALESCE(RRequestIdOverride, a.RequestId)
        FROM resolved
            LEFT JOIN appraisal.Appraisals a ON a.Id = resolved.RAppraisalId
            LEFT JOIN request.Requests     r ON r.Id = COALESCE(resolved.RRequestIdOverride, a.RequestId)
        -- NO RequestCustomers/RequestProperties/AppraisalAssignments/Appointments/Companies here
    )
    INSERT INTO #page (Id, Rn, Total)
    SELECT Id,
           ROW_NUMBER() OVER (ORDER BY
               CASE WHEN @SortDir = 'ASC' THEN
                   CASE @SortBy
                       WHEN 'AppraisalNumber' THEN AppraisalNumber
                       WHEN 'RequestNumber'   THEN RequestNumber
                       WHEN 'TaskType'        THEN TaskName
                       WHEN 'Purpose'         THEN Purpose
                       WHEN 'Status'          THEN AStatus
                       WHEN 'RequestedBy'     THEN RequestedBy
                       WHEN 'Movement'        THEN Movement
                       WHEN 'Priority'        THEN Priority
                       WHEN 'SlaStatus'       THEN SlaStatus
                   END END ASC,
               CASE WHEN @SortDir = 'DESC' THEN
                   CASE @SortBy
                       WHEN 'AppraisalNumber' THEN AppraisalNumber
                       WHEN 'RequestNumber'   THEN RequestNumber
                       WHEN 'TaskType'        THEN TaskName
                       WHEN 'Purpose'         THEN Purpose
                       WHEN 'Status'          THEN AStatus
                       WHEN 'RequestedBy'     THEN RequestedBy
                       WHEN 'Movement'        THEN Movement
                       WHEN 'Priority'        THEN Priority
                       WHEN 'SlaStatus'       THEN SlaStatus
                   END END DESC,
               CASE WHEN @SortDir = 'ASC' THEN
                   CASE @SortBy
                       WHEN 'RequestReceivedDate' THEN RequestReceivedDate
                       WHEN 'DueAt'               THEN DueAt
                       WHEN 'AssignedDate'        THEN AssignedAt
                   END END ASC,
               CASE WHEN @SortDir = 'DESC' THEN
                   CASE @SortBy
                       WHEN 'RequestReceivedDate' THEN RequestReceivedDate
                       WHEN 'DueAt'               THEN DueAt
                       WHEN 'AssignedDate'        THEN AssignedAt
                   END END DESC,
               AssignedAt DESC, Id DESC) AS Rn,
           COUNT(*) OVER () AS Total
    FROM light
    WHERE (@Status          IS NULL OR AStatus  = @Status)
      AND (@Priority        IS NULL OR Priority = @Priority)
      -- @AppraisalNumber / @CustomerName / @Search are applied per-branch in `resolved` (#f_cust/#f_appr/#f_search pushdown).
      AND (@RequestedAtFrom IS NULL OR RequestReceivedDate >= @RequestedAtFrom)
      AND (@RequestedAtTo   IS NULL OR RequestReceivedDate <  DATEADD(day, 1, @RequestedAtTo))
    ORDER BY Rn
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
    OPTION (RECOMPILE, MAXDOP 1);   -- RECOMPILE literalizes NULL params; MAXDOP 1 smooths 50-VUS oversubscription

    END  -- ELSE: light + name-pushdown path

    ----------------------------------------------------------------------------
    -- STAGE 2: full display enrichment for ONLY the page's ids (reuses the view
    -- so the projected columns stay in exact parity with the DTO).
    ----------------------------------------------------------------------------
    SELECT v.*
    FROM   workflow.vw_TaskList v
    JOIN   #page p ON p.Id = v.Id
    ORDER BY p.Rn;

    -- Total filtered count (second result set; 0 when the page is empty/past the end).
    SELECT ISNULL((SELECT TOP 1 Total FROM #page), 0) AS Total;

    DROP TABLE #page;
    DROP TABLE #f_cust;
    DROP TABLE #f_appr;
    DROP TABLE #f_search;
END
-- NOTE: keep the 4-branch routing + column derivations here in lockstep with
-- Database/Scripts/Views/Workflow/vw_TaskList.sql. Verify with docs/task-list/parity_vw_TaskList.sql.
