using Reporting.Application.Models;
using Reporting.Application.Services;

namespace Reporting.Application.Providers;

/// <summary>
/// Assembles an <see cref="AppointmentQuotationFormModel"/> from the database
/// using Dapper + existing views/tables.
///
/// Data sourcing strategy (all reads are read-only, no EF tracking):
///   - appraisal.vw_AppraisalList  → appraisal header, purpose, customer name
///   - request.Requests            → requestor details, fee payment type
///   - request.RequestCustomers    → customer name (fallback)
///   - request.RequestProperties   → property rows (type, building type, addresses)
///   - appraisal.vw_AppointmentList → contact person (from latest appointment)
///   - request.RequestDetail       → loan amount, addresses, checker/maker, fee type
///
/// IMPORTANT: Fields that are not yet directly exposed via a single view are
/// queried from the underlying request tables. If a field is not present,
/// it defaults to null and the template renders gracefully (no crash).
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
        // vw_AppraisalList already exposes CustomerName/Purpose/FacilityLimit/
        // AppraisalType/RequestId/RequestedAt. The requester (maker) display name
        // is the request Creator's denormalised name (request.Requests.CreatorName).
        // There is no "checker" concept on requests today → left null.
        const string headerSql = """
            SELECT
                al.CustomerName,
                al.Purpose        AS AppraisalPurpose,
                al.FacilityLimit  AS LoanAmount,
                al.AppraisalType  AS AppraisalType,
                al.RequestId      AS RequestId,
                al.RequestedAt    AS RequestDate,
                r.CreatorName     AS RequesterMakerName
            FROM appraisal.vw_AppraisalList al
            LEFT JOIN request.Requests r ON r.Id = al.RequestId
            WHERE al.Id = @AppraisalId
            """;

        var parameters = new DynamicParameters();
        parameters.Add("AppraisalId", appraisalId);

        var header = await connection.QueryFirstOrDefaultAsync<HeaderRow>(headerSql, parameters);
        if (header is null)
            throw new NotFoundException("Appraisal", entityId);

        // ── 2. Request detail (addresses, fee, contact) ─────────────────────────
        // Address/Contact/Fee are owned value objects flattened into
        // request.RequestDetails (PK/FK = RequestId). NOTE: Division / Department /
        // Cost Center / Referrer are NOT modelled in the schema today, so those
        // form fields are left blank in v1.
        const string detailSql = """
            SELECT
                rd.HouseNumber,
                rd.ProjectName,
                rd.Soi,
                rd.Road,
                rd.SubDistrict,
                rd.District,
                rd.Province,
                rd.ContactPersonName,
                rd.ContactPersonPhone  AS ContactPersonTel,
                rd.FeePaymentType,
                rd.PrevAppraisalNumber AS OldAppraisalReportNumber
            FROM request.RequestDetails rd
            WHERE rd.RequestId = @RequestId
            """;

        var detailParams = new DynamicParameters();
        detailParams.Add("RequestId", header.RequestId);

        var detail = await connection.QueryFirstOrDefaultAsync<DetailRow>(detailSql, detailParams);

        // ── 3. Property rows ────────────────────────────────────────────────────
        // request.RequestProperties only carries PropertyType/BuildingType/SellingPrice;
        // there are no per-property address columns (location lives on RequestDetails).
        const string propertiesSql = """
            SELECT
                ROW_NUMBER() OVER (ORDER BY rp.Id) AS RowNumber,
                rp.PropertyType,
                rp.BuildingType
            FROM request.RequestProperties rp
            WHERE rp.RequestId = @RequestId
            ORDER BY rp.Id
            """;

        var propertyRows = (await connection.QueryAsync<PropertyRowFlat>(propertiesSql, detailParams))
            .ToList();

        // ── 4. Collateral new/existing from AppraisalType (already on the header) ─
        // AppraisalType values: New | ReAppraisal | Progressive | PreAppraisal.
        bool isNewCollateral =
            !string.Equals(header.AppraisalType, "ReAppraisal", StringComparison.OrdinalIgnoreCase);

        // ── 5. Uploaded PDF attachments linked to the request ───────────────────
        // Documents attach to the Request (not the Appraisal). Only merge real PDFs —
        // PDFsharp imports pages, so images/other types are excluded by MimeType.
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

        // ── Build model ─────────────────────────────────────────────────────────
        var properties = propertyRows.Select(p => new PropertyRow
        {
            RowNumber = p.RowNumber,
            PropertyType = p.PropertyType,
            BuildingType = p.BuildingType,
            Village = p.Village,
            HouseNumber = p.HouseNumber,
            Soi = p.Soi,
            Road = p.Road,
            SubDistrict = p.SubDistrict,
            District = p.District,
            Province = p.Province
        }).ToList();

        // Fall back to the first property address if no dedicated collateral address
        var firstProp = propertyRows.FirstOrDefault();

        var model = new AppointmentQuotationFormModel
        {
            CustomerName = header.CustomerName,
            AppraisalPurpose = header.AppraisalPurpose,
            LoanAmount = header.LoanAmount,
            RequesterMakerName = header.RequesterMakerName ?? detail?.ReferrerName,
            RequestDate = header.RequestDate,
            RequesterCheckerName = header.RequesterCheckerName,

            Division = detail?.Division,
            Department = detail?.Department,
            CostCenter = detail?.CostCenter,
            ReferrerName = detail?.ReferrerName,
            ReferrerTel = detail?.ReferrerTel,
            ContactPersonName = detail?.ContactPersonName,
            ContactPersonTel = detail?.ContactPersonTel,
            FeePaymentType = detail?.FeePaymentType,
            OldAppraisalReportNumber = detail?.OldAppraisalReportNumber,

            // Primary address: from detail if present, else first property
            HouseNumber = detail?.HouseNumber ?? firstProp?.HouseNumber,
            ProjectName = detail?.ProjectName ?? firstProp?.Village,
            Soi = detail?.Soi ?? firstProp?.Soi,
            Road = detail?.Road ?? firstProp?.Road,
            SubDistrict = detail?.SubDistrict ?? firstProp?.SubDistrict,
            District = detail?.District ?? firstProp?.District,
            Province = detail?.Province ?? firstProp?.Province,

            IsNewCollateral = isNewCollateral,
            Properties = properties,

            // Attachment slot → the request's uploaded PDFs (appended after the form).
            AttachmentsBySlot = new Dictionary<string, IReadOnlyList<Guid>>
            {
                ["attachments"] = attachmentDocumentIds
            }
        };

        logger.LogDebug(
            "AppointmentQuotation model assembled for appraisal {AppraisalId}: {PropertyCount} properties, {AttachmentCount} PDF attachments",
            appraisalId, properties.Count, attachmentDocumentIds.Count);

        return model;
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
        public string? RequesterCheckerName { get; init; }
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
        public string? Department { get; init; }
        public string? CostCenter { get; init; }
        public string? Division { get; init; }
        public string? ReferrerName { get; init; }
        public string? ReferrerTel { get; init; }
        public string? OldAppraisalReportNumber { get; init; }
    }

    private sealed class PropertyRowFlat
    {
        public int RowNumber { get; init; }
        public string? PropertyType { get; init; }
        public string? BuildingType { get; init; }
        public string? Village { get; init; }
        public string? HouseNumber { get; init; }
        public string? Soi { get; init; }
        public string? Road { get; init; }
        public string? SubDistrict { get; init; }
        public string? District { get; init; }
        public string? Province { get; init; }
    }
}
