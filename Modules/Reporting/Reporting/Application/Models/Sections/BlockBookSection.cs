namespace Reporting.Application.Models.Sections;

/// <summary>
/// Block (project) pages of the appraisal book — FSD §2.1.7 / §2.1.2 for a project appraisal
/// (<c>appraisal.Projects</c> row): project land, project detail, one building+cost page per house
/// model, the per-model analysis page and the unit price tables. Everything is read from the
/// Project aggregate tables; a field the system does not store is printed by the template as a
/// red "-", not modelled here. Null when the appraisal has no project.
/// </summary>
public sealed class BlockBookSection
{
    /// <summary>ProjectType "U" (condominium units) — condo models get no building page.</summary>
    public bool IsCondo { get; init; }

    public BlockLandDetail? Land { get; init; }
    public BlockProjectDetail Project { get; init; } = new();
    public IReadOnlyList<BlockModelDetail> Models { get; init; } = [];

    /// <summary>PricingAnalysis.Remark of the models (SubjectType = 1), "model: text" when more than one.</summary>
    public string? AnalysisRemark { get; init; }

    /// <summary>Pricing-assumption lines (label, value), e.g. corner / edge adjustment.</summary>
    public IReadOnlyList<BlockAssumptionLine> Assumptions { get; init; } = [];

    /// <summary>Unit price rows (house projects). The book prints them for external books only — the internal summary body already has them.</summary>
    public IReadOnlyList<BlockBuildingUnitRow> BuildingUnits { get; init; } = [];

    /// <summary>Unit price rows (condo projects). Same rule as <see cref="BuildingUnits"/>.</summary>
    public IReadOnlyList<BlockCondoUnitRow> CondoUnits { get; init; } = [];
}

/// <summary>รายละเอียดที่ดินโครงการ — appraisal.ProjectLands + ProjectLandTitles.</summary>
public sealed class BlockLandDetail
{
    /// <summary>เอกสารสิทธิ — ProjectLandTitles (type + number + parcel + survey), one per title.</summary>
    public IReadOnlyList<string> Titles { get; init; } = [];
    public string? CheckMethod { get; init; }
    public string? RoadInFront { get; init; }
    /// <summary>เขตทางกว้าง (metres) — ProjectLands.RightOfWayWidth.</summary>
    public decimal? RightOfWayWidth { get; init; }
    public string? PublicUtility { get; init; }
    public string? Transportation { get; init; }
    public string? LandZone { get; init; }
    /// <summary>สภาพแวดล้อม — ProjectLands.EnvironmentType (group 'Environment'; 99 prints the Other text), joined ", "; null on records saved before the field existed.</summary>
    public string? Environment { get; init; }
    public string? UrbanPlanning { get; init; }
    /// <summary>สีผังเมือง — ProjectLands.UrbanPlanningColor (99 prints the Other text).</summary>
    public string? UrbanPlanningColor { get; init; }
    public string? Anticipation { get; init; }
    /// <summary>แนวเวนคืน — ProjectLands.IsInExpropriationLine (+ ExpropriationLineRemark); the legacy IsExpropriated is not read.</summary>
    public string? Expropriation { get; init; }
    public string? Eviction { get; init; }
    /// <summary>รายละเอียดการรอนสิทธิ์ — ProjectLands.EvictionRemark.</summary>
    public string? EvictionRemark { get; init; }
}

/// <summary>รายละเอียดโครงการ — appraisal.Projects.</summary>
public sealed class BlockProjectDetail
{
    public string? ProjectName { get; init; }
    public string? Developer { get; init; }
    public string? LandAreaText { get; init; }
    /// <summary>The project land in square wa (ไร่ × 400 + งาน × 100 + ตร.ว.); null when no area is keyed. The cover prints it through <c>ThaiLandAreaFormatter.Describe</c>.</summary>
    public decimal? LandAreaSquareWa { get; init; }
    public decimal? ConstructionProgressPercent { get; init; }
    public int? UnitForSaleCount { get; init; }
    public string? SaleLaunchDate { get; init; }

    /// <summary>พิกัด GPS — Projects.Latitude / Longitude.</summary>
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }

    /// <summary>สาธารณูปโภคในโครงการ — Projects.Utilities (+ Other), comma-joined Thai labels.</summary>
    public string? Utilities { get; init; }

    /// <summary>สิ่งอำนวยความสะดวก — Projects.Facilities (+ Other), comma-joined Thai labels.</summary>
    public string? Facilities { get; init; }

    public int AppraisedUnitCount { get; init; }

    /// <summary>AppraisedUnitCount / UnitForSaleCount as a percentage (computed; null without a for-sale count).</summary>
    public decimal? AppraisedUnitSharePct { get; init; }
}

/// <summary>One house / unit model — appraisal.ProjectModels with its cost and price figures.</summary>
public sealed class BlockModelDetail
{
    public int Sequence { get; init; }
    public string? ModelName { get; init; }
    public string? ModelDescription { get; init; }
    public string? BuildingType { get; init; }
    public decimal? NumberOfFloors { get; init; }
    public string? Decoration { get; init; }
    public string? RoomLayout { get; init; }
    public decimal? StandardUsableArea { get; init; }
    public decimal? StandardLandArea { get; init; }
    public decimal? StartingPriceMin { get; init; }
    public int UnitCount { get; init; }

    public bool? IsUnderConstruction { get; init; }
    public int? BuildingAge { get; init; }
    public string? Structure { get; init; }
    public string? RoofFrame { get; init; }
    public string? Roof { get; init; }
    public string? FloorMaterial { get; init; }
    public string? BathroomFloorMaterial { get; init; }
    public string? InteriorWall { get; init; }
    public string? ExteriorWall { get; init; }
    public string? Ceiling { get; init; }

    /// <summary>ราคาประเมิน — PricingAnalysis.FinalAppraisedValue of the model.</summary>
    public decimal? AppraisedValue { get; init; }

    /// <summary>มูลค่าประกันภัย — ProjectUnitPrices.CoverageAmount (one value per model).</summary>
    public decimal? CoverageAmount { get; init; }

    public IReadOnlyList<BlockCostRow> Costs { get; init; } = [];

    /// <summary>The model's own WQS / Sale Grid (PricingAnalysis SubjectType 1, AnchorId = the model).</summary>
    public IReadOnlyList<PricingSubAnalysis> Analyses { get; init; } = [];
}

/// <summary>One row of the model cost table — appraisal.ProjectModelDepreciationDetails.</summary>
public sealed class BlockCostRow
{
    public string? Description { get; init; }
    public decimal Area { get; init; }
    public decimal PricePerSqm { get; init; }
    public decimal PriceBeforeDepreciation { get; init; }
    public decimal TotalDepreciationPct { get; init; }
    public decimal PriceAfterDepreciation { get; init; }
}

public sealed class BlockAssumptionLine
{
    public string Label { get; init; } = "";
    public string Value { get; init; } = "";
}
