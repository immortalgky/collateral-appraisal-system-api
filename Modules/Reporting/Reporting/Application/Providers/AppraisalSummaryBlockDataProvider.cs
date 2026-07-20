using Reporting.Application.Formatting;
using Reporting.Application.Models;
using Reporting.Application.Services;

namespace Reporting.Application.Providers;

/// <summary>
/// Assembles an <see cref="AppraisalSummaryModel"/> for FSD §2.1.5
/// "ใบสรุปรายงานการประเมิน – Block/Project (หมู่บ้านจัดสรร / คอนโดมิเนียม)".
///
/// Common queries (Q1–Q14 + ColTypeMap) are delegated to
/// <see cref="AppraisalSummaryCommonLoader"/>. This provider adds only block-specific
/// detail queries:
///
///   QB1  appraisal.Projects — project header + address + utilities/facilities
///   QB2  appraisal.ProjectModels — house/unit models for ProjectDetails text
///   QB3  appraisal.ProjectUnits LEFT JOIN ProjectUnitPrices — per-unit price rows
///
/// Column notes (confirmed from ProjectConfiguration.cs + ProjectUnitConfiguration.cs
/// + ProjectUnitPriceConfiguration.cs + ProjectModelConfiguration.cs):
///   Projects: ProjectName, Developer, ProjectDescription, ProjectSaleLaunchDate,
///     LandAreaRai/Ngan/SquareWa, UnitForSaleCount, NumberOfPhase, LandOffice,
///     HouseNumber, Soi, Road, SubDistrict/District/Province (HasColumnName),
///     Latitude/Longitude (HasColumnName), Utilities/Facilities (nvarchar(1000) JSON),
///     UtilitiesOther, FacilitiesOther, ProjectType (nvarchar code "U"/"LB"/"L").
///   ProjectModels: ModelName, BuildingType, NumberOfFloors, NumberOfHouse,
///     StartingPriceMin, StartingPriceMax, UsableAreaMin, UsableAreaMax, StandardUsableArea.
///   ProjectUnits: SequenceNumber, PlotNumber, HouseNumber, ModelType, LandArea,
///     UsableArea, SellingPrice, Floor (int?), TowerName, RoomNumber, IsSold.
///   ProjectUnitPrices: TotalAppraisalValueRounded, ForceSellingPrice, CoverageAmount,
///     StandardPrice (used as PricePerSqm for Condo).
///
/// Deferred / not stored:
///   - Condition/Remark at project level: comes from common AppraisalDecisions.
///   - CollateralAddress for the committee partial: composed from project address fields.
/// </summary>
public sealed class AppraisalSummaryBlockDataProvider(
    ISqlConnectionFactory connectionFactory,
    ILogger<AppraisalSummaryBlockDataProvider> logger)
    : IReportDataProvider
{
    public string ReportTypeKey => "appraisal-summary-block";

    public async Task<object> GetModelAsync(string entityId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(entityId, out var appraisalId))
            throw new NotFoundException("Appraisal", entityId);

        using var connection = connectionFactory.CreateNewConnection();
        var model = await BuildAsync(connection, appraisalId, cancellationToken);

        logger.LogDebug(
            "AppraisalSummaryBlock model assembled for appraisal {AppraisalId}: " +
            "buildingUnits={BuildingUnits}, condoUnits={CondoUnits}",
            appraisalId,
            model.BuildingUnits.Count,
            model.CondoUnits.Count);

        return model;
    }

    /// <summary>
    /// Builds an <see cref="AppraisalSummaryModel"/> from an open connection.
    /// Called by <see cref="GetModelAsync"/> (standalone summary) and by
    /// <c>AppraisalBookDataProvider</c> (unified appraisal book, "block" body).
    /// </summary>
    internal static async Task<AppraisalSummaryModel> BuildAsync(
        System.Data.IDbConnection connection,
        Guid appraisalId,
        CancellationToken ct)
    {
        // ── Common data (Q1–Q14 + ColTypeMap) ───────────────────────────────────
        var common = await AppraisalSummaryCommonLoader.LoadAsync(connection, appraisalId, ct);
        if (common is null)
            throw new NotFoundException("Appraisal", appraisalId.ToString());

        // ── Batch: 3 block-specific result sets, single round-trip ──────────────
        //
        // QB1 (RS01) — Project header. All columns off @AppraisalId.
        // QB2 (RS02) — Project models. Rewritten to derive ProjectId via subquery so the
        //              entire batch needs only @AppraisalId (avoids a second round-trip
        //              to capture project.ProjectId first).
        // QB3 (RS03) — Unit prices. Same subquery pattern as QB2.
        //
        // Column names confirmed against ProjectConfiguration.cs:
        //   Address VO: HasColumnName("SubDistrict"/"District"/"Province")
        //   Coordinates VO: HasColumnName("Latitude"/"Longitude")
        //   Utilities/Facilities: nvarchar(1000) JSON; decoded in C# below.
        //   ProjectType: nvarchar code ("U"/"LB"/"L")
        //   LandOffice: direct column on Projects (not Address VO)
        const string batchSql = """
            -- RS01: QB1 — Project header row
            SELECT
                p.ProjectName,
                p.Developer,
                p.ProjectDescription,
                p.ProjectSaleLaunchDate,
                p.LandAreaRai,
                p.LandAreaNgan,
                p.LandAreaSquareWa,
                p.UnitForSaleCount,
                p.NumberOfPhase,
                COALESCE(pLandOffice.[description], p.LandOffice) AS LandOffice,
                p.HouseNumber,
                p.Soi,
                p.Road,
                COALESCE(tsub.NameTh,  p.SubDistrict) AS SubDistrict,
                COALESCE(tdist.NameTh, p.District)    AS District,
                COALESCE(tprov.NameTh, p.Province)    AS Province,
                p.Latitude,
                p.Longitude,
                p.Utilities,
                p.UtilitiesOther,
                p.Facilities,
                p.FacilitiesOther,
                p.ProjectType,
                p.Id            AS ProjectId
            FROM appraisal.Projects p
            LEFT JOIN parameter.TitleProvinces    tprov ON tprov.Code = p.Province
            LEFT JOIN parameter.TitleDistricts    tdist ON tdist.Code = p.District
            LEFT JOIN parameter.TitleSubDistricts tsub  ON tsub.Code  = p.SubDistrict
            LEFT JOIN parameter.Parameters pLandOffice
                ON pLandOffice.[group]    = 'LandOffice'
               AND pLandOffice.[language] = 'TH'
               AND pLandOffice.[isactive] = 1
               AND pLandOffice.[code]     = p.LandOffice
            WHERE p.AppraisalId = @AppraisalId;

            -- RS02: QB2 — House/unit models (derive ProjectId via subquery)
            -- Confirmed columns from ProjectModelConfiguration.cs.
            SELECT
                pm.ModelName,
                pm.BuildingType,
                pm.NumberOfFloors,
                pm.NumberOfHouse,
                pm.StartingPriceMin,
                pm.StartingPriceMax,
                pm.UsableAreaMin,
                pm.UsableAreaMax,
                pm.StandardUsableArea
            FROM appraisal.ProjectModels pm
            WHERE pm.ProjectId = (
                SELECT p2.Id FROM appraisal.Projects p2
                WHERE p2.AppraisalId = @AppraisalId)
            ORDER BY pm.ModelName;

            -- RS03: QB3 — Per-unit rows with prices (unsold only)
            -- LEFT JOIN so units without a price row still appear (price columns null).
            -- Filter IsSold = 0 to match the unit-price listing convention.
            SELECT
                u.SequenceNumber,
                u.PlotNumber,
                u.HouseNumber,
                u.ModelType,
                u.LandArea,
                u.UsableArea,
                u.SellingPrice,
                u.Floor,
                u.TowerName,
                u.RoomNumber,
                up.TotalAppraisalValueRounded  AS AppraisalValue,
                up.StandardPrice               AS PricePerSqm,
                up.ForceSellingPrice           AS ForcedSaleValue,
                up.CoverageAmount
            FROM appraisal.ProjectUnits u
            LEFT JOIN appraisal.ProjectUnitPrices up ON up.ProjectUnitId = u.Id
            WHERE u.ProjectId = (
                SELECT p3.Id FROM appraisal.Projects p3
                WHERE p3.AppraisalId = @AppraisalId)
              AND u.IsSold = 0
            ORDER BY u.SequenceNumber;

            -- RS04: distinct request property types (ประเภททรัพย์สิน — same as land-building)
            SELECT DISTINCT rp.PropertyType
            FROM request.RequestProperties rp
            WHERE rp.RequestId = (
                SELECT a.RequestId FROM appraisal.Appraisals a
                WHERE a.Id = @AppraisalId AND a.IsDeleted = 0)
              AND rp.PropertyType IS NOT NULL;

            -- RS05: combined selected pricing approaches across ALL project models (วิธีการประเมิน)
            SELECT DISTINCT paa.ApproachType
            FROM appraisal.PricingAnalysisApproaches paa
            JOIN appraisal.PricingAnalysis pa ON pa.Id = paa.PricingAnalysisId AND pa.SubjectType = 1
            JOIN appraisal.ProjectModels pm ON pm.Id = pa.AnchorId
            WHERE pm.ProjectId = (SELECT p.Id FROM appraisal.Projects p WHERE p.AppraisalId = @AppraisalId)
              AND paa.IsSelected = 1;

            -- RS06: utility/facility code → Thai description maps
            SELECT [group] AS [Group], [code] AS [Code], [description] AS [Description]
            FROM parameter.Parameters
            WHERE [group] IN ('PublicUtility', 'Facilities')
              AND [language] = 'TH' AND [isactive] = 1;
            """;

        var batchParams = new DynamicParameters();
        batchParams.Add("AppraisalId", appraisalId);

        ProjectRow? project;
        List<ProjectModelRow> modelRows;
        List<UnitPriceRow> unitRows;
        List<string> requestPropertyTypes;
        List<string> modelApproachTypes;
        List<ParamRow> utilityFacilityParams;

        using (var multi = await connection.QueryMultipleAsync(batchSql, batchParams))
        {
            // RS01: QB1 — project header
            project = await multi.ReadFirstOrDefaultAsync<ProjectRow>();

            // RS02: QB2 — project models
            modelRows = (await multi.ReadAsync<ProjectModelRow>()).ToList();

            // RS03: QB3 — unit price rows
            unitRows = (await multi.ReadAsync<UnitPriceRow>()).ToList();

            // RS04: distinct request property types
            requestPropertyTypes = (await multi.ReadAsync<string>()).ToList();

            // RS05: combined approaches across project models
            modelApproachTypes = (await multi.ReadAsync<string>()).ToList();

            // RS06: utility/facility param maps
            utilityFacilityParams = (await multi.ReadAsync<ParamRow>()).ToList();
        }

        var utilityMap = utilityFacilityParams
            .Where(p => string.Equals(p.Group, "PublicUtility", StringComparison.OrdinalIgnoreCase) && p.Code is not null)
            .GroupBy(p => p.Code!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Description, StringComparer.OrdinalIgnoreCase);
        var facilityMap = utilityFacilityParams
            .Where(p => string.Equals(p.Group, "Facilities", StringComparison.OrdinalIgnoreCase) && p.Code is not null)
            .GroupBy(p => p.Code!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Description, StringComparer.OrdinalIgnoreCase);

        // A block appraisal must have a Project; if absent the appraisal exists but is not a block type.
        if (project is null)
            throw new NotFoundException("Project", appraisalId.ToString());

        // ── Build composed fields ────────────────────────────────────────────────

        // Project address — drop any label whose value is empty (same as the other forms).
        var projectAddress = ThaiAddressFormatter.FormatLandBuilding(
            houseNumber: project.HouseNumber,
            village: null,
            moo: null,
            soi: project.Soi,
            road: project.Road,
            subDistrict: project.SubDistrict,
            district: project.District,
            province: project.Province);
        if (string.IsNullOrWhiteSpace(projectAddress))
            projectAddress = null;

        // Committee table รายการทรัพย์สิน (block): ชื่อโครงการ + ที่ตั้งโครงการ.
        var approvalPropertyText = string.Join(" ",
            new[] { project.ProjectName, projectAddress }.Where(s => !string.IsNullOrWhiteSpace(s)));

        // GPS
        var gps = ThaiAddressFormatter.FormatGps(project.Latitude, project.Longitude);

        // Utilities + Facilities: JSON array → comma-joined, appended with free-text Other
        var utilities = BuildUtilityDisplay(JsonArrayToDisplay(project.Utilities, utilityMap), project.UtilitiesOther);
        var facilities = BuildUtilityDisplay(JsonArrayToDisplay(project.Facilities, facilityMap), project.FacilitiesOther);

        // ProjectDetails: composed narrative from type + area + model list
        var projectDetails = BuildProjectDetails(project, modelRows);

        // วิธีการประเมิน — combine the selected approaches from all project models.
        var methodFlags = AppraisalSummaryCommonLoader.BuildMethodFlags(modelApproachTypes);

        // Determine ProjectType code ("U" / "LB" / "L")
        var projectTypeCode = project.ProjectType?.Trim().ToUpperInvariant();

        // Property type — distinct Request property types translated to Thai (same as the
        // land-building form); fall back to the project-type code description.
        var requestTypeLabel = string.Join(", ",
            requestPropertyTypes
                .Select(common.TranslateCollateralType)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct());
        var projectTypeLabel = projectTypeCode switch
        {
            "U"  => "อาคารชุด/ห้องชุด",
            "LB" => "บ้านพร้อมที่ดิน",
            "L"  => "ที่ดินจัดสรร",
            _    => projectTypeCode
        };
        var propertyTypeDisplay = !string.IsNullOrWhiteSpace(requestTypeLabel)
            ? requestTypeLabel
            : projectTypeLabel;

        // Per-unit table rows — branch on ProjectType
        var buildingUnits = new List<BlockBuildingUnitRow>();
        var condoUnits    = new List<BlockCondoUnitRow>();

        bool isCondo = string.Equals(projectTypeCode, "U", StringComparison.OrdinalIgnoreCase);

        foreach (var u in unitRows)
        {
            if (isCondo)
            {
                condoUnits.Add(new BlockCondoUnitRow
                {
                    Sequence       = u.SequenceNumber,
                    Floor          = u.Floor,
                    TowerName      = u.TowerName,
                    RoomNumber     = u.RoomNumber,
                    ModelType      = u.ModelType,
                    UsableArea     = u.UsableArea,
                    AppraisalValue = u.AppraisalValue,
                    PricePerSqm    = u.PricePerSqm,
                    ForcedSaleValue = u.ForcedSaleValue,
                    CoverageAmount = u.CoverageAmount
                });
            }
            else
            {
                buildingUnits.Add(new BlockBuildingUnitRow
                {
                    Sequence        = u.SequenceNumber,
                    PlotNumber      = u.PlotNumber,
                    HouseNumber     = u.HouseNumber,
                    ModelType       = u.ModelType,
                    LandArea        = u.LandArea,
                    UsableArea      = u.UsableArea,
                    SellingPrice    = u.SellingPrice,
                    AppraisalValue  = u.AppraisalValue,
                    ForcedSaleValue = u.ForcedSaleValue,
                    CoverageAmount  = u.CoverageAmount
                });
            }
        }

        // TotalAppraisalValue for committee partial: prefer ValuationAnalyses total, then sum units
        var totalAppraisalValue = common.TotalAppraisalValue;
        if (totalAppraisalValue is null && unitRows.Count > 0)
        {
            var unitSum = unitRows.Sum(u => u.AppraisalValue ?? 0m);
            if (unitSum > 0) totalAppraisalValue = unitSum;
        }

        // ── Build model ──────────────────────────────────────────────────────────
        var model = new AppraisalSummaryModel
        {
            AppraisalBookNumber = common.AppraisalNumber,
            AppraisalDate       = common.AppraisalDate,
            CustomerName        = common.CustomerName,
            AoName              = common.AoName,
            AppraisalPurpose    = common.AppraisalPurpose,
            PropertyType        = propertyTypeDisplay,
            // Field 22 — opinion-section property type: the project kind only, not the
            // mix of Request collateral types shown in the header.
            SummaryPropertyType = projectTypeLabel,

            // ที่ตั้งทรัพย์สิน from the Request detail (same as the land-building form)
            CollateralAddress       = common.CollateralAddress,
            AdministrativeDistrict  = project.SubDistrict,
            LandOffice              = project.LandOffice,
            OldAppraisalValue       = common.PrevAppraisedValue,
            HasPrevAppraisal        = common.HasPrevAppraisal,
            IsReAppraisal           = string.Equals(common.AppraisalType, "ReAppraisal", StringComparison.OrdinalIgnoreCase),

            Appraiser  = common.Appraiser,
            LoanValue  = common.LoanValue,

            // Groups table is not used in the block template (superseded by BuildingUnits/CondoUnits)
            Groups             = [],
            TotalAppraisalValue = totalAppraisalValue,
            BuildingCoverageAmount = common.BuildingCoverageAmount,
            ForcedSaleValue    = common.ForcedSaleValue,
            Condition          = common.Condition,
            Remark             = common.Remark,

            // Block-variant specific fields
            ProjectName           = project.ProjectName,
            ProjectAddress        = projectAddress,
            Developer             = project.Developer,
            ProjectDetails        = projectDetails,
            ProjectSaleLaunchDate = FormatThaiPartialDate(project.ProjectSaleLaunchDate),
            Utilities             = utilities,
            Facilities            = facilities,
            AppraisalValueWording =
                "รายละเอียดดูตามสรุปราคาประเมินรายแปลง เพื่อสนับสนุนรายย่อย",
            BuildingUnits = buildingUnits,
            CondoUnits    = condoUnits,

            // Attribute block
            Gps                    = gps,
            LandOwner              = null, // No owner field on Project aggregate
            EntryExitRights        = null,
            BuildingOwner          = null,
            LandCondition          = null,
            Obligation             = null,
            CityPlan               = null,
            GovernmentAssessedValue = null,
            Utilization            = null,

            // วิธีการประเมิน — combined approaches across all project models
            IsWqs       = methodFlags.IsWqs,
            IsSaleGrid  = methodFlags.IsSaleGrid,
            IsCost      = methodFlags.IsCost,
            IsIncome    = methodFlags.IsIncome,
            IsHypothesis = methodFlags.IsHypothesis,
            IsLeasehold  = methodFlags.IsLeasehold,
            IsProfitRent = methodFlags.IsProfitRent,

            AppraiserComment        = common.AppraiserComment,
            AppraisalStaffName      = common.StaffName,
            AppraisalStaffPosition  = common.StaffPosition,
            AppraisalCheckerName    = common.CheckerName,
            AppraisalCheckerPosition = common.CheckerPosition,
            AppraisalVerifyName     = common.VerifyName,
            AppraisalVerifyPosition = common.VerifyPosition,

            MeetingNumber           = common.Review?.MeetingNo,
            MeetingDate             = common.Review?.MeetingDate,
            ApprovalDate            = common.ApprovalDate,
            IsCompleted             = common.IsCompleted,
            ShowMeeting             = common.ShowMeeting,
            ApproverDecisionApproved = common.ApproverDecisionApproved,
            Approvers               = common.Approvers,
            ApproverSummaryComment  = common.CommitteeOpinion,
            ApprovalValueText       = "ตามแนบ",
            ApprovalPropertyText    = string.IsNullOrWhiteSpace(approvalPropertyText) ? null : approvalPropertyText
        };

        return model;
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Composes the ลักษณะโครงการ text from project header and model list.
    /// Format (best-effort, matches FSD §2.1.5):
    ///   {typeDesc} พื้นที่โครงการประมาณ {Rai}-{Ngan}-{Wa} ไร่
    ///   จำนวนแปลงขาย {UnitForSaleCount} ยูนิต/แปลง
    ///   มีแบบบ้าน/ห้องชุด {N} แบบ ได้แก่
    ///   1.) {ModelName} {BuildingType} {floors} ชั้น พื้นที่ใช้สอย {area} ตร.ม. ราคาเริ่มต้น {price} บาท
    ///   2.) ...
    /// </summary>
    private static string? BuildProjectDetails(ProjectRow project, List<ProjectModelRow> models)
    {
        var parts = new System.Text.StringBuilder();

        var typeDesc = project.ProjectType?.Trim().ToUpperInvariant() switch
        {
            "U"  => "อาคารชุด",
            "LB" => "โครงการบ้านจัดสรร",
            "L"  => "โครงการที่ดินจัดสรร",
            _    => "โครงการ"
        };
        parts.Append(typeDesc);

        var hasArea = project.LandAreaRai.GetValueOrDefault() > 0
            || project.LandAreaNgan.GetValueOrDefault() > 0
            || project.LandAreaSquareWa.GetValueOrDefault() > 0;

        if (hasArea)
        {
            parts.Append(
                $" พื้นที่โครงการประมาณ " +
                $"{project.LandAreaRai.GetValueOrDefault():0.##}-{project.LandAreaNgan.GetValueOrDefault():0.##}-{project.LandAreaSquareWa.GetValueOrDefault():0.##} ไร่");
        }

        if (project.UnitForSaleCount.HasValue && project.UnitForSaleCount.Value > 0)
            parts.Append($" จำนวนแปลงขาย {project.UnitForSaleCount} ยูนิต/แปลง");

        if (models.Count > 0)
        {
            var unitWord = string.Equals(
                project.ProjectType?.Trim(), "U", StringComparison.OrdinalIgnoreCase)
                ? "แบบห้องชุด"
                : "แบบบ้าน";

            parts.Append($" มี{unitWord} {models.Count} แบบ ได้แก่");

            for (int i = 0; i < models.Count; i++)
            {
                var m = models[i];
                var modelParts = new List<string>();

                if (!string.IsNullOrWhiteSpace(m.ModelName))
                    modelParts.Add(m.ModelName);

                if (!string.IsNullOrWhiteSpace(m.BuildingType))
                    modelParts.Add(m.BuildingType);

                if (m.NumberOfFloors.HasValue && m.NumberOfFloors.Value > 0)
                    modelParts.Add($"{m.NumberOfFloors:0.##} ชั้น");

                if (m.NumberOfHouse.HasValue && m.NumberOfHouse.Value > 0)
                    modelParts.Add($"จำนวน {m.NumberOfHouse} หลัง");

                // Area: prefer StandardUsableArea, fall back to UsableAreaMin
                var area = m.StandardUsableArea ?? m.UsableAreaMin;
                if (area.HasValue && area.Value > 0)
                    modelParts.Add($"พื้นที่ใช้สอย {area:0.##} ตร.ม.");

                // Price range
                if (m.StartingPriceMin.HasValue && m.StartingPriceMax.HasValue
                    && m.StartingPriceMin.Value > 0)
                {
                    if (m.StartingPriceMin == m.StartingPriceMax)
                        modelParts.Add($"ราคาเริ่มต้น {m.StartingPriceMin:N0} บาท");
                    else
                        modelParts.Add($"ราคา {m.StartingPriceMin:N0}–{m.StartingPriceMax:N0} บาท");
                }
                else if (m.StartingPriceMin.HasValue && m.StartingPriceMin.Value > 0)
                {
                    modelParts.Add($"ราคาเริ่มต้น {m.StartingPriceMin:N0} บาท");
                }

                if (modelParts.Count > 0)
                    parts.Append($"\n{i + 1}.) {string.Join(" ", modelParts)}");
            }
        }

        var result = parts.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    /// <summary>
    /// Combines a decoded JSON-array display string with a free-text "Other" suffix.
    /// Returns null when both inputs are blank.
    /// </summary>
    // วันที่เปิดขายโครงการ may be partial: "YYYY", "YYYY-MM", or "YYYY-MM-DD" (Gregorian).
    // Show only the parts present, converting the year to Buddhist era.
    private static readonly string[] ThaiMonths =
    {
        "", "มกราคม", "กุมภาพันธ์", "มีนาคม", "เมษายน", "พฤษภาคม", "มิถุนายน",
        "กรกฎาคม", "สิงหาคม", "กันยายน", "ตุลาคม", "พฤศจิกายน", "ธันวาคม"
    };

    private static string? FormatThaiPartialDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var parts = raw.Trim().Split('-');
        if (!int.TryParse(parts[0], out var year)) return null;
        var beYear = year + 543;

        if (parts.Length >= 3
            && int.TryParse(parts[1], out var m) && m is >= 1 and <= 12
            && int.TryParse(parts[2], out var d) && d >= 1)
            return $"{d} {ThaiMonths[m]} {beYear}";

        if (parts.Length >= 2 && int.TryParse(parts[1], out var m2) && m2 is >= 1 and <= 12)
            return $"{ThaiMonths[m2]} {beYear}";

        return beYear.ToString();
    }

    private static string? BuildUtilityDisplay(string? decoded, string? other)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(decoded)) parts.Add(decoded.Trim());
        if (!string.IsNullOrWhiteSpace(other))   parts.Add(other.Trim());
        var joined = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    /// <summary>
    /// Decodes a nvarchar(1000) column that EF stores as a JSON array of strings.
    /// Returns a comma-joined display string, falls back to the raw value for non-JSON,
    /// and null for empty/null input.
    /// </summary>
    private static string? JsonArrayToDisplay(string? raw, IReadOnlyDictionary<string, string?> codeMap)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var s = raw.Trim();
        if (!s.StartsWith('['))
            return s;

        try
        {
            var items = System.Text.Json.JsonSerializer.Deserialize<List<string>>(s);
            if (items is null || items.Count == 0)
                return null;
            // Translate each code to its description; fall back to the code if unmapped.
            var joined = string.Join(", ", items
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(c => codeMap.TryGetValue(c, out var d) && !string.IsNullOrWhiteSpace(d) ? d : c));
            return string.IsNullOrWhiteSpace(joined) ? null : joined;
        }
        catch (System.Text.Json.JsonException)
        {
            return s;
        }
    }

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    private sealed class ProjectRow
    {
        public string? ProjectName       { get; init; }
        public string? Developer         { get; init; }
        public string? ProjectDescription { get; init; }
        public string? ProjectSaleLaunchDate { get; init; }
        public decimal? LandAreaRai      { get; init; }
        public decimal? LandAreaNgan     { get; init; }
        public decimal? LandAreaSquareWa { get; init; }
        public int?     UnitForSaleCount { get; init; }
        public int?     NumberOfPhase    { get; init; }
        public string?  LandOffice       { get; init; }
        public string?  HouseNumber      { get; init; }
        public string?  Soi              { get; init; }
        public string?  Road             { get; init; }
        public string?  SubDistrict      { get; init; }
        public string?  District         { get; init; }
        public string?  Province         { get; init; }
        public decimal? Latitude         { get; init; }
        public decimal? Longitude        { get; init; }
        // Raw JSON column values — decoded in C# to avoid SQL JSON_VALUE
        public string?  Utilities        { get; init; }
        public string?  UtilitiesOther   { get; init; }
        public string?  Facilities       { get; init; }
        public string?  FacilitiesOther  { get; init; }
        public string?  ProjectType      { get; init; }
        public Guid     ProjectId        { get; init; }
    }

    private sealed class ProjectModelRow
    {
        public string?  ModelName         { get; init; }
        public string?  BuildingType      { get; init; }
        public decimal? NumberOfFloors    { get; init; }
        public int?     NumberOfHouse     { get; init; }
        public decimal? StartingPriceMin  { get; init; }
        public decimal? StartingPriceMax  { get; init; }
        public decimal? UsableAreaMin     { get; init; }
        public decimal? UsableAreaMax     { get; init; }
        public decimal? StandardUsableArea { get; init; }
    }

    private sealed class ParamRow
    {
        public string? Group { get; init; }
        public string? Code { get; init; }
        public string? Description { get; init; }
    }

    private sealed class UnitPriceRow
    {
        public int      SequenceNumber  { get; init; }
        // LB-side
        public string?  PlotNumber      { get; init; }
        public string?  HouseNumber     { get; init; }
        // Common
        public string?  ModelType       { get; init; }
        public decimal? LandArea        { get; init; }
        public decimal? UsableArea      { get; init; }
        public decimal? SellingPrice    { get; init; }
        // Condo-side
        public int?     Floor           { get; init; }
        public string?  TowerName       { get; init; }
        public string?  RoomNumber      { get; init; }
        // Prices (from LEFT JOIN ProjectUnitPrices)
        public decimal? AppraisalValue  { get; init; }
        public decimal? PricePerSqm     { get; init; }  // StandardPrice column
        public decimal? ForcedSaleValue { get; init; }
        public decimal? CoverageAmount  { get; init; }
    }
}
