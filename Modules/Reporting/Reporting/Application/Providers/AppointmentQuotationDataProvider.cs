using Reporting.Application.Models;
using Reporting.Application.Services;

namespace Reporting.Application.Providers;

/// <summary>
/// Assembles an <see cref="AppointmentQuotationFormModel"/> (FSD Ch.10 §2.1.1) from the
/// database using Dapper + existing views/tables. All reads are read-only (no EF tracking).
///
/// Data sourcing:
///   - appraisal.vw_AppraisalList → customer name, purpose, facility limit, appraisal type, request id/date
///   - request.Requests           → maker (Creator) display name
///   - request.RequestDetails     → contact, fee, prev-appraisal-number, single collateral address
///   - request.RequestProperties  → distinct property/building types (free-text detail, NOT a table)
///   - request.RequestDocuments   → uploaded PDF attachments to append after the form
///   - parameter.Parameters       → code→Thai descriptions (AppraisalPurpose / PropertyType /
///                                  BuildingType / FeePaymentMethod groups)
///   - parameter.Title{Provinces,Districts,SubDistricts} → address geocode→Thai name
///
/// Fields the FSD shows but the Request schema does NOT carry are intentionally left null and
/// render as blank fill-in lines: the requestor org block (Section/Department/Line-of-work/
/// Cost Center, FSD 1–4), the referrer (5–6), the administrative-jurisdiction address (22–28,
/// only one address exists), and the checker (33–34, no checker role on requests).
/// </summary>
public sealed class AppointmentQuotationDataProvider(
    ISqlConnectionFactory connectionFactory,
    ILogger<AppointmentQuotationDataProvider> logger)
    : IReportDataProvider
{
    public string ReportTypeKey => "appointment-letter";

    public async Task<object> GetModelAsync(string entityId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(entityId, out var requestId))
            throw new NotFoundException("Request", entityId);

        using var connection = connectionFactory.CreateNewConnection();

        // ── 1. Request header (customer, purpose, loan, requestor) ──────────────
        // Keyed by RequestId so the letter can be produced at the request stage (an appraisal need
        // not exist yet). The screen resolves a typed Request No. OR Appraisal No. → RequestId via
        // ReportEntityResolver.
        const string headerSql = """
            SELECT
                cust.Name              AS CustomerName,
                COALESCE(pPurpose.Description, r.Purpose) AS AppraisalPurpose,
                rd.FacilityLimit       AS LoanAmount,
                r.RequestedAt          AS RequestDate,
                r.CreatorName          AS RequesterMakerName,
                r.RequestorName        AS ReferrerName,
                ru.PhoneNumber         AS ReferrerTel,
                rd.PrevAppraisalNumber AS PrevAppraisalNumber
            FROM request.Requests r
            LEFT JOIN request.RequestDetails rd ON rd.RequestId = r.Id
            LEFT JOIN auth.AspNetUsers ru ON ru.UserName = r.Requestor
            OUTER APPLY (
                SELECT TOP 1 c.Name FROM request.RequestCustomers c
                WHERE c.RequestId = r.Id ORDER BY c.Id
            ) cust
            LEFT JOIN parameter.Parameters pPurpose
                ON pPurpose.[Group]    = 'AppraisalPurpose'
               AND pPurpose.[Language] = 'TH'
               AND pPurpose.IsActive   = 1
               AND pPurpose.[Code]     = r.Purpose
            WHERE r.Id = @RequestId AND r.IsDeleted = 0
            """;

        var detailParams = new DynamicParameters();
        detailParams.Add("RequestId", requestId);

        var header = await connection.QueryFirstOrDefaultAsync<HeaderRow>(headerSql, detailParams);
        if (header is null)
            throw new NotFoundException("Request", entityId);

        // ── 2. Request detail (contact, fee, old report number) ────────────────
        // Contact/Fee are owned value objects flattened into request.RequestDetails. Addresses now
        // come from the title (section 2b) — RequestDetails address is no longer used here.
        const string detailSql = """
            SELECT
                rd.ContactPersonName,
                rd.ContactPersonPhone  AS ContactPersonTel,
                COALESCE(pFee.Description, rd.FeePaymentType) AS FeePaymentType,
                rd.PrevAppraisalNumber AS OldAppraisalReportNumber
            FROM request.RequestDetails rd
            LEFT JOIN parameter.Parameters pFee
                ON pFee.[Group]    = 'FeePaymentMethod'
               AND pFee.[Language] = 'TH'
               AND pFee.IsActive   = 1
               AND pFee.[Code]     = rd.FeePaymentType
            WHERE rd.RequestId = @RequestId
            """;

        var detail = await connection.QueryFirstOrDefaultAsync<DetailRow>(detailSql, detailParams);

        // ── 2b. Title addresses: deed (TitleAddress→Title master, FSD 15–21) and
        //         administrative (DopaAddress→Dopa master, FSD 22–28). One form → first title. ──
        const string titleAddrSql = """
            SELECT TOP 1
                t.ProjectName AS TitleProjectName,
                t.HouseNumber AS TitleHouseNumber,
                t.Soi         AS TitleSoi,
                t.Road        AS TitleRoad,
                COALESCE(tsub.NameTh,  t.SubDistrict) AS TitleSubDistrict,
                COALESCE(tdist.NameTh, t.District)    AS TitleDistrict,
                COALESCE(tprov.NameTh, t.Province)    AS TitleProvince,
                t.DopaProjectName AS DopaProjectName,
                t.DopaHouseNumber AS DopaHouseNumber,
                t.DopaSoi         AS DopaSoi,
                t.DopaRoad        AS DopaRoad,
                COALESCE(dsub.NameTh,  t.DopaSubDistrict) AS DopaSubDistrict,
                COALESCE(ddist.NameTh, t.DopaDistrict)    AS DopaDistrict,
                COALESCE(dprov.NameTh, t.DopaProvince)    AS DopaProvince
            FROM request.RequestTitles t
            LEFT JOIN parameter.TitleProvinces    tprov ON tprov.Code = t.Province
            LEFT JOIN parameter.TitleDistricts    tdist ON tdist.Code = t.District
            LEFT JOIN parameter.TitleSubDistricts tsub  ON tsub.Code  = t.SubDistrict
            LEFT JOIN parameter.DopaProvinces     dprov ON dprov.Code = t.DopaProvince
            LEFT JOIN parameter.DopaDistricts     ddist ON ddist.Code = t.DopaDistrict
            LEFT JOIN parameter.DopaSubDistricts  dsub  ON dsub.Code  = t.DopaSubDistrict
            WHERE t.RequestId = @RequestId
            ORDER BY t.Id
            """;

        var titleAddr = await connection.QueryFirstOrDefaultAsync<TitleAddrRow>(titleAddrSql, detailParams);

        // ── 2c. Checker (FSD 33–34) ← whoever the appraisal-initiation-check task was assigned
        //         to, and when they completed it. The task is optional (entry-source dependent),
        //         so this tolerates no row. CompletedTask.CorrelationId is the appraisal's RequestId.
        const string checkTaskSql = """
            SELECT TOP 1
                ct.AssignedTo,
                LTRIM(RTRIM(COALESCE(u.FirstName, '') + ' ' + COALESCE(u.LastName, ''))) AS AssignedToName,
                ct.CompletedAt
            FROM workflow.CompletedTasks ct
            LEFT JOIN auth.AspNetUsers u ON u.UserName = ct.AssignedTo
            WHERE ct.CorrelationId = @RequestId
              AND ct.ActivityId = 'appraisal-initiation-check'
            ORDER BY ct.CompletedAt DESC
            """;

        var checkTask = await connection.QueryFirstOrDefaultAsync<CheckTaskRow>(checkTaskSql, detailParams);
        var checkerName = checkTask is null
            ? null
            : (!string.IsNullOrWhiteSpace(checkTask.AssignedToName) ? checkTask.AssignedToName : checkTask.AssignedTo);

        // ── 3. Collateral detail (FSD 13/14) — free text, not a table ───────────
        // request.RequestProperties carries PropertyType/BuildingType only. The FSD front page
        // shows one free-text "รายละเอียดทรัพย์สิน" + "ประเภทหลักประกัน", so collapse the rows
        // into distinct, comma-joined values.
        const string propertiesSql = """
            SELECT DISTINCT
                COALESCE(pPT.Description, rp.PropertyType) AS PropertyType,
                COALESCE(pBT.Description, rp.BuildingType) AS BuildingType
            FROM request.RequestProperties rp
            LEFT JOIN parameter.Parameters pPT
                ON pPT.[Group]    = 'PropertyType'
               AND pPT.[Language] = 'TH'
               AND pPT.IsActive   = 1
               AND pPT.[Code]     = rp.PropertyType
            LEFT JOIN parameter.Parameters pBT
                ON pBT.[Group]    = 'BuildingType'
               AND pBT.[Language] = 'TH'
               AND pBT.IsActive   = 1
               AND pBT.[Code]     = rp.BuildingType
            WHERE rp.RequestId = @RequestId
            """;

        var propertyRows = (await connection.QueryAsync<PropertyTypeRow>(propertiesSql, detailParams))
            .ToList();

        var propertyDetail = JoinDistinct(propertyRows.Select(p => p.PropertyType));
        var collateralType = JoinDistinct(propertyRows.Select(p => p.BuildingType));

        // ── 4. Collateral new/existing: existing (เดิม) when a previous appraisal is referenced ─
        bool isNewCollateral = string.IsNullOrWhiteSpace(header.PrevAppraisalNumber);

        // ── 5. Uploaded PDF attachments linked to the request ───────────────────
        const string attachmentsSql = """
            SELECT rdoc.DocumentId
            FROM request.RequestDocuments rdoc
            INNER JOIN document.Documents d ON d.Id = rdoc.DocumentId
            WHERE rdoc.RequestId = @RequestId
              AND rdoc.DocumentId IS NOT NULL
              AND d.IsDeleted = 0
              AND d.IsActive  = 1
              AND d.MimeType  = 'application/pdf'
            ORDER BY rdoc.CreatedAt
            """;

        var attachmentDocumentIds =
            (await connection.QueryAsync<Guid>(attachmentsSql, detailParams)).ToList();

        // ── 6. Page-2 checklist: which docs were uploaded at the title level ────
        // A doc ticks when an uploaded TitleDocument's DocumentType matches a checklist item's
        // code, scoped to a title whose CollateralType belongs to that section (same rule as
        // GetRequestDocumentChecklistQueryHandler).
        const string titleDocsSql = """
            SELECT t.CollateralType, td.DocumentType
            FROM request.RequestTitles t
            INNER JOIN request.RequestTitleDocuments td ON td.TitleId = t.Id
            WHERE t.RequestId = @RequestId
              AND td.DocumentId  IS NOT NULL
              AND td.DocumentType IS NOT NULL
            """;

        var uploaded = new HashSet<(string Collateral, string DocType)>();
        foreach (var row in await connection.QueryAsync<TitleDocRow>(titleDocsSql, detailParams))
        {
            if (string.IsNullOrWhiteSpace(row.CollateralType) || string.IsNullOrWhiteSpace(row.DocumentType))
                continue;
            uploaded.Add((row.CollateralType.Trim(), row.DocumentType.Trim().ToUpperInvariant()));
        }

        var checklistSections = Checklist.Select(sec => new ChecklistSection
        {
            CollateralLabel = sec.Label,
            Items = sec.Items.Select(it => new ChecklistItem
            {
                Label = it.Label,
                IsChecked = it.DocCodes.Any(dc =>
                    sec.CollateralCodes.Any(cc => uploaded.Contains((cc, dc))))
            }).ToList()
        }).ToList();

        // ── Build model (Dash → "-" for any null/empty text value) ──────────────
        var model = new AppointmentQuotationFormModel
        {
            // Requestor org block (1–4) still has no data source → "-".
            Division = Dash(null),
            Department = Dash(null),
            LineOfWork = Dash(null),
            CostCenter = Dash(null),

            // Referrer (5–6) ← the request's Requestor (name + auth phone).
            ReferrerName = Dash(header.ReferrerName),
            ReferrerTel = Dash(header.ReferrerTel),

            CustomerName = Dash(header.CustomerName),
            AppraisalPurpose = Dash(header.AppraisalPurpose),
            LoanAmount = header.LoanAmount,          // numeric → template renders "-" when null
            RequesterMakerName = Dash(header.RequesterMakerName),
            RequestDate = header.RequestDate,        // date → template renders "-" when null

            ContactPersonName = Dash(detail?.ContactPersonName),
            ContactPersonTel = Dash(detail?.ContactPersonTel),
            FeePaymentType = Dash(detail?.FeePaymentType),
            OldAppraisalReportNumber = Dash(detail?.OldAppraisalReportNumber),

            IsNewCollateral = isNewCollateral,
            PropertyDetail = Dash(propertyDetail),
            CollateralType = Dash(collateralType),
            ChecklistSections = checklistSections,

            // Property location per title deed (ตามโฉนด, 15–21) ← RequestTitle.TitleAddress.
            ProjectName = Dash(titleAddr?.TitleProjectName),
            HouseNumber = Dash(titleAddr?.TitleHouseNumber),
            Soi = Dash(titleAddr?.TitleSoi),
            Road = Dash(titleAddr?.TitleRoad),
            SubDistrict = Dash(titleAddr?.TitleSubDistrict),
            District = Dash(titleAddr?.TitleDistrict),
            Province = Dash(titleAddr?.TitleProvince),

            // Property location per administrative jurisdiction (ตามเขตปกครอง, 22–28) ← DopaAddress.
            AdminProjectName = Dash(titleAddr?.DopaProjectName),
            AdminHouseNumber = Dash(titleAddr?.DopaHouseNumber),
            AdminSoi = Dash(titleAddr?.DopaSoi),
            AdminRoad = Dash(titleAddr?.DopaRoad),
            AdminSubDistrict = Dash(titleAddr?.DopaSubDistrict),
            AdminDistrict = Dash(titleAddr?.DopaDistrict),
            AdminProvince = Dash(titleAddr?.DopaProvince),

            // Checker (33–34) ← appraisal-initiation-check assignee + its completion date.
            RequesterCheckerName = Dash(checkerName),
            CheckerDate = checkTask?.CompletedAt,

            AttachmentsBySlot = new Dictionary<string, IReadOnlyList<Guid>>
            {
                ["attachments"] = attachmentDocumentIds
            }
        };

        logger.LogDebug(
            "AppointmentLetter model assembled for request {RequestId}: {PropertyTypeCount} distinct property types, {AttachmentCount} PDF attachments",
            requestId, propertyRows.Count, attachmentDocumentIds.Count);

        return model;
    }

    /// <summary>Returns the trimmed value, or "-" when null/blank (FSD blanks print as a dash).</summary>
    private static string Dash(string? v) => string.IsNullOrWhiteSpace(v) ? "-" : v.Trim();

    /// <summary>Joins distinct, non-blank values with a comma; returns null when nothing to show.</summary>
    private static string? JoinDistinct(IEnumerable<string?> values)
    {
        var distinct = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return distinct.Count == 0 ? null : string.Join(", ", distinct);
    }

    // ── Private flat DTOs for Dapper mapping ───────────────────────────────────

    private sealed class HeaderRow
    {
        public string? CustomerName { get; init; }
        public string? AppraisalPurpose { get; init; }
        public decimal? LoanAmount { get; init; }
        public DateTime? RequestDate { get; init; }
        public string? RequesterMakerName { get; init; }
        public string? ReferrerName { get; init; }
        public string? ReferrerTel { get; init; }
        public string? PrevAppraisalNumber { get; init; }
    }

    private sealed class DetailRow
    {
        public string? ContactPersonName { get; init; }
        public string? ContactPersonTel { get; init; }
        public string? FeePaymentType { get; init; }
        public string? OldAppraisalReportNumber { get; init; }
    }

    private sealed class TitleAddrRow
    {
        public string? TitleProjectName { get; init; }
        public string? TitleHouseNumber { get; init; }
        public string? TitleSoi { get; init; }
        public string? TitleRoad { get; init; }
        public string? TitleSubDistrict { get; init; }
        public string? TitleDistrict { get; init; }
        public string? TitleProvince { get; init; }
        public string? DopaProjectName { get; init; }
        public string? DopaHouseNumber { get; init; }
        public string? DopaSoi { get; init; }
        public string? DopaRoad { get; init; }
        public string? DopaSubDistrict { get; init; }
        public string? DopaDistrict { get; init; }
        public string? DopaProvince { get; init; }
    }

    private sealed class PropertyTypeRow
    {
        public string? PropertyType { get; init; }
        public string? BuildingType { get; init; }
    }

    private sealed class TitleDocRow
    {
        public string? CollateralType { get; init; }
        public string? DocumentType { get; init; }
    }

    private sealed class CheckTaskRow
    {
        public string? AssignedTo { get; init; }
        public string? AssignedToName { get; init; }
        public DateTime? CompletedAt { get; init; }
    }

    // ── Page-2 checklist definition (FSD img4) ──────────────────────────────────
    // Each section maps an FSD collateral-type row to the CollateralType codes it covers;
    // each item maps an FSD document line to the DocumentType code(s) (D001–D041) that tick it.
    // NOTE: items with an empty DocCodes set, or a section with empty CollateralCodes, can never
    // tick (no taxonomy code / unconfirmed mapping) — see plan's ⚠ items.
    private sealed record ChecklistDef(string Label, string[] CollateralCodes, ItemDef[] Items);
    private sealed record ItemDef(string Label, string[] DocCodes);

    private const string DocDeed       = "D013"; // เอกสารสิทธิ์ขนาดเท่าตัวจริง
    private const string DocIdCard     = "D015"; // บัตรประชาชน
    private const string DocBorrowerCert = "D016"; // หนังสือรับรองของผู้กู้
    private const string DocSaleAgmt   = "D014"; // สัญญาซื้อขาย
    private const string DocLocMap     = "D006"; // แผนที่ที่ตั้งทรัพย์สิน
    private const string DocBldgPlan   = "D020"; // แบบแปลนอาคาร
    private const string DocBuildPermit = "D017"; // ใบขออนุญาตสิ่งปลูกสร้าง
    private const string DocBuildLicense = "D026"; // ใบอนุญาตปลูกสร้าง
    private const string DocHouseReg   = "D019"; // ทะเบียนบ้าน (เจ้าบ้าน)
    private const string DocAllocPermit = "D021"; // ใบขออนุญาตจัดสรรที่ดิน
    private const string DocProjPermit = "D023"; // ใบขออนุญาตสิ่งปลูกสร้างของโครงการ
    private const string DocProjOwnerCert = "D024"; // หนังสือรับรอง (เจ้าของโครงการ)
    private const string DocCarReg     = "D028"; // คู่มือทะเบียนรถยนต์
    private const string DocMachineReg = "D029"; // ทะเบียนเครื่องจักรและแผนผังที่ตั้ง
    private const string DocMachineManual = "D031"; // คู่มือการใช้งานเครื่องจักร
    private const string DocInvoice    = "D030"; // ใบสั่งซื้อ (Invoice)
    private const string DocBoatReg    = "D032"; // ทะเบียนเรือ
    private const string DocBoatCert   = "D033"; // ใบทะเบียนประจำเรือ
    private const string DocBoatBuild  = "D034"; // สัญญาต่อเรือ
    private const string DocConstructContract = "D025"; // หนังสือสัญญาจ้างก่อสร้าง

    private static readonly ChecklistDef[] Checklist =
    [
        new("ที่ดิน", ["01", "13", "14", "17", "19", "21", "26", "27"],
        [
            new("สำเนาเอกสารสิทธิ์ขนาดเท่าตัวจริง (พิมพ์ขยายหน้า-หลัง โฉนด/นส.3 ทุกแปลง)", [DocDeed]),
            new("สำเนาบัตรประชาชนหรือสำเนาหนังสือรับรองของผู้กู้", [DocIdCard, DocBorrowerCert]),
            new("สำเนาสัญญาซื้อขาย", [DocSaleAgmt]),
            new("แผนที่ตั้งหลักประกัน", [DocLocMap]),
        ]),
        new("ที่ดินพร้อมสิ่งปลูกสร้าง ทุกประเภท", ["02", "03"],
        [
            new("สำเนาเอกสารสิทธิ์ขนาดเท่าตัวจริง (พิมพ์ขยายหน้า-หลัง โฉนด/นส.3 ทุกแปลง)", [DocDeed]),
            new("สำเนาแบบแปลนอาคาร", [DocBldgPlan]),
            new("แผนที่ตั้งหลักประกัน", [DocLocMap]),
            new("สำเนาใบขออนุญาตปลูกสร้าง หรือ สำเนาใบรับรองสิ่งปลูกสร้าง หรือ สำเนาทะเบียนบ้าน (เจ้าบ้าน)", [DocBuildPermit, DocBuildLicense, DocHouseReg]),
            new("สำเนาบัตรประชาชนหรือสำเนาหนังสือรับรองของผู้กู้", [DocIdCard, DocBorrowerCert]),
        ]),
        new("ห้องชุด", ["08", "33"],
        [
            new("สำเนาเอกสารสิทธิ์ขนาดเท่าตัวจริง (พิมพ์ขยายหน้า-หลัง โฉนด/นส.3 ทุกแปลง)", [DocDeed]),
            new("สำเนาบัตรประชาชนหรือสำเนาหนังสือรับรองของผู้กู้", [DocIdCard, DocBorrowerCert]),
            new("สำเนาสัญญาซื้อขาย", [DocSaleAgmt]),
            new("สำเนาหนังสือกรรมสิทธิ์ห้องชุด", [DocDeed]),
        ]),
        new("ที่ดินจัดสรร (ทั้งโครงการ)", ["04"],
        [
            new("สำเนาเอกสารสิทธิ์ขนาดเท่าตัวจริง (พิมพ์ขยายหน้า-หลัง โฉนด/นส.3 ทุกแปลง)", [DocDeed]),
            new("สำเนาบัตรประชาชนหรือสำเนาหนังสือรับรอง (เจ้าของโครงการ)", [DocProjOwnerCert, DocIdCard]),
            new("สำเนาใบขออนุญาตจัดสรรที่ดิน", [DocAllocPermit]),
            new("แผนที่ตั้งหลักประกัน", [DocLocMap]),
        ]),
        new("สิ่งปลูกสร้าง (ทั้งโครงการ)", ["05", "06", "07", "15", "16", "18", "20", "22"],
        [
            new("สำเนาเอกสารสิทธิ์ขนาดเท่าตัวจริง (พิมพ์ขยายหน้า-หลัง โฉนด/นส.3 ทุกแปลง)", [DocDeed]),
            new("สำเนาใบขออนุญาตปลูกสร้างของโครงการ", [DocProjPermit]),
            new("สำเนาแบบแปลนอาคาร", [DocBldgPlan]),
            new("แผนที่ตั้งหลักประกัน", [DocLocMap]),
            new("สำเนาใบรับรองสิ่งปลูกสร้าง", [DocBuildLicense]),
            new("สำเนาบัตรประชาชนหรือสำเนาหนังสือรับรอง (เจ้าของโครงการ)", [DocProjOwnerCert, DocIdCard]),
        ]),
        new("สิทธิการเช่าอสังหาริมทรัพย์", ["09", "25", "28", "29", "30", "31"],
        [
            // ⚠ no DocumentType code exists for lease agreements today → never ticks until one is added
            new("สำเนาสัญญาเช่า", []),
            new("สำเนาสัญญาเช่าที่มีการจดทะเบียนกับเจ้าพนักงานตามกฎหมาย", []),
            new("แผนที่ตั้งหลักประกัน", [DocLocMap]),
            new("สำเนาเอกสารสิทธิ์ (ผู้ให้เช่า)", [DocDeed]),
            new("สำเนาบัตรประชาชนหรือสำเนาหนังสือรับรองของผู้กู้", [DocIdCard, DocBorrowerCert]),
        ]),
        new("รถยนต์", ["10"],
        [
            new("สำเนาคู่มือจดทะเบียนรถยนต์", [DocCarReg]),
            new("สำเนาบัตรประชาชนหรือสำเนาหนังสือรับรองของผู้กู้", [DocIdCard, DocBorrowerCert]),
            new("สำเนาสัญญาซื้อขายรถ หรือ สัญญาเช่าซื้อ", [DocSaleAgmt]),
        ]),
        new("เครื่องจักร", ["11"],
        [
            new("สำเนาทะเบียนเครื่องจักรและแบบแปลนที่ตั้ง", [DocMachineReg]),
            new("คู่มือการใช้", [DocMachineManual]),
            new("แผนที่ตั้งหลักประกัน", [DocLocMap]),
            new("สำเนาใบสั่งซื้อ (Invoice)", [DocInvoice]),
            new("สำเนาบัตรประชาชนหรือสำเนาหนังสือรับรองของผู้กู้", [DocIdCard, DocBorrowerCert]),
        ]),
        new("เรือ", ["12"],
        [
            new("สำเนาทะเบียนเรือ", [DocBoatReg]),
            new("สำเนาสัญญาต่อเรือ", [DocBoatBuild]),
            new("แผนที่ตั้งหลักประกัน", [DocLocMap]),
            new("สำเนาใบทะเบียนประจำเรือ", [DocBoatCert]),
            new("สำเนาบัตรประชาชนหรือสำเนาหนังสือรับรองของผู้กู้", [DocIdCard, DocBorrowerCert]),
        ]),
        // ⚠ "future construction" has no direct CollateralType code → empty set never ticks (confirm).
        new("ประเมินราคาสิ่งปลูกสร้างในอนาคต", [],
        [
            new("สำเนาแบบแปลนอาคาร", [DocBldgPlan]),
            new("สำเนาบัตรประชาชนหรือสำเนาหนังสือรับรองของผู้กู้", [DocIdCard, DocBorrowerCert]),
            new("สำเนาหนังสือสัญญาจ้างก่อสร้าง", [DocConstructContract]),
            new("แผนที่ตั้งหลักประกัน", [DocLocMap]),
        ]),
    ];
}
