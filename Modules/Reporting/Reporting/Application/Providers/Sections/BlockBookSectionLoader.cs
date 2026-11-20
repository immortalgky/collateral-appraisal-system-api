using System.Data;
using Reporting.Application.Formatting;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the <see cref="BlockBookSection"/> — the project pages of the appraisal book.
///
/// One Dapper batch off <c>@AppraisalId</c>, read-only, on the Project aggregate tables:
///   RS01  parameter.Parameters (TH)               code → Thai
///   RS02  appraisal.Projects                      project header
///   RS03  appraisal.ProjectLands                  project land (1:1)
///   RS04  appraisal.ProjectLandTitles             title documents
///   RS05  appraisal.ProjectModels                 models + the model's PricingAnalysis value
///   RS06  appraisal.ProjectModelDepreciationDetails   cost rows
///   RS07  appraisal.ProjectUnits + ProjectUnitPrices  unit rows
///   RS08  appraisal.ProjectPricingAssumptions     adjustments
///   RS09  appraisal.PricingAnalysis (SubjectType = 1)  remarks
///
/// Returns null when the appraisal has no Projects row.
/// </summary>
internal static class BlockBookSectionLoader
{
    public static async Task<BlockBookSection?> LoadAsync(
        IDbConnection connection, Guid appraisalId, Func<DateTime, DateTime> toApplicationTime, CancellationToken ct = default)
    {
        const string batchSql = """
            -- RS01: code -> Thai
            SELECT [group] AS [Group], [code] AS [Code], [description] AS [Description]
            FROM parameter.Parameters
            WHERE [language] = 'TH' AND [isactive] = 1
              AND [group] IN ('CheckBy', 'RoadSurface', 'PublicUtility', 'Transportation', 'Location',
                              'TypeOfUrbanPlanning', 'UrbanPlanningColor', 'AnticipationOfProsperity', 'Eviction',
                              'BuildingType', 'Decoration', 'RoomLayout', 'GeneralStructure', 'RoofFrame',
                              'Facilities', 'Roof', 'GroundFlooringMaterials', 'UpperFlooringMaterials',
                              'BathroomFlooringMaterials', 'Ceiling', 'Interior', 'Exterior', 'Environment');

            -- RS02: project header
            SELECT p.Id, p.ProjectType, p.ProjectName, p.Developer, p.LandAreaRai, p.LandAreaNgan,
                   p.LandAreaSquareWa, p.ConstructionProgressPercent, p.UnitForSaleCount,
                   p.ProjectSaleLaunchDate, p.IsUnderConstruction, p.Latitude, p.Longitude,
                   p.Utilities, p.UtilitiesOther, p.Facilities, p.FacilitiesOther
            FROM appraisal.Projects p
            WHERE p.AppraisalId = @AppraisalId;

            -- RS03: project land
            SELECT pl.Id, pl.LandCheckMethodType, pl.LandCheckMethodTypeOther, pl.RoadPassInFrontOfLand,
                   pl.RoadSurfaceType, pl.RoadSurfaceTypeOther, pl.CarriagewayWidth, pl.RightOfWayWidth, pl.NumberOfLanes,
                   pl.PublicUtilityType, pl.PublicUtilityTypeOther,
                   pl.TransportationAccessType, pl.TransportationAccessTypeOther, pl.EnvironmentType, pl.EnvironmentTypeOther,
                   pl.LandZoneType, pl.LandZoneTypeOther, pl.UrbanPlanningType, pl.UrbanPlanningColor, pl.UrbanPlanningColorOther,
                   pl.PropertyAnticipationType, pl.PropertyAnticipationTypeOther,
                   pl.IsInExpropriationLine, pl.ExpropriationLineRemark, pl.EvictionType, pl.EvictionTypeOther, pl.EvictionRemark
            FROM appraisal.ProjectLands pl
            JOIN appraisal.Projects p ON p.Id = pl.ProjectId
            WHERE p.AppraisalId = @AppraisalId;

            -- RS04: title documents
            SELECT t.TitleType, t.TitleNumber, t.LandParcelNumber, t.SurveyNumber
            FROM appraisal.ProjectLandTitles t
            JOIN appraisal.ProjectLands pl ON pl.Id = t.ProjectLandId
            JOIN appraisal.Projects p ON p.Id = pl.ProjectId
            WHERE p.AppraisalId = @AppraisalId
            ORDER BY t.CreatedAt, t.Id;

            -- RS05: models (the value is the model's own pricing analysis; the coverage is the one
            -- the model's units carry)
            SELECT pm.Id, pm.ModelName, pm.ModelDescription, pm.BuildingType, pm.BuildingTypeOther,
                   pm.NumberOfFloors, pm.DecorationType, pm.DecorationTypeOther,
                   pm.RoomLayoutType, pm.RoomLayoutTypeOther, pm.StandardUsableArea, pm.StandardLandArea,
                   pm.StartingPriceMin, pm.BuildingAge,
                   pm.StructureType, pm.StructureTypeOther, pm.RoofFrameType, pm.RoofFrameTypeOther,
                   pm.RoofType, pm.RoofTypeOther, pm.CeilingType, pm.CeilingTypeOther,
                   pm.InteriorWallType, pm.InteriorWallTypeOther, pm.ExteriorWallType, pm.ExteriorWallTypeOther,
                   pm.GroundFloorMaterialType, pm.GroundFloorMaterialTypeOther,
                   pm.UpperFloorMaterialType, pm.UpperFloorMaterialTypeOther,
                   pm.BathroomFloorMaterialType, pm.BathroomFloorMaterialTypeOther,
                   (SELECT COUNT(*) FROM appraisal.ProjectUnits u WHERE u.ProjectModelId = pm.Id) AS UnitCount,
                   (SELECT TOP 1 pa.FinalAppraisedValue FROM appraisal.PricingAnalysis pa
                    WHERE pa.AnchorId = pm.Id AND pa.SubjectType = 1
                    ORDER BY pa.CreatedAt DESC, pa.Id) AS AppraisedValue,
                   (SELECT MAX(up.CoverageAmount) FROM appraisal.ProjectUnits u
                    JOIN appraisal.ProjectUnitPrices up ON up.ProjectUnitId = u.Id
                    WHERE u.ProjectModelId = pm.Id) AS CoverageAmount
            FROM appraisal.ProjectModels pm
            JOIN appraisal.Projects p ON p.Id = pm.ProjectId
            WHERE p.AppraisalId = @AppraisalId
            ORDER BY pm.ModelName, pm.Id;

            -- RS06: model cost rows
            SELECT d.ProjectModelId, d.AreaDescription, d.Area, d.PricePerSqMBeforeDepreciation,
                   d.PriceBeforeDepreciation, d.TotalDepreciationPct, d.PriceAfterDepreciation
            FROM appraisal.ProjectModelDepreciationDetails d
            JOIN appraisal.ProjectModels pm ON pm.Id = d.ProjectModelId
            JOIN appraisal.Projects p ON p.Id = pm.ProjectId
            WHERE p.AppraisalId = @AppraisalId
            ORDER BY d.ProjectModelId, d.[Year], d.CreatedAt, d.Id;

            -- RS07: unit rows (every unit, sold or not — they have to add up to the block totals)
            SELECT u.SequenceNumber, u.PlotNumber, u.HouseNumber, u.ModelType, u.LandArea, u.UsableArea,
                   u.SellingPrice, u.Floor, u.TowerName, u.CondoRegistrationNumber, u.RoomNumber,
                   up.TotalAppraisalValueRounded AS AppraisalValue, up.StandardPrice AS PricePerSqm,
                   up.ForceSellingPrice AS ForcedSaleValue, up.CoverageAmount
            FROM appraisal.ProjectUnits u
            JOIN appraisal.Projects p ON p.Id = u.ProjectId
            LEFT JOIN appraisal.ProjectUnitPrices up ON up.ProjectUnitId = u.Id
            WHERE p.AppraisalId = @AppraisalId
            ORDER BY u.SequenceNumber;

            -- RS08: pricing assumptions
            SELECT pa.LocationMethod, pa.CornerAdjustment, pa.EdgeAdjustment, pa.OtherAdjustment,
                   pa.PoolViewAdjustment, pa.SouthAdjustment, pa.FloorIncrementEveryXFloor,
                   pa.FloorIncrementAmount, pa.NearGardenAdjustment, pa.LandIncreaseDecreaseRate
            FROM appraisal.ProjectPricingAssumptions pa
            JOIN appraisal.Projects p ON p.Id = pa.ProjectId
            WHERE p.AppraisalId = @AppraisalId;

            -- RS09: model analysis remarks
            SELECT pm.ModelName, pa.Remark
            FROM appraisal.PricingAnalysis pa
            JOIN appraisal.ProjectModels pm ON pm.Id = pa.AnchorId
            JOIN appraisal.Projects p ON p.Id = pm.ProjectId
            WHERE p.AppraisalId = @AppraisalId AND pa.SubjectType = 1
              AND NULLIF(LTRIM(RTRIM(pa.Remark)), '') IS NOT NULL
            ORDER BY pm.ModelName, pa.Id;
            """;

        var args = new DynamicParameters();
        args.Add("AppraisalId", appraisalId);

        List<ParamRow> paramRows;
        ProjectRow? project;
        LandRow? land;
        List<TitleRow> titleRows;
        List<ModelRow> modelRows;
        List<CostDbRow> costRows;
        List<UnitRow> unitRows;
        AssumptionRow? assumption;
        List<RemarkRow> remarkRows;

        using (var multi = await connection.QueryMultipleAsync(new CommandDefinition(batchSql, args, cancellationToken: ct)))
        {
            paramRows = (await multi.ReadAsync<ParamRow>()).ToList();
            project = await multi.ReadFirstOrDefaultAsync<ProjectRow>();
            land = await multi.ReadFirstOrDefaultAsync<LandRow>();
            titleRows = (await multi.ReadAsync<TitleRow>()).ToList();
            modelRows = (await multi.ReadAsync<ModelRow>()).ToList();
            costRows = (await multi.ReadAsync<CostDbRow>()).ToList();
            unitRows = (await multi.ReadAsync<UnitRow>()).ToList();
            assumption = await multi.ReadFirstOrDefaultAsync<AssumptionRow>();
            remarkRows = (await multi.ReadAsync<RemarkRow>()).ToList();
        }

        if (project is null)
            return null;

        var maps = paramRows
            .Where(p => p.Group is not null && p.Code is not null)
            .GroupBy(p => p.Group!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<string, string?>)g
                    .GroupBy(p => p.Code!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(c => c.Key, c => c.First().Description, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        IReadOnlyDictionary<string, string?> Map(string group) =>
            maps.TryGetValue(group, out var m) ? m : new Dictionary<string, string?>();

        // Scalar code + its *Other free text (code 99 shows the free text instead), and the JSON-array form.
        string? One(string? code, string? other, string group)
        {
            var text = ParameterCodeFormatter.Translate(code, Map(group));
            if (string.IsNullOrWhiteSpace(other)) return text;
            if (code == ParameterCodeFormatter.OtherCode) return other.Trim();
            return text is null ? other.Trim() : $"{text} ({other.Trim()})";
        }
        string? Many(string? json, string? other, string group) =>
            ParameterCodeFormatter.DecodeJsonArray(json, other, Map(group));

        var isCondo = string.Equals(project.ProjectType?.Trim(), "U", StringComparison.OrdinalIgnoreCase);

        // ── Project land ──────────────────────────────────────────────────────────
        BlockLandDetail? landDetail = null;
        if (land is not null)
        {
            var roadParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(land.RoadPassInFrontOfLand)) roadParts.Add(land.RoadPassInFrontOfLand.Trim());
            var surface = One(land.RoadSurfaceType, land.RoadSurfaceTypeOther, "RoadSurface");
            if (surface is not null) roadParts.Add(surface);
            if (land.CarriagewayWidth is > 0) roadParts.Add($"ผิวจราจรกว้าง {land.CarriagewayWidth:#,##0.##} ม.");
            if (land.NumberOfLanes is > 0) roadParts.Add($"{land.NumberOfLanes} ช่องจราจร");

            landDetail = new BlockLandDetail
            {
                Titles = titleRows.Select(t => string.Join(" ", new[]
                {
                    ThaiAddressFormatter.Stated(t.TitleNumber) is { } number ? $"{TitleDeedLabel.NumberPrefix(t.TitleType)} {number}" : TitleDeedLabel.Noun(t.TitleType),
                    ThaiAddressFormatter.Stated(t.LandParcelNumber) is { } parcel ? $"เลขที่ดิน {parcel}" : null,
                    ThaiAddressFormatter.Stated(t.SurveyNumber) is { } survey ? $"หน้าสำรวจ {survey}" : null
                }.Where(s => !string.IsNullOrWhiteSpace(s)))).ToList(),
                // A multi-select, but rows saved before that hold a bare code, which DecodeJsonArray would not translate.
                CheckMethod = LandSectionLoader.JsonCodesToThai(land.LandCheckMethodType, Map("CheckBy"), land.LandCheckMethodTypeOther),
                RoadInFront = roadParts.Count > 0 ? string.Join(" ", roadParts) : null,
                RightOfWayWidth = land.RightOfWayWidth,
                PublicUtility = Many(land.PublicUtilityType, land.PublicUtilityTypeOther, "PublicUtility"),
                Transportation = Many(land.TransportationAccessType, land.TransportationAccessTypeOther, "Transportation"),
                LandZone = Many(land.LandZoneType, land.LandZoneTypeOther, "Location"),
                Environment = Many(land.EnvironmentType, land.EnvironmentTypeOther, "Environment"),
                UrbanPlanning = One(land.UrbanPlanningType, null, "TypeOfUrbanPlanning"),
                UrbanPlanningColor = One(land.UrbanPlanningColor, land.UrbanPlanningColorOther, "UrbanPlanningColor"),
                Anticipation = One(land.PropertyAnticipationType, land.PropertyAnticipationTypeOther, "AnticipationOfProsperity"),
                Expropriation = ExpropriationText(land.IsInExpropriationLine, land.ExpropriationLineRemark),
                Eviction = Many(land.EvictionType, land.EvictionTypeOther, "Eviction"),
                EvictionRemark = AppraisalSummaryCommonLoader.FirstNonBlank(land.EvictionRemark)
            };
        }

        // ── Models ────────────────────────────────────────────────────────────────
        var costsByModel = costRows.ToLookup(c => c.ProjectModelId);
        // Each model's own WQS / Sale Grid (PricingAnalysis SubjectType 1), printed under สรุปราคาประเมินรายแบบ.
        var analysesByModel = await PricingSubAnalysisLoader.LoadForModelsAsync(
            connection, modelRows.ToDictionary(m => m.Id, m => (m.ModelName, m.StandardUsableArea)), toApplicationTime, ct);
        var models = modelRows.Select((m, i) => new BlockModelDetail
        {
            Sequence = i + 1,
            ModelName = m.ModelName,
            ModelDescription = m.ModelDescription,
            BuildingType = One(m.BuildingType, m.BuildingTypeOther, "BuildingType"),
            NumberOfFloors = m.NumberOfFloors,
            Decoration = One(m.DecorationType, m.DecorationTypeOther, "Decoration"),
            RoomLayout = One(m.RoomLayoutType, m.RoomLayoutTypeOther, "RoomLayout"),
            StandardUsableArea = m.StandardUsableArea,
            StandardLandArea = m.StandardLandArea,
            StartingPriceMin = m.StartingPriceMin,
            UnitCount = m.UnitCount,
            IsUnderConstruction = project.IsUnderConstruction,
            BuildingAge = m.BuildingAge,
            Structure = Many(m.StructureType, m.StructureTypeOther, "GeneralStructure"),
            RoofFrame = Many(m.RoofFrameType, m.RoofFrameTypeOther, "RoofFrame"),
            Roof = Many(m.RoofType, m.RoofTypeOther, "Roof"),
            FloorMaterial = string.Join(" / ", new[]
                {
                    One(m.GroundFloorMaterialType, m.GroundFloorMaterialTypeOther, "GroundFlooringMaterials"),
                    One(m.UpperFloorMaterialType, m.UpperFloorMaterialTypeOther, "UpperFlooringMaterials")
                }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct()) is { Length: > 0 } floor ? floor : null,
            BathroomFloorMaterial = One(m.BathroomFloorMaterialType, m.BathroomFloorMaterialTypeOther, "BathroomFlooringMaterials"),
            InteriorWall = Many(m.InteriorWallType, m.InteriorWallTypeOther, "Interior"),
            ExteriorWall = Many(m.ExteriorWallType, m.ExteriorWallTypeOther, "Exterior"),
            Ceiling = Many(m.CeilingType, m.CeilingTypeOther, "Ceiling"),
            AppraisedValue = m.AppraisedValue,
            CoverageAmount = m.CoverageAmount,
            Analyses = analysesByModel.GetValueOrDefault(m.Id) ?? [],
            Costs = costsByModel[m.Id].Select(c => new BlockCostRow
            {
                Description = c.AreaDescription,
                Area = c.Area,
                PricePerSqm = c.PricePerSqMBeforeDepreciation,
                PriceBeforeDepreciation = c.PriceBeforeDepreciation,
                TotalDepreciationPct = c.TotalDepreciationPct,
                PriceAfterDepreciation = c.PriceAfterDepreciation
            }).ToList()
        }).ToList();

        // ── Project detail ────────────────────────────────────────────────────────
        var hasArea = (project.LandAreaRai ?? 0) + (project.LandAreaNgan ?? 0) + (project.LandAreaSquareWa ?? 0) > 0;
        var projectDetail = new BlockProjectDetail
        {
            ProjectName = ThaiAddressFormatter.Stated(project.ProjectName),
            Developer = ThaiAddressFormatter.Stated(project.Developer),
            LandAreaText = hasArea
                ? ThaiLandAreaFormatter.FormatTotal(project.LandAreaRai ?? 0, project.LandAreaNgan ?? 0, project.LandAreaSquareWa ?? 0)
                : null,
            LandAreaSquareWa = hasArea ? (project.LandAreaRai ?? 0) * 400m + (project.LandAreaNgan ?? 0) * 100m + (project.LandAreaSquareWa ?? 0) : null,
            ConstructionProgressPercent = project.ConstructionProgressPercent,
            UnitForSaleCount = project.UnitForSaleCount,
            SaleLaunchDate = FormatThaiPartialDate(project.ProjectSaleLaunchDate),
            Latitude = project.Latitude,
            Longitude = project.Longitude,
            // สาธารณูปโภค / สิ่งอำนวยความสะดวก of the PROJECT (Projects.Utilities / Facilities), not the land's.
            Utilities = Many(project.Utilities, project.UtilitiesOther, "PublicUtility"),
            Facilities = Many(project.Facilities, project.FacilitiesOther, "Facilities"),
            AppraisedUnitCount = unitRows.Count,
            AppraisedUnitSharePct = project.UnitForSaleCount is > 0
                ? Math.Round(unitRows.Count * 100m / project.UnitForSaleCount.Value, 2)
                : null
        };

        // ── Analysis ──────────────────────────────────────────────────────────────
        var remark = remarkRows.Count switch
        {
            0 => null,
            1 => remarkRows[0].Remark!.Trim(),
            _ => string.Join("\n", remarkRows.Select(r => $"{r.ModelName}: {r.Remark!.Trim()}"))
        };

        var lines = new List<BlockAssumptionLine>();
        if (assumption is not null)
        {
            // The adjustments are baht per sq.m., a percentage or a flat baht amount, by LocationMethod.
            var (suffix, methodText) = assumption.LocationMethod switch
            {
                "AdjustPriceSqm" => (" บาท/ตร.ม.", "ปรับราคาเป็นบาทต่อตารางเมตร"),
                "AdjustPricePercentage" => ("%", "ปรับราคาเป็นร้อยละ"),
                "Lumpsum" => (" บาท", "ปรับราคาเป็นจำนวนเงินเหมา"),
                _ => ("", null as string)
            };
            void Add(string label, decimal? value, string unit)
            {
                if (value.HasValue) lines.Add(new BlockAssumptionLine { Label = label, Value = $"{value:#,##0.##}{unit}" });
            }
            if (methodText is not null) lines.Add(new BlockAssumptionLine { Label = "วิธีปรับราคาตามทำเล", Value = methodText });
            Add("แปลงมุม", assumption.CornerAdjustment, suffix);
            Add("แปลงริม", assumption.EdgeAdjustment, suffix);
            Add("ใกล้สวน", assumption.NearGardenAdjustment, suffix);
            Add("วิวสระว่ายน้ำ", assumption.PoolViewAdjustment, suffix);
            Add("ทิศใต้", assumption.SouthAdjustment, suffix);
            Add("อื่นๆ", assumption.OtherAdjustment, suffix);
            Add("ที่ดินเพิ่ม-ลด ตารางวาละ", assumption.LandIncreaseDecreaseRate, " บาท");
            if (assumption.FloorIncrementEveryXFloor is > 0 && assumption.FloorIncrementAmount.HasValue)
                lines.Add(new BlockAssumptionLine
                {
                    Label = "ราคาเพิ่มตามชั้น",
                    Value = $"ทุก {assumption.FloorIncrementEveryXFloor} ชั้น ชั้นละ {assumption.FloorIncrementAmount:#,##0.##} บาท"
                });
        }

        return new BlockBookSection
        {
            IsCondo = isCondo,
            Land = landDetail,
            Project = projectDetail,
            Models = models,
            AnalysisRemark = remark,
            Assumptions = lines,
            BuildingUnits = isCondo ? [] : unitRows.Select(u => new BlockBuildingUnitRow
            {
                Sequence = u.SequenceNumber,
                PlotNumber = u.PlotNumber,
                HouseNumber = u.HouseNumber,
                ModelType = u.ModelType,
                LandArea = u.LandArea,
                UsableArea = u.UsableArea,
                SellingPrice = u.SellingPrice,
                AppraisalValue = u.AppraisalValue,
                ForcedSaleValue = u.ForcedSaleValue,
                CoverageAmount = u.CoverageAmount
            }).ToList(),
            CondoUnits = !isCondo ? [] : unitRows.Select(u => new BlockCondoUnitRow
            {
                Sequence = u.SequenceNumber,
                Floor = u.Floor,
                TowerName = u.TowerName,
                CondoRegistrationNumber = u.CondoRegistrationNumber,
                RoomNumber = u.RoomNumber,
                ModelType = u.ModelType,
                UsableArea = u.UsableArea,
                SellingPrice = u.SellingPrice,
                AppraisalValue = u.AppraisalValue,
                PricePerSqm = u.PricePerSqm,
                ForcedSaleValue = u.ForcedSaleValue,
                CoverageAmount = u.CoverageAmount
            }).ToList()
        };
    }

    /// <summary>แนวเวนคืน only (the legacy IsExpropriated "ระวางเวนคืน" is no longer read): the answer, then the remark in brackets when there is one.</summary>
    internal static string? ExpropriationText(bool? inLine, string? remark) => inLine switch
    {
        true => string.IsNullOrWhiteSpace(remark) ? "อยู่ในแนวเวนคืน" : $"อยู่ในแนวเวนคืน ({remark.Trim()})",
        false => "ไม่อยู่ในแนวเวนคืน",
        _ => null
    };

    /// <summary>"YYYY" / "YYYY-MM" / "YYYY-MM-DD" (Gregorian) → Buddhist-era Thai text.</summary>
    private static string? FormatThaiPartialDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var parts = raw.Trim().Split('-');
        if (!int.TryParse(parts[0], out var year)) return null;
        var beYear = year + 543;

        if (parts.Length >= 3
            && int.TryParse(parts[1], out var m) && m is >= 1 and <= 12
            && int.TryParse(parts[2], out var d) && d >= 1)
            return $"{d} {ThaiDateFormatter.MonthName(m)} {beYear}";

        if (parts.Length >= 2 && int.TryParse(parts[1], out var m2) && m2 is >= 1 and <= 12)
            return $"{ThaiDateFormatter.MonthName(m2)} {beYear}";

        return beYear.ToString();
    }

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    private sealed class ParamRow
    {
        public string? Group { get; init; }
        public string? Code { get; init; }
        public string? Description { get; init; }
    }

    private sealed class ProjectRow
    {
        public Guid Id { get; init; }
        public string? ProjectType { get; init; }
        public string? ProjectName { get; init; }
        public string? Developer { get; init; }
        public decimal? LandAreaRai { get; init; }
        public decimal? LandAreaNgan { get; init; }
        public decimal? LandAreaSquareWa { get; init; }
        public decimal? ConstructionProgressPercent { get; init; }
        public int? UnitForSaleCount { get; init; }
        public string? ProjectSaleLaunchDate { get; init; }
        public bool? IsUnderConstruction { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
        public string? Utilities { get; init; }
        public string? UtilitiesOther { get; init; }
        public string? Facilities { get; init; }
        public string? FacilitiesOther { get; init; }
    }

    private sealed class LandRow
    {
        public string? LandCheckMethodType { get; init; }
        public string? LandCheckMethodTypeOther { get; init; }
        public string? RoadPassInFrontOfLand { get; init; }
        public string? RoadSurfaceType { get; init; }
        public string? RoadSurfaceTypeOther { get; init; }
        public decimal? CarriagewayWidth { get; init; }
        public decimal? RightOfWayWidth { get; init; }
        public short? NumberOfLanes { get; init; }
        public string? PublicUtilityType { get; init; }
        public string? PublicUtilityTypeOther { get; init; }
        public string? TransportationAccessType { get; init; }
        public string? TransportationAccessTypeOther { get; init; }
        public string? EnvironmentType { get; init; }
        public string? EnvironmentTypeOther { get; init; }
        public string? LandZoneType { get; init; }
        public string? LandZoneTypeOther { get; init; }
        public string? UrbanPlanningType { get; init; }
        public string? UrbanPlanningColor { get; init; }
        public string? UrbanPlanningColorOther { get; init; }
        public string? PropertyAnticipationType { get; init; }
        public string? PropertyAnticipationTypeOther { get; init; }
        public bool? IsInExpropriationLine { get; init; }
        public string? ExpropriationLineRemark { get; init; }
        public string? EvictionType { get; init; }
        public string? EvictionTypeOther { get; init; }
        public string? EvictionRemark { get; init; }
    }

    private sealed class TitleRow
    {
        public string? TitleType { get; init; }
        public string? TitleNumber { get; init; }
        public string? LandParcelNumber { get; init; }
        public string? SurveyNumber { get; init; }
    }

    private sealed class ModelRow
    {
        public Guid Id { get; init; }
        public string? ModelName { get; init; }
        public string? ModelDescription { get; init; }
        public string? BuildingType { get; init; }
        public string? BuildingTypeOther { get; init; }
        public decimal? NumberOfFloors { get; init; }
        public string? DecorationType { get; init; }
        public string? DecorationTypeOther { get; init; }
        public string? RoomLayoutType { get; init; }
        public string? RoomLayoutTypeOther { get; init; }
        public decimal? StandardUsableArea { get; init; }
        public decimal? StandardLandArea { get; init; }
        public decimal? StartingPriceMin { get; init; }
        public int? BuildingAge { get; init; }
        public string? StructureType { get; init; }
        public string? StructureTypeOther { get; init; }
        public string? RoofFrameType { get; init; }
        public string? RoofFrameTypeOther { get; init; }
        public string? RoofType { get; init; }
        public string? RoofTypeOther { get; init; }
        public string? CeilingType { get; init; }
        public string? CeilingTypeOther { get; init; }
        public string? InteriorWallType { get; init; }
        public string? InteriorWallTypeOther { get; init; }
        public string? ExteriorWallType { get; init; }
        public string? ExteriorWallTypeOther { get; init; }
        public string? GroundFloorMaterialType { get; init; }
        public string? GroundFloorMaterialTypeOther { get; init; }
        public string? UpperFloorMaterialType { get; init; }
        public string? UpperFloorMaterialTypeOther { get; init; }
        public string? BathroomFloorMaterialType { get; init; }
        public string? BathroomFloorMaterialTypeOther { get; init; }
        public int UnitCount { get; init; }
        public decimal? AppraisedValue { get; init; }
        public decimal? CoverageAmount { get; init; }
    }

    private sealed class CostDbRow
    {
        public Guid ProjectModelId { get; init; }
        public string? AreaDescription { get; init; }
        public decimal Area { get; init; }
        public decimal PricePerSqMBeforeDepreciation { get; init; }
        public decimal PriceBeforeDepreciation { get; init; }
        public decimal TotalDepreciationPct { get; init; }
        public decimal PriceAfterDepreciation { get; init; }
    }

    private sealed class UnitRow
    {
        public int SequenceNumber { get; init; }
        public string? PlotNumber { get; init; }
        public string? HouseNumber { get; init; }
        public string? ModelType { get; init; }
        public decimal? LandArea { get; init; }
        public decimal? UsableArea { get; init; }
        public decimal? SellingPrice { get; init; }
        public int? Floor { get; init; }
        public string? TowerName { get; init; }
        public string? CondoRegistrationNumber { get; init; }
        public string? RoomNumber { get; init; }
        public decimal? AppraisalValue { get; init; }
        public decimal? PricePerSqm { get; init; }
        public decimal? ForcedSaleValue { get; init; }
        public decimal? CoverageAmount { get; init; }
    }

    private sealed class AssumptionRow
    {
        public string? LocationMethod { get; init; }
        public decimal? CornerAdjustment { get; init; }
        public decimal? EdgeAdjustment { get; init; }
        public decimal? OtherAdjustment { get; init; }
        public decimal? PoolViewAdjustment { get; init; }
        public decimal? SouthAdjustment { get; init; }
        public int? FloorIncrementEveryXFloor { get; init; }
        public decimal? FloorIncrementAmount { get; init; }
        public decimal? NearGardenAdjustment { get; init; }
        public decimal? LandIncreaseDecreaseRate { get; init; }
    }

    private sealed class RemarkRow
    {
        public string? ModelName { get; init; }
        public string? Remark { get; init; }
    }
}
