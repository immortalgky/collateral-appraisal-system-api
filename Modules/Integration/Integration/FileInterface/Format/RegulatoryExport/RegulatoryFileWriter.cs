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

        // LSU is a leasehold OVER a condo unit: its area and age live on CondoDetails exactly like a
        // freehold condo's, so it gates with U and stays out of isLandType / isBuildingType.
        bool isCondoType = row.CollateralType is CollateralTypes.Condo or CollateralTypes.LeaseholdCondo;

        // Field #5 applies to every REAL-ESTATE collateral, condo and legacy (UNK) included — the
        // business rule is "all real estate", not just the land/building types. Condo used to fall out
        // of this gate and report blank, which the regulator reads as "not applicable" for a unit that
        // is very much a structure. The bank's own 2026-08-02 file sends N for all 7,716 condo and all
        // 1,209 legacy rows, so this matches the file we are replacing.
        // Machinery / PRJ stay out → blank, same as before.
        bool isRealEstate = isLandType || isBuildingType || isCondoType
                            || row.CollateralType is CollateralTypes.Unidentified;

        string underConstruction;
        if (!isRealEstate)
        {
            underConstruction = string.Empty;
        }
        else if (row.CollateralType is CollateralTypes.Land or CollateralTypes.Leasehold)
        {
            underConstruction = "L";
        }
        else
        {
            // Everything else in-group carries a structure: LB / LSB / LS, condo U / LSU, legacy UNK.
            underConstruction = row.IsUnderConstruction ? "Y" : "N";
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

        // Field #7 — value as it stands today. When buildings are part-built it is the progress-adjusted
        // figure (land + finished buildings + inspected buildings at their progress), computed upstream
        // by IConstructionCurrentValueService and frozen on the engagement. NULL means nothing was under
        // construction, so the as-completed appraised value already IS the current value.
        var currentValue = row.CurrentValue ?? row.LatestAppraisalValue;

        var values = new Dictionary<string, string?>
        {
            ["RecordType"]                 = "D",
            // Both Application Id and Newest Application Id carry the latest appraisal number — the
            // bank always sends the latest report number in both fields.
            ["ApplicationId"]              = row.LatestAppraisalNumber,
            ["NewestApplicationId"]        = row.LatestAppraisalNumber,
            ["CollateralIdHost"]           = row.HostCollateralId,
            ["UnderConstruction"]          = underConstruction,
            // Field #6 is computed in vw_RegulatoryExport (0 / 100 / progress%); here we only format it.
            ["ConstructionProgress"]       = Money(row.ConstructionProgressPercent ?? 0m),
            ["AppraisalValueCompleted"]    = Money(currentValue),
            // Field #8 — the full appraised value, unconditionally. The bank dropped the previous
            // "Progressive → use the earliest value" rule: this field is always the latest appraisal's
            // value, so it now carries the same figure as ValuationPrice (field #13).
            ["AppraisalValueOrigination"]  = Money(row.LatestAppraisalValue),
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
            // Field #24 — the date construction was last reviewed. Any appraisal that inspected the
            // construction counts, not only a Progressive-type one, so this is the latest appraisal's
            // date whenever the collateral is under construction. Blank when it is not: there is no
            // construction left to review.
            ["ConstructionReviewDate"]     = row.IsUnderConstruction ? Date(row.LatestAppraisalDate) : null,
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

    // Area fields are dec(7,2) in 8 characters, so ×100 must still fit 7 digits with no sign. The view
    // already guards both ends, but this is the line where an out-of-range value throws and takes the
    // WHOLE monthly file down with it — U3 had one appraisal with LandArea = -10258.60 and the export
    // produced no file at all. Belt and braces: out of range → blank, same as the view's NULL.
    private static string? SmallDecimal(decimal? value) =>
        value is null or < 0m or > 99_999.99m ? null : Money(value);

    private static string? SmallInt(int? value, int maxValue) =>
        (value is not null && value >= 0 && value <= maxValue)
            ? value.Value.ToString(CultureInfo.InvariantCulture)
            : null;

    private static string? Date(DateTime? value) =>
        value.HasValue ? DateOnly.FromDateTime(value.Value).ToString("yyyyMMdd", CultureInfo.InvariantCulture) : null;
}
