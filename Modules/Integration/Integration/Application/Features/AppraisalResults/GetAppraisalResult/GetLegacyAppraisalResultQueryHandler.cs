using System.Data;
using System.Globalization;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Projects;
using Dapper;
using Shared.CQRS;
using Shared.Data;

namespace Integration.Application.Features.AppraisalResults.GetAppraisalResult;

// Serves the legacy (AS400) flat { ResultCode, ResultValue } contract for ONE collateral, selected by
// ApplicationNo (= AppraisalNumber) + Filter1/Filter2. Any miss (not found / no matching collateral /
// error) returns ResultCode = 0 with an empty ResultValue rather than throwing.
public class GetLegacyAppraisalResultQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetLegacyAppraisalResultQuery, LegacyAppraisalResultEnvelope>
{
    private const int Success = 1;
    private const int NotFoundOrError = 0;

    public async Task<LegacyAppraisalResultEnvelope> Handle(
        GetLegacyAppraisalResultQuery query,
        CancellationToken cancellationToken)
    {
        try
        {
            var conn = connectionFactory.GetOpenConnection();

            var headerParams = new DynamicParameters();
            headerParams.Add("AppraisalNumber", query.ApplicationNo);
            var header = await conn.QuerySingleOrDefaultAsync<LegacyAppraisalRow>(
                new CommandDefinition(GetAppraisalResultSql.LegacyByAppraisalNumber, headerParams,
                    cancellationToken: cancellationToken));

            if (header is null) return Empty();

            var assignmentParams = new DynamicParameters();
            assignmentParams.Add("AppraisalId", header.Id);
            var assignment = await conn.QueryFirstOrDefaultAsync<AssignmentRow>(
                new CommandDefinition(GetAppraisalResultSql.ActiveAssignment, assignmentParams,
                    cancellationToken: cancellationToken));

            decimal? fee = null;
            if (assignment is not null)
            {
                var feeParams = new DynamicParameters();
                feeParams.Add("AssignmentId", assignment.AssignmentId);
                fee = await conn.QueryFirstOrDefaultAsync<decimal?>(
                    new CommandDefinition(GetAppraisalResultSql.Fee, feeParams, cancellationToken: cancellationToken));
            }

            var valParams = new DynamicParameters();
            valParams.Add("AppraisalId", header.Id);
            var valuation = await conn.QueryFirstOrDefaultAsync<ValuationRow>(
                new CommandDefinition(GetAppraisalResultSql.ValuationTotals, valParams,
                    cancellationToken: cancellationToken));

            // Block/project appraisal? (1:1 row in appraisal.Projects — has no AppraisalProperty rows.)
            var projParams = new DynamicParameters();
            projParams.Add("AppraisalId", header.Id);
            var project = await conn.QueryFirstOrDefaultAsync<ProjectRow>(
                new CommandDefinition(GetAppraisalResultSql.ProjectByAppraisalId, projParams,
                    cancellationToken: cancellationToken));

            LegacyAppraisalResult? result;
            if (project is not null)
            {
                // Condo block selects by Room(Filter1)+Floor(Filter2); land/building block by Plot(Filter2).
                var selector = new UnitSelector(
                    PlotNumber: query.Filter2,
                    RoomNumber: query.Filter1,
                    FloorNumber: query.Filter2);
                var unit = await AppraisalResultBuilder.ResolveBlockUnitAsync(
                    conn, project, selector, strict: false, cancellationToken);
                if (unit is null) return Empty();
                result = LegacyResultMapper.MapBlock(header, assignment, fee, valuation, project, unit);
            }
            else
            {
                var groupParams = new DynamicParameters();
                groupParams.Add("AppraisalId", header.Id);
                var rows = (await conn.QueryAsync<CollateralRow>(
                    new CommandDefinition(GetAppraisalResultSql.GroupsAndCollaterals, groupParams,
                        cancellationToken: cancellationToken))).ToList();

                // Condo: single-row match on Room(Filter1)+Floor(Filter2).
                var condo = LegacyResultMapper.SelectCondo(rows, query.Filter1, query.Filter2);
                if (condo is not null)
                {
                    result = LegacyResultMapper.MapCondo(header, assignment, fee, valuation, condo);
                }
                else
                {
                    // Non-condo: House(Filter1) is on the building row, Title(Filter2) on the land row —
                    // possibly two rows in the same group — so match at group level and combine.
                    var group = LegacyResultMapper.SelectNonCondoGroup(rows, query.Filter1, query.Filter2);
                    if (group is null) return Empty();
                    result = LegacyResultMapper.MapNonCondoCombined(
                        header, assignment, fee, valuation, group.Value.Land, group.Value.Building, group.Value.GroupLandValue);
                }
            }

            return new LegacyAppraisalResultEnvelope(Success, result);
        }
        catch
        {
            // Legacy contract: never surface a 500 — any failure is an empty ResultCode = 0 result.
            return Empty();
        }
    }

    private static LegacyAppraisalResultEnvelope Empty() =>
        new(NotFoundOrError, new LegacyAppraisalResult());
}

internal static class LegacyResultMapper
{
    private static readonly string[] CondoCodes = ["U", "LSU"];
    private static readonly string[] LandCodes = ["L", "LB", "LSL", "LS"];
    private static readonly string[] BuildingCodes = ["B", "LB", "LSB", "LS"];

    // Condo: single collateral row matched by Room(Filter1) + Floor(Filter2).
    public static CollateralRow? SelectCondo(IEnumerable<CollateralRow> rows, string? filter1, string? filter2)
    {
        foreach (var r in rows)
        {
            if (IsCondo(r.PropertyType) && Eq(r.RoomNo, filter1) && Eq(r.FloorNo, filter2))
                return r;
        }

        return null;
    }

    // Non-condo: House(Filter1) lives on the building row, Title(Filter2) on the land row. A collateral
    // may be one combined LB row OR a separate L + B pair sharing a PropertyGroup, so match at group
    // level and return the land-bearing and building-bearing rows (either may be null for pure L / B).
    public static (CollateralRow? Land, CollateralRow? Building, decimal? GroupLandValue)? SelectNonCondoGroup(
        IEnumerable<CollateralRow> rows, string? filter1, string? filter2)
    {
        var wantHouse = !string.IsNullOrWhiteSpace(filter1);
        var wantTitle = !string.IsNullOrWhiteSpace(filter2);

        foreach (var group in rows.Where(r => !IsCondo(r.PropertyType)).GroupBy(r => r.GroupId))
        {
            var landRow = group.FirstOrDefault(r => HasLand(r.PropertyType) && (!wantTitle || Eq(r.TitleNo, filter2)));
            var buildingRow = group.FirstOrDefault(r => HasBuilding(r.PropertyType) && (!wantHouse || Eq(r.HouseNo, filter1)));

            var titleOk = !wantTitle || landRow is not null;
            var houseOk = !wantHouse || buildingRow is not null;
            var anyMatched = (wantTitle && landRow is not null) || (wantHouse && buildingRow is not null);

            if (titleOk && houseOk && anyMatched)
                return (landRow, buildingRow, group.First().GroupLandValue);
        }

        return null;
    }

    public static LegacyAppraisalResult MapCondo(
        LegacyAppraisalRow header, AssignmentRow? assignment, decimal? fee, ValuationRow? valuation, CollateralRow row)
    {
        var appraisalValue = valuation?.AppraisedValue ?? 0m;
        var valuer = SplitValuer(assignment, appraisalValue, valuation?.ValuationDate);

        return BaseResult(header, fee, valuation, valuer) with
        {
            AppraisalValue = appraisalValue,
            ForceSaleValue = valuation?.ForcedSaleValue ?? 0m,
            LandOffice = Str(row.LandOfficeName ?? row.CadLandOfficeName),
            LandValue = row.GroupLandValue ?? 0m,
            LandNo = Str(row.LandNo),
            TitleNo = Str(row.CondoBuiltOnTitleNo),   // condo title = the land title it is built on
            Rawang = Str(row.Rawang),
            SurveyNo = Str(row.SurveyNo),
            BookNo = Str(row.BookNo),
            PageNo = Str(row.PageNo),
            Rai = row.Rai ?? 0m,
            Ngan = row.Ngan ?? 0m,
            Wah = row.Wa ?? 0m,
            RoomNo = Str(row.RoomNo),
            FloorNo = Str(row.FloorNo),                 // the unit's own floor
            FloorNumber = NumStr(row.CondoTotalFloor),  // total floors of the building
            BuildingNo = Str(row.BuildingNo),
            BuildingAge = row.CondoBuildingAge ?? row.BuildingAge ?? 0,
            AreaUtilize = row.AreaUtilize ?? 0m,
            BuildingDetails = Str(row.CondoName),
            BuildingRegisterNo = Str(row.CondoRegistrationNumber),
            Decorate = ParseDecorate(row.CondoDecorationType),
        };
    }

    // Combines a group's land-bearing row (title/land/area) and building-bearing row (house/building/
    // decorate). A combined LB row is passed as both `land` and `building`.
    public static LegacyAppraisalResult MapNonCondoCombined(
        LegacyAppraisalRow header, AssignmentRow? assignment, decimal? fee, ValuationRow? valuation,
        CollateralRow? land, CollateralRow? building, decimal? groupLandValue)
    {
        var appraisalValue = valuation?.AppraisedValue ?? 0m;
        var valuer = SplitValuer(assignment, appraisalValue, valuation?.ValuationDate);

        return BaseResult(header, fee, valuation, valuer) with
        {
            AppraisalValue = appraisalValue,
            ForceSaleValue = valuation?.ForcedSaleValue ?? 0m,
            LandOffice = Str(land?.LandOfficeName ?? building?.LandOfficeName),
            LandValue = groupLandValue ?? 0m,
            // Land row
            LandNo = Str(land?.LandNo),
            TitleNo = Str(land?.TitleNo),
            Rawang = Str(land?.Rawang),
            SurveyNo = Str(land?.SurveyNo),
            BookNo = Str(land?.BookNo),
            PageNo = Str(land?.PageNo),
            Rai = land?.Rai ?? 0m,
            Ngan = land?.Ngan ?? 0m,
            Wah = land?.Wa ?? 0m,
            // Building row
            HouseNo = Str(building?.HouseNo),
            BuildingNo = Str(building?.BuildingNo),
            BuildingAge = building?.BuildingAge ?? 0,
            FloorNumber = NumStr(building?.TotalFloor),   // total floors of the building
            AreaUtilize = building?.TotalBuildingArea ?? 0m,
            Decorate = ParseDecorate(building?.BuildingDecorationType),
            BuildingDetails = Str(FirstNonEmpty(land?.Village, building?.CondoName)),
        };
    }

    public static LegacyAppraisalResult MapBlock(
        LegacyAppraisalRow header, AssignmentRow? assignment, decimal? fee, ValuationRow? valuation,
        ProjectRow project, BlockUnitRow unit)
    {
        var isCondo = ProjectType.IsCondoCode(project.ProjectType);
        var (rai, ngan, wa) = SqWaToRaiNganWa(unit.LandArea);
        var appraisalValue = unit.TotalAppraisalValueRounded ?? valuation?.AppraisedValue ?? 0m;
        var valuer = SplitValuer(assignment, appraisalValue, valuation?.ValuationDate);

        return BaseResult(header, fee, valuation, valuer) with
        {
            AppraisalValue = appraisalValue,
            ForceSaleValue = unit.ForceSellingPrice ?? valuation?.ForcedSaleValue ?? 0m,
            // Block insurance = the unit's coverage amount (falls back to appraisal-level fire insurance).
            BuildingValue = unit.CoverageAmount ?? valuation?.InsuranceValue ?? 0m,
            // Block market value = the unit's own selling price (falls back to the request-level total).
            MarketValue = unit.SellingPrice ?? header.MarketValue ?? 0m,
            // Project-level descriptors.
            BuildingDetails = Str(project.ProjectName),
            Developer = Str(project.Developer),
            TitleNo = Str(project.BuiltOnTitleDeedNumber),
            LandOffice = Str(project.LandOfficeName),
            // Per-unit identity.
            LandNo = isCondo ? "" : Str(unit.PlotNumber),
            HouseNo = isCondo ? "" : Str(unit.HouseNumber),
            RoomNo = isCondo ? Str(unit.UnitRoomNo ?? unit.RoomNumber) : "",
            FloorNo = isCondo ? IntStr(unit.Floor) : "",                       // the unit's own floor
            FloorNumber = IntStr(unit.TowerFloors ?? unit.NumberOfFloors),     // total floors (from the tower)
            BuildingAge = unit.TowerBuildingAge ?? 0,
            BuildingNo = isCondo ? Str(unit.TowerName) : "",
            BuildingRegisterNo = isCondo ? Str(unit.CondoRegistrationNumber) : "",
            Decorate = ParseDecorate(unit.DecorationType),
            AreaUtilize = unit.UsableArea ?? 0m,
            Rai = rai,
            Ngan = ngan,
            Wah = wa,
        };
    }

    // Thai land area: 1 rai = 4 ngan = 400 sq.wa; 1 ngan = 100 sq.wa. Splits a total in sq.wa.
    private static (decimal Rai, decimal Ngan, decimal Wa) SqWaToRaiNganWa(decimal? totalSqWa)
    {
        if (totalSqWa is not { } total || total <= 0m) return (0m, 0m, 0m);

        var rai = Math.Floor(total / 400m);
        var afterRai = total - rai * 400m;
        var ngan = Math.Floor(afterRai / 100m);
        var wa = afterRai - ngan * 100m;
        return (rai, ngan, wa);
    }

    // Appraisal-level fields shared by both paths (identity, fee, dates, address, valuer, type).
    private static LegacyAppraisalResult BaseResult(
        LegacyAppraisalRow header, decimal? fee, ValuationRow? valuation, ValuerSplit valuer)
    {
        var appraisalDate = IsoDate(valuation?.ValuationDate ?? header.CompletedAt);

        return new LegacyAppraisalResult
        {
            AppraisalReportNo = Str(header.AppraisalNumber),
            AppraisalFee = fee ?? 0m,
            SequenceOfApprove = Str(header.SequenceOfApprove),
            AppraisalType = MapAppraisalType(header.AppraisalType),
            MethodOfAppraisal = 1, // TODO revisit: currently a fixed default per the legacy contract.
            MarketValue = header.MarketValue ?? 0m,
            BuildingValue = valuation?.InsuranceValue ?? 0m, // legacy BuildingValue = fire-insurance value
            Province = Str(header.Province),
            District = Str(header.District),
            SubDistrict = Str(header.SubDistrict),
            AppraisalDate = appraisalDate,
            InternalValuerCode = valuer.InternalCode,
            InternalValuerName = valuer.InternalName,
            InternalValuation = valuer.InternalValue,
            InternalValuationDate = valuer.InternalDate,
            ExternalValuerCode = valuer.ExternalCode,
            ExternalValuerName = valuer.ExternalName,
            ExternalValuation = valuer.ExternalValue,
            ExternalValuationDate = valuer.ExternalDate,
        };
    }

    // The internal/external valuation amount mirrors the result's AppraisalValue (per-unit for block),
    // so the caller passes the resolved appraisal value rather than reading ValuationAnalyses directly.
    private static ValuerSplit SplitValuer(AssignmentRow? assignment, decimal appraisalValue, DateTime? valuationDate)
    {
        if (assignment is null) return new ValuerSplit();

        var value = appraisalValue;
        var date = IsoDate(valuationDate);

        if (string.Equals(assignment.AssignmentType, AssignmentType.External.Code, StringComparison.OrdinalIgnoreCase))
        {
            return new ValuerSplit
            {
                ExternalCode = Str(assignment.CompanyCode),
                ExternalName = Str(assignment.CompanyName),
                ExternalValue = value,
                ExternalDate = date,
            };
        }

        if (string.Equals(assignment.AssignmentType, AssignmentType.Internal.Code, StringComparison.OrdinalIgnoreCase))
        {
            var fullName = $"{assignment.UserFirstName} {assignment.UserLastName}".Trim();
            return new ValuerSplit
            {
                InternalCode = Str(assignment.EmployeeId),
                InternalName = fullName,
                InternalValue = value,
                InternalDate = date,
            };
        }

        return new ValuerSplit();
    }

    private static int MapAppraisalType(string? type) => type switch
    {
        AppraisalTypes.New => 1,
        AppraisalTypes.ReAppraisal => 2,
        AppraisalTypes.Progressive => 3,
        AppraisalTypes.PreAppraisal => 4,
        _ => 0,
    };

    // Legacy Decorate is our DecorationType code with the leading zero stripped ("01" → 1, "99" → 99).
    // Returns null when there is no usable value (the legacy contract wants null, not 0, for "unknown").
    private static int? ParseDecorate(string? code) =>
        int.TryParse(code, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static bool IsCondo(string? propertyType) =>
        propertyType is not null && CondoCodes.Contains(propertyType, StringComparer.OrdinalIgnoreCase);

    private static bool HasLand(string? propertyType) =>
        propertyType is not null && LandCodes.Contains(propertyType, StringComparer.OrdinalIgnoreCase);

    private static bool HasBuilding(string? propertyType) =>
        propertyType is not null && BuildingCodes.Contains(propertyType, StringComparer.OrdinalIgnoreCase);

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static bool Eq(string? a, string? b) =>
        string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase);

    private static string Str(string? value) => value ?? string.Empty;

    private static string IntStr(int? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    // Formats a numeric count as a string, dropping the decimal part when it is a whole number.
    private static string NumStr(decimal? value) =>
        value is null
            ? string.Empty
            : (value.Value == Math.Truncate(value.Value)
                ? ((long)value.Value).ToString(CultureInfo.InvariantCulture)
                : value.Value.ToString(CultureInfo.InvariantCulture));

    private static string IsoDate(DateTime? value) =>
        value?.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;

    private sealed record ValuerSplit
    {
        public string InternalCode { get; init; } = "";
        public string InternalName { get; init; } = "";
        public decimal InternalValue { get; init; }
        public string InternalDate { get; init; } = "";
        public string ExternalCode { get; init; } = "";
        public string ExternalName { get; init; } = "";
        public decimal ExternalValue { get; init; }
        public string ExternalDate { get; init; } = "";
    }
}
