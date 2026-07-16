using System.Globalization;
using Collateral.Contracts;
using Collateral.Contracts.FileInterface;
using Integration.Contracts.FixedWidth;

namespace Integration.FileInterface.Format.RegulatoryExport;

/// <summary>
/// Writes the outbound CAS-AS400-Regulatory interface — a fixed-width 300-char UTF-8 H/D/T file
/// sent monthly as a full Basel/RDT regulatory snapshot (one record per active IsMaster master).
/// </summary>
public sealed class RegulatoryFileWriter
{
    public const int RecordLength = 300;

    // Engagement AppraisalType value for a construction (progressive) inspection.
    private const string ProgressiveAppraisalType = "Progressive";

    private static readonly FixedWidthField[] DetailFields =
    [
        new("RecordType",                   1,   FixedWidthAlign.Left),
        new("ApplicationId",               10,   FixedWidthAlign.Left),
        new("NewestApplicationId",         10,   FixedWidthAlign.Left),
        new("CollateralIdHost",            19,   FixedWidthAlign.RightZeroFill),
        new("UnderConstruction",            1,   FixedWidthAlign.Left),
        new("ConstructionProgress",         5,   FixedWidthAlign.RightZeroFill),
        new("AppraisalValueCompleted",     15,   FixedWidthAlign.RightZeroFill),
        new("AppraisalValueOrigination",   15,   FixedWidthAlign.RightZeroFill),
        new("NumberOfFloors",               3,   FixedWidthAlign.RightZeroFill),
        new("BuildingAge",                  3,   FixedWidthAlign.RightZeroFill),
        new("MarketSellingPrice",          15,   FixedWidthAlign.RightZeroFill),
        new("ValuationDate",                8,   FixedWidthAlign.Left),
        new("ValuationPrice",              15,   FixedWidthAlign.RightZeroFill),
        new("MortgageValue",               15,   FixedWidthAlign.RightZeroFill),
        new("AppraiserType",                1,   FixedWidthAlign.Left),
        new("CollateralRegistrationFlag",   1,   FixedWidthAlign.Left),
        new("LandOwnershipFlag",            1,   FixedWidthAlign.Left),
        new("DopaLocation",                 6,   FixedWidthAlign.Left),
        new("LandAreaSqWa",                 7,   FixedWidthAlign.RightZeroFill),
        new("AreaUtilization",              7,   FixedWidthAlign.RightZeroFill),
        new("BuildingTypeId",              10,   FixedWidthAlign.Left),
        new("BuildingName",               100,   FixedWidthAlign.Left),
        new("ExpectedCompletionDate",       8,   FixedWidthAlign.Left),
        new("ConstructionReviewDate",       8,   FixedWidthAlign.Left),
        new("FirstValuationDate",           8,   FixedWidthAlign.Left),
        new("LatestValuationDate",          8,   FixedWidthAlign.Left),
    ];

    private static readonly FixedWidthRecordBuilder DetailBuilder =
        new(DetailFields, RecordLength, FixedWidthOverflow.ThrowOnNumeric);

    public string BuildHeader(DateOnly effectiveDate) =>
        ("H" + effectiveDate.ToString("ddMMyyyy", CultureInfo.InvariantCulture)).PadRight(RecordLength);

    public string BuildTrailer(int detailCount) =>
        ("T" + detailCount.ToString(CultureInfo.InvariantCulture).PadLeft(9, '0')).PadRight(RecordLength);

    public string BuildDetail(RegulatoryExportRow row)
    {
        bool isLandType = row.CollateralType is CollateralTypes.Land
                                              or CollateralTypes.LandWithBuilding
                                              or CollateralTypes.Leasehold
                                              or CollateralTypes.LeaseholdBuilding
                                              or CollateralTypes.LeaseholdWithBuilding;

        bool isBuildingType = row.CollateralType is CollateralTypes.LandWithBuilding
                                                  or CollateralTypes.LeaseholdBuilding
                                                  or CollateralTypes.LeaseholdWithBuilding;

        bool isCondoType = row.CollateralType == CollateralTypes.Condo;

        // Under-construction and construction-progress apply only to land / building / land&building
        // types. Condo (and everything else) → blank / 0.00.
        bool isLandOrBuilding = isLandType || isBuildingType;

        string underConstruction;
        if (!isLandOrBuilding)
        {
            underConstruction = string.Empty;
        }
        else if (row.CollateralType is CollateralTypes.Land or CollateralTypes.Leasehold)
        {
            underConstruction = "L";
        }
        else
        {
            // Remaining in-group types are building types (LB / LSB / LS).
            underConstruction = row.IsUnderConstruction ? "Y" : "N";
        }

        string constructionProgress;
        if (!isLandOrBuilding)
        {
            constructionProgress = Money(0m)!;
        }
        else if (row.CollateralType is CollateralTypes.Land or CollateralTypes.Leasehold)
        {
            constructionProgress = Money(100m)!;
        }
        else
        {
            constructionProgress = Money(row.ConstructionProgressPercent ?? 0m)!;
        }

        var appraiserType = row.LatestAppraisalCompanyId.HasValue ? "1" : "2";

        string? landAreaSqWa = isLandType
            ? SmallDecimal(row.LandAreaSqWa)
            : null;

        string? areaUtilization = (isBuildingType || isCondoType)
            ? SmallDecimal(row.BuildingArea)
            : null;

        string? buildingTypeId = isBuildingType ? row.BuildingTypeCode : null;
        string? buildingName   = isBuildingType ? row.BuildingTypeDescription : null;

        // Origination value: for a construction (Progressive) inspection the first appraisal already
        // estimated the as-completed (100%) value, so use the earliest value; otherwise the latest.
        var isProgressive = string.Equals(row.LatestAppraisalType, ProgressiveAppraisalType, StringComparison.Ordinal);
        var originationValue = isProgressive ? row.EarliestAppraisalValue : row.LatestAppraisalValue;

        var values = new Dictionary<string, string?>
        {
            ["RecordType"]                 = "D",
            // Both Application Id and Newest Application Id carry the latest appraisal number — the
            // bank always sends the latest report number in both fields.
            ["ApplicationId"]              = row.LatestAppraisalNumber,
            ["NewestApplicationId"]        = row.LatestAppraisalNumber,
            ["CollateralIdHost"]           = row.HostCollateralId,
            ["UnderConstruction"]          = underConstruction,
            ["ConstructionProgress"]       = constructionProgress,
            ["AppraisalValueCompleted"]    = Money(row.LatestAppraisalValue),
            ["AppraisalValueOrigination"]  = Money(originationValue),
            ["NumberOfFloors"]             = SmallInt(row.NumberOfFloors, 999),
            ["BuildingAge"]                = SmallInt(row.BuildingAge, 999),
            ["MarketSellingPrice"]         = Money(row.SellingPrice),
            ["ValuationDate"]              = Date(row.LatestAppraisalDate),
            ["ValuationPrice"]             = Money(row.LatestAppraisalValue),
            ["MortgageValue"]              = null,
            ["AppraiserType"]              = appraiserType,
            ["CollateralRegistrationFlag"] = null,
            ["LandOwnershipFlag"]          = null,
            ["DopaLocation"]               = row.DopaCode,
            ["LandAreaSqWa"]               = landAreaSqWa,
            ["AreaUtilization"]            = areaUtilization,
            ["BuildingTypeId"]             = buildingTypeId,
            ["BuildingName"]               = buildingName,
            ["ExpectedCompletionDate"]     = null,
            ["ConstructionReviewDate"]     = Date(row.LatestProgressiveAppraisalDate),
            ["FirstValuationDate"]         = Date(row.EarliestAppraisalDate),
            ["LatestValuationDate"]        = Date(row.LatestAppraisalDate),
        };

        return DetailBuilder.Build(values);
    }

    public string BuildContent(DateOnly effectiveDate, IReadOnlyList<RegulatoryExportRow> rows)
    {
        var lines = new List<string>(rows.Count + 2) { BuildHeader(effectiveDate) };
        lines.AddRange(rows.Select(BuildDetail));
        lines.Add(BuildTrailer(rows.Count));
        return string.Join("\r\n", lines) + "\r\n";
    }

    private static string? Money(decimal? value) =>
        value is null
            ? null
            : ((long)Math.Round(value.Value * 100m, MidpointRounding.AwayFromZero))
                .ToString(CultureInfo.InvariantCulture);

    private static string? SmallDecimal(decimal? value) => Money(value);

    private static string? SmallInt(int? value, int maxValue) =>
        (value is not null && value >= 0 && value <= maxValue)
            ? value.Value.ToString(CultureInfo.InvariantCulture)
            : null;

    private static string? Date(DateTime? value) =>
        value.HasValue ? DateOnly.FromDateTime(value.Value).ToString("yyyyMMdd", CultureInfo.InvariantCulture) : null;
}
