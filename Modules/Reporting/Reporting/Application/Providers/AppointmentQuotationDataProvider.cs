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
    public string ReportTypeKey => "appointment-quotation-request";

    public async Task<object> GetModelAsync(string entityId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(entityId, out var appraisalId))
            throw new NotFoundException("Appraisal", entityId);

        using var connection = connectionFactory.CreateNewConnection();

        // ── 1. Appraisal header + request info ──────────────────────────────────
        const string headerSql = """
            SELECT
                al.CustomerName,
                COALESCE(pPurpose.Description, al.Purpose) AS AppraisalPurpose,
                al.FacilityLimit  AS LoanAmount,
                al.AppraisalType  AS AppraisalType,
                al.RequestId      AS RequestId,
                al.RequestedAt    AS RequestDate,
                r.CreatorName     AS RequesterMakerName
            FROM appraisal.vw_AppraisalList al
            LEFT JOIN request.Requests r ON r.Id = al.RequestId
            LEFT JOIN parameter.Parameters pPurpose
                ON pPurpose.[Group]    = 'AppraisalPurpose'
               AND pPurpose.[Language] = 'TH'
               AND pPurpose.IsActive   = 1
               AND pPurpose.[Code]     = al.Purpose
            WHERE al.Id = @AppraisalId
            """;

        var parameters = new DynamicParameters();
        parameters.Add("AppraisalId", appraisalId);

        var header = await connection.QueryFirstOrDefaultAsync<HeaderRow>(headerSql, parameters);
        if (header is null)
            throw new NotFoundException("Appraisal", entityId);

        // ── 2. Request detail (single collateral address, contact, fee) ─────────
        // Address/Contact/Fee are owned value objects flattened into request.RequestDetails.
        // Codes are resolved to Thai names/descriptions via the shared parameter schema
        // (COALESCE falls back to the raw code if a lookup row is missing). Address geocodes
        // use the Title (per-title-deed) master; Title/Dopa share identical codes+names.
        const string detailSql = """
            SELECT
                rd.HouseNumber,
                rd.ProjectName,
                rd.Soi,
                rd.Road,
                COALESCE(subd.NameTh, rd.SubDistrict) AS SubDistrict,
                COALESCE(dist.NameTh, rd.District)    AS District,
                COALESCE(prov.NameTh, rd.Province)    AS Province,
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
            LEFT JOIN parameter.TitleProvinces    prov ON prov.Code = rd.Province
            LEFT JOIN parameter.TitleDistricts    dist ON dist.Code = rd.District
            LEFT JOIN parameter.TitleSubDistricts subd ON subd.Code = rd.SubDistrict
            WHERE rd.RequestId = @RequestId
            """;

        var detailParams = new DynamicParameters();
        detailParams.Add("RequestId", header.RequestId);

        var detail = await connection.QueryFirstOrDefaultAsync<DetailRow>(detailSql, detailParams);

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

        // ── 4. Collateral new/existing from AppraisalType ───────────────────────
        // AppraisalType values: New | ReAppraisal | Progressive | PreAppraisal.
        bool isNewCollateral =
            !string.Equals(header.AppraisalType, "ReAppraisal", StringComparison.OrdinalIgnoreCase);

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

        // ── Build model ─────────────────────────────────────────────────────────
        var model = new AppointmentQuotationFormModel
        {
            CustomerName = header.CustomerName,
            AppraisalPurpose = header.AppraisalPurpose,
            LoanAmount = header.LoanAmount,
            RequesterMakerName = header.RequesterMakerName,
            RequestDate = header.RequestDate,

            ContactPersonName = detail?.ContactPersonName,
            ContactPersonTel = detail?.ContactPersonTel,
            FeePaymentType = detail?.FeePaymentType,
            OldAppraisalReportNumber = detail?.OldAppraisalReportNumber,

            IsNewCollateral = isNewCollateral,
            PropertyDetail = propertyDetail,
            CollateralType = collateralType,
            ChecklistSections = checklistSections,

            // Property location per title deed (ตามโฉนด). หมู่บ้าน[15] ← ProjectName.
            ProjectName = detail?.ProjectName,
            HouseNumber = detail?.HouseNumber,
            Soi = detail?.Soi,
            Road = detail?.Road,
            SubDistrict = detail?.SubDistrict,
            District = detail?.District,
            Province = detail?.Province,

            // Admin-jurisdiction address (ตามเขตปกครอง), org block, and checker have no
            // data source today → left null → render as blank fill-in lines.

            AttachmentsBySlot = new Dictionary<string, IReadOnlyList<Guid>>
            {
                ["attachments"] = attachmentDocumentIds
            }
        };

        logger.LogDebug(
            "AppointmentQuotation model assembled for appraisal {AppraisalId}: {PropertyTypeCount} distinct property types, {AttachmentCount} PDF attachments",
            appraisalId, propertyRows.Count, attachmentDocumentIds.Count);

        return model;
    }

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
        public string? AppraisalType { get; init; }
        public Guid RequestId { get; init; }
        public DateTime? RequestDate { get; init; }
        public string? RequesterMakerName { get; init; }
    }

    private sealed class DetailRow
    {
        public string? HouseNumber { get; init; }
        public string? ProjectName { get; init; }
        public string? Soi { get; init; }
        public string? Road { get; init; }
        public string? SubDistrict { get; init; }
        public string? District { get; init; }
        public string? Province { get; init; }
        public string? ContactPersonName { get; init; }
        public string? ContactPersonTel { get; init; }
        public string? FeePaymentType { get; init; }
        public string? OldAppraisalReportNumber { get; init; }
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
