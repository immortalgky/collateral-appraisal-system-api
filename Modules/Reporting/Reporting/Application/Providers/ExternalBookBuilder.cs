using System.Data;
using Reporting.Application.Formatting;
using Reporting.Application.Models;

namespace Reporting.Application.Providers;

/// <summary>
/// Builds the EXTERNAL-company cover-page + company-letter fields of the unified appraisal book
/// (FSD §2.1.2 — เล่มรายงานประเมินบริษัท) onto an <see cref="AppraisalSummaryModel"/>.
///
/// Only the cover/letter fields are set here; the shared detail sections
/// (Land/Building/Condo/Construction/Machine/Comparison/WQS/SaleGrid/CostMachine/Appendix) are
/// loaded ONCE by <see cref="AppraisalBookDataProvider"/> regardless of internal/external, so this
/// builder deliberately does NOT call the section loaders.
///
/// Data is fetched in a single QueryMultiple batch (13 result sets, all off @AppraisalId) plus a
/// conditional company lookup — identical to the former ExternalReportDataProvider.
/// </summary>
internal static class ExternalBookBuilder
{
    private const string BankName = "ธนาคารแลนด์ แอนด์ เฮ้าส์ จำกัด (มหาชน)";
    private const string LetterSubject = "แจ้งผลการประเมินมูลค่าทรัพย์สิน";
    private const string ExternalAppraisalPurpose =
        "เพื่อใช้ในการพิจารณาขอสินเชื่อของ ธนาคารแลนด์ แอนด์ เฮ้าส์ จำกัด (มหาชน)";

    // ── Method type → Thai label map ─────────────────────────────────────────────
    private static readonly IReadOnlyDictionary<string, string> MethodTypeLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["WQS"]               = "วิธีเปรียบเทียบราคาตลาด (WQS)",
            ["SaleGrid"]          = "วิธีเปรียบเทียบตลาด",
            ["DirectComparison"]  = "วิธีเปรียบเทียบตลาด",
            ["BuildingCost"]      = "วิธีต้นทุน",
            ["MachineryCost"]     = "วิธีต้นทุน",
            ["Income"]            = "วิธีรายได้",
            ["Leasehold"]         = "วิธีสิทธิการเช่า",
            ["ProfitRent"]        = "วิธีกำไรจากค่าเช่า",
            ["Hypothesis"]        = "วิธีสมมติฐาน",
        };

    /// <summary>
    /// Builds the external cover/letter portion of <see cref="AppraisalSummaryModel"/> from an open
    /// connection. <c>IsExternal</c> is set to true; section props are left null.
    /// </summary>
    internal static async Task<AppraisalSummaryModel> BuildAsync(
        IDbConnection connection,
        Guid appraisalId,
        CancellationToken cancellationToken)
    {
        // ── Batch 1: 13 result sets, single round-trip (all keyed off @AppraisalId) ──
        const string batchSql = """
            -- RS01: Q1 — Appraisal header
            SELECT
                a.AppraisalNumber,
                a.RequestId,
                a.CompletedAt
            FROM appraisal.Appraisals a
            WHERE a.Id = @AppraisalId
              AND a.IsDeleted = 0;

            -- RS02: Q2 — Customer names (RequestId via subquery)
            SELECT rc.Name
            FROM request.RequestCustomers rc
            WHERE rc.RequestId = (
                SELECT a2.RequestId
                FROM appraisal.Appraisals a2
                WHERE a2.Id = @AppraisalId AND a2.IsDeleted = 0)
            ORDER BY rc.Name;

            -- RS03: Q3 — Latest non-cancelled External assignment
            SELECT TOP 1
                aa.AssigneeCompanyId,
                aa.ExternalAppraiserName
            FROM appraisal.AppraisalAssignments aa
            WHERE aa.AppraisalId = @AppraisalId
              AND aa.AssignmentType = 'External'
              AND aa.AssignmentStatus NOT IN ('Rejected', 'Cancelled')
            ORDER BY aa.AssignedAt DESC, aa.Id DESC;

            -- RS04: Q5 — Appraisal date (latest non-cancelled appointment)
            SELECT TOP 1 ap.AppointmentDateTime
            FROM appraisal.Appointments ap
            JOIN appraisal.AppraisalAssignments aa ON aa.Id = ap.AssignmentId
            WHERE aa.AppraisalId = @AppraisalId
              AND ap.Status <> 'Cancelled'
            ORDER BY ap.AppointmentDateTime DESC;

            -- RS05: Q6 — Property type counts
            SELECT
                ap.PropertyType,
                COUNT(*) AS PropertyCount
            FROM appraisal.AppraisalProperties ap
            WHERE ap.AppraisalId = @AppraisalId
            GROUP BY ap.PropertyType;

            -- RS06: Q13 — CollateralType code→Thai map
            SELECT [Code] AS Code, [Description] AS Description
            FROM parameter.Parameters
            WHERE [Group] = 'CollateralType' AND [Language] = 'TH' AND IsActive = 1;

            -- RS07: Q7 — Land rows (all land properties, ordered by sequence)
            SELECT
                lad.Id            AS LandDetailId,
                lad.OwnerName,
                lad.ObligationDetails,
                lad.Village,
                lad.Soi,
                lad.Street        AS Road,
                lad.SubDistrict,
                lad.District,
                lad.Province,
                lad.Latitude,
                lad.Longitude
            FROM appraisal.LandAppraisalDetails lad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY ap.SequenceNumber;

            -- RS08: Land titles (all titles across all land properties)
            SELECT
                lt.TitleNumber,
                lt.AreaRai,
                lt.AreaNgan,
                lt.AreaSquareWa
            FROM appraisal.LandTitles lt
            JOIN appraisal.LandAppraisalDetails lad ON lad.Id = lt.LandAppraisalDetailId
            JOIN appraisal.AppraisalProperties ap ON ap.Id = lad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY lad.Id, lt.Id;

            -- RS09: Q8 — Condo rows
            SELECT
                cad.OwnerName       AS CondoOwner,
                cad.ObligationDetails AS CondoObligationDetails,
                cad.RoomNumber,
                cad.FloorNumber,
                cad.CondoName,
                cad.Soi             AS CondoSoi,
                cad.Street          AS CondoRoad,
                cad.SubDistrict     AS CondoSubDistrict,
                cad.District        AS CondoDistrict,
                cad.Province        AS CondoProvince,
                cad.Latitude        AS CondoLatitude,
                cad.Longitude       AS CondoLongitude,
                cad.UsableArea
            FROM appraisal.CondoAppraisalDetails cad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = cad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY ap.SequenceNumber;

            -- RS10: Q9 — Building detail (TOP 1, first by sequence)
            SELECT TOP 1
                COALESCE(pt.Description, bad.BuildingType)  AS BuildingTypeDisplay,
                bad.NumberOfFloors,
                bad.ModelName,
                bad.OwnerName                               AS BuildingOwner
            FROM appraisal.BuildingAppraisalDetails bad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
            LEFT JOIN parameter.Parameters pt
                ON pt.[Group] = 'BuildingType'
               AND pt.[Language] = 'TH'
               AND pt.[Code] = bad.BuildingType
               AND pt.IsActive = 1
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY ap.SequenceNumber;

            -- RS11: Q10 — Machinery registration numbers
            SELECT mad.RegistrationNumber
            FROM appraisal.MachineryAppraisalDetails mad
            JOIN appraisal.AppraisalProperties ap ON ap.Id = mad.AppraisalPropertyId
            WHERE ap.AppraisalId = @AppraisalId
              AND mad.RegistrationNumber IS NOT NULL
              AND mad.RegistrationNumber <> ''
            ORDER BY ap.SequenceNumber;

            -- RS12: Q11 — Valuation totals
            SELECT
                va.AppraisedValue,
                va.ForcedSaleValue,
                va.InsuranceValue
            FROM appraisal.ValuationAnalyses va
            WHERE va.AppraisalId = @AppraisalId;

            -- RS13: Q12 — Distinct pricing method types
            SELECT DISTINCT pam.MethodType
            FROM appraisal.PricingAnalysisMethods pam
            JOIN appraisal.PricingAnalysisApproaches paa ON paa.Id = pam.ApproachId
            JOIN appraisal.PricingAnalysis pa ON pa.Id = paa.PricingAnalysisId
            JOIN appraisal.PropertyGroups pg ON pg.Id = pa.AnchorId
            WHERE pg.AppraisalId = @AppraisalId
              AND pa.SubjectType = 0;
            """;

        var batchParams = new DynamicParameters();
        batchParams.Add("AppraisalId", appraisalId);

        HeaderRow? header;
        List<string> customerNames;
        AssignmentRow? assignment;
        DateTime? appraisalDate;
        List<PropertyCountRow> propCounts;
        List<ParamRow> collateralTypeParams;
        List<LandRow> landRows;
        List<TitleRow> titleRows;
        List<CondoRow> condoRows;
        BuildingRow? building;
        List<string> machineRegs;
        ValuationRow? valuation;
        HashSet<string> methodTypes;

        using (var multi = await connection.QueryMultipleAsync(batchSql, batchParams))
        {
            header = await multi.ReadFirstOrDefaultAsync<HeaderRow>();
            if (header is null)
                throw new NotFoundException("Appraisal", appraisalId.ToString());

            customerNames = (await multi.ReadAsync<string>()).ToList();
            assignment = await multi.ReadFirstOrDefaultAsync<AssignmentRow>();
            appraisalDate = await multi.ReadFirstOrDefaultAsync<DateTime?>();
            propCounts = (await multi.ReadAsync<PropertyCountRow>()).ToList();
            collateralTypeParams = (await multi.ReadAsync<ParamRow>()).ToList();
            landRows = (await multi.ReadAsync<LandRow>()).ToList();
            titleRows = (await multi.ReadAsync<TitleRow>()).ToList();
            condoRows = (await multi.ReadAsync<CondoRow>()).ToList();
            building = await multi.ReadFirstOrDefaultAsync<BuildingRow>();
            machineRegs = (await multi.ReadAsync<string>()).ToList();
            valuation = await multi.ReadFirstOrDefaultAsync<ValuationRow>();
            methodTypes = (await multi.ReadAsync<string>())
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var customerName = customerNames.Count > 0
            ? string.Join(" และ ", customerNames)
            : null;

        // ── Company details (conditional on a valid AssigneeCompanyId Guid) ──────────
        string? companyName = null;
        string? companyAddress = null;
        string? companyTel = null;

        if (assignment is not null
            && !string.IsNullOrWhiteSpace(assignment.AssigneeCompanyId)
            && Guid.TryParse(assignment.AssigneeCompanyId, out var companyGuid))
        {
            const string companySql = """
                SELECT
                    c.Name,
                    c.Phone,
                    c.AddressLine1,
                    c.AddressLine2
                FROM auth.Companies c
                WHERE c.Id = @CompanyId
                """;

            var companyParams = new DynamicParameters();
            companyParams.Add("CompanyId", companyGuid);

            var company = await connection.QueryFirstOrDefaultAsync<CompanyRow>(companySql, companyParams);
            if (company is not null)
            {
                companyName = company.Name;
                companyTel = company.Phone;
                var addrParts = new List<string?> { company.AddressLine1, company.AddressLine2 }
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();
                companyAddress = addrParts.Count > 0 ? string.Join(" ", addrParts) : null;
            }
        }

        // ── Compose derived fields ───────────────────────────────────────────────────
        var collateralTypeMap = collateralTypeParams
            .Where(p => !string.IsNullOrWhiteSpace(p.Code))
            .GroupBy(p => p.Code!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Description, StringComparer.OrdinalIgnoreCase);

        // GPS: prefer land → condo
        string? gps = null;
        var firstLand = landRows.FirstOrDefault();
        if (firstLand?.Latitude is not null && firstLand.Longitude is not null)
            gps = $"Lat : {firstLand.Latitude:F6}  Lon : {firstLand.Longitude:F6}";
        else
        {
            var firstCondo = condoRows.FirstOrDefault();
            if (firstCondo?.CondoLatitude is not null && firstCondo.CondoLongitude is not null)
                gps = $"Lat : {firstCondo.CondoLatitude:F6}  Lon : {firstCondo.CondoLongitude:F6}";
        }

        // Collateral location: prefer condo FormatCondo, else land FormatLandBuilding
        string? collateralLocation = null;
        if (condoRows.Count > 0)
        {
            var c = condoRows.First();
            var fmt = ThaiAddressFormatter.FormatCondo(
                roomNumber: c.RoomNumber,
                floorNumber: c.FloorNumber,
                buildingName: c.CondoName,
                soi: c.CondoSoi,
                road: c.CondoRoad,
                subDistrict: c.CondoSubDistrict,
                district: c.CondoDistrict,
                province: c.CondoProvince);
            collateralLocation = string.IsNullOrEmpty(fmt) ? null : fmt;
        }
        else if (firstLand is not null)
        {
            var fmt = ThaiAddressFormatter.FormatLandBuilding(
                houseNumber: null,
                village: firstLand.Village,
                moo: null,
                soi: firstLand.Soi,
                road: firstLand.Road,
                subDistrict: firstLand.SubDistrict,
                district: firstLand.District,
                province: firstLand.Province);
            collateralLocation = string.IsNullOrEmpty(fmt) ? null : fmt;
        }

        // Title deed numbers and land area
        var titleNumbers = titleRows
            .Where(t => !string.IsNullOrWhiteSpace(t.TitleNumber))
            .Select(t => t.TitleNumber!)
            .Distinct()
            .ToList();

        string? titleDeedNumbers = titleNumbers.Count > 0 ? string.Join(", ", titleNumbers) : null;
        int? totalTitleDeeds = titleRows.Count > 0 ? titleRows.Count : null;

        // Show the area line whenever any title exists (preserves the original output, including
        // a "0 - 0 - 0" line for titles with no captured area); hide it only when there are no titles.
        string? landAreaText = titleRows.Count > 0
            ? ThaiLandAreaFormatter.FormatTotal(
                titleRows.Sum(t => t.AreaRai ?? 0m),
                titleRows.Sum(t => t.AreaNgan ?? 0m),
                titleRows.Sum(t => t.AreaSquareWa ?? 0m))
            : null;

        // Owners
        string? landOwner = firstLand?.OwnerName;
        string? condoOwner = condoRows.Count > 0 ? condoRows.First().CondoOwner : null;
        string? buildingOwner = building?.BuildingOwner;

        // Building details text
        string? buildingDetailsText = null;
        if (building is not null)
        {
            var bParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(building.BuildingTypeDisplay))
                bParts.Add(building.BuildingTypeDisplay);
            if (building.NumberOfFloors.HasValue)
                bParts.Add($"{building.NumberOfFloors:0.##} ชั้น");
            if (!string.IsNullOrWhiteSpace(building.ModelName))
                bParts.Add($"แบบ {building.ModelName}");
            buildingDetailsText = bParts.Count > 0 ? string.Join(" ", bParts) : null;
        }

        // Machinery registration numbers
        string? machineRegNumbers = machineRegs.Count > 0 ? string.Join(", ", machineRegs) : null;

        // Obligation: prefer land, fall back to condo
        string? obligation = firstLand?.ObligationDetails;
        if (string.IsNullOrWhiteSpace(obligation))
            obligation = condoRows.FirstOrDefault()?.CondoObligationDetails;

        // Property-type summary
        var condoUsableAreaTotal = condoRows.Sum(c => c.UsableArea ?? 0m);
        var landSqWaTotal = titleRows.Sum(t => t.AreaSquareWa ?? 0m);

        var summarySections = new List<string>();
        foreach (var pc in propCounts.OrderBy(p => p.PropertyType))
        {
            // PropertyType stores domain family codes — translate via representative CollateralType code.
            var typeThai = CollateralFamilyTranslator.ToThai(pc.PropertyType, collateralTypeMap) ?? "";

            string? section;
            if (CollateralFamilyTranslator.IsCondoFamily(pc.PropertyType))
            {
                section = condoUsableAreaTotal > 0
                    ? $"{typeThai} จำนวน {pc.PropertyCount} ยูนิต เนื้อที่ {condoUsableAreaTotal:0.##} ตารางเมตร"
                    : $"{typeThai} จำนวน {pc.PropertyCount} ยูนิต";
            }
            else if (CollateralFamilyTranslator.IsEquipmentFamily(pc.PropertyType))
            {
                section = $"{typeThai} จำนวน {pc.PropertyCount} เครื่อง";
            }
            else
            {
                section = landSqWaTotal > 0
                    ? $"{typeThai} จำนวน {pc.PropertyCount} หลัง เนื้อที่ {landSqWaTotal:0.##} ตารางวา"
                    : $"{typeThai} จำนวน {pc.PropertyCount} หลัง";
            }
            summarySections.Add(section);
        }

        string? propertyTypeSummary = summarySections.Count > 0
            ? string.Join(", ", summarySections)
            : null;

        // Price method Thai labels (distinct, preserve insertion order of MethodTypeLabels map)
        var methodLabels = MethodTypeLabels.Keys
            .Where(k => methodTypes.Contains(k))
            .Select(k => MethodTypeLabels[k])
            .Distinct()
            .ToList();
        string? priceMethod = methodLabels.Count > 0 ? string.Join(", ", methodLabels) : null;

        // Verify date: CompletedAt (external verify) or appraisalDate fallback
        DateTime? verifyDate = header.CompletedAt ?? appraisalDate;

        // ── Assemble model (cover/letter fields only; sections loaded by the provider) ──
        return new AppraisalSummaryModel
        {
            IsExternal = true,
            BankName = BankName,
            Subject = LetterSubject,
            AppraisalPurpose = ExternalAppraisalPurpose,

            CompanyName = companyName,
            CompanyAddress = companyAddress,
            CompanyTel = companyTel,
            CustomerName = customerName,
            AppraisalBookNumber = header.AppraisalNumber,
            PropertyTypeSummary = propertyTypeSummary,
            CollateralLocation = collateralLocation,
            VerifyDate = verifyDate,
            Gps = gps,
            TitleDeedNumbers = titleDeedNumbers,
            TotalTitleDeeds = totalTitleDeeds,
            LandAreaText = landAreaText,
            LandOwner = landOwner,
            BuildingDetailsText = buildingDetailsText,
            BuildingOwner = buildingOwner,
            CondoOwner = condoOwner,
            MachineRegistrationNumbers = machineRegNumbers,
            Obligation = string.IsNullOrWhiteSpace(obligation) ? null : obligation,
            PriceMethod = priceMethod,
            AppraisalDate = appraisalDate,
            CollateralValue = valuation?.AppraisedValue,
            ForcedSaleValue = valuation?.ForcedSaleValue,
            BuildingCoverageAmount = valuation?.InsuranceValue,
            SurveyorName = assignment?.ExternalAppraiserName,
            CheckerName = null,      // Deferred: no source column in current schema
            VerifyName = null,       // Deferred: no source column in current schema
            VerifyLicenseNo = null,  // Deferred: no source column in current schema
            DirectorName = null,     // Deferred: no source column in current schema
        };
    }

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    private sealed class HeaderRow
    {
        public string? AppraisalNumber { get; init; }
        public Guid RequestId { get; init; }
        public DateTime? CompletedAt { get; init; }
    }

    private sealed class AssignmentRow
    {
        public string? AssigneeCompanyId { get; init; }
        public string? ExternalAppraiserName { get; init; }
    }

    private sealed class CompanyRow
    {
        public string? Name { get; init; }
        public string? Phone { get; init; }
        public string? AddressLine1 { get; init; }
        public string? AddressLine2 { get; init; }
    }

    private sealed class PropertyCountRow
    {
        public string? PropertyType { get; init; }
        public int PropertyCount { get; init; }
    }

    private sealed class LandRow
    {
        public Guid LandDetailId { get; init; }
        public string? OwnerName { get; init; }
        public string? ObligationDetails { get; init; }
        public string? Village { get; init; }
        public string? Soi { get; init; }
        public string? Road { get; init; }
        public string? SubDistrict { get; init; }
        public string? District { get; init; }
        public string? Province { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
    }

    private sealed class TitleRow
    {
        public string? TitleNumber { get; init; }
        public decimal? AreaRai { get; init; }
        public decimal? AreaNgan { get; init; }
        public decimal? AreaSquareWa { get; init; }
    }

    private sealed class CondoRow
    {
        public string? CondoOwner { get; init; }
        public string? CondoObligationDetails { get; init; }
        public string? RoomNumber { get; init; }
        public string? FloorNumber { get; init; }
        public string? CondoName { get; init; }
        public string? CondoSoi { get; init; }
        public string? CondoRoad { get; init; }
        public string? CondoSubDistrict { get; init; }
        public string? CondoDistrict { get; init; }
        public string? CondoProvince { get; init; }
        public decimal? CondoLatitude { get; init; }
        public decimal? CondoLongitude { get; init; }
        public decimal? UsableArea { get; init; }
    }

    private sealed class BuildingRow
    {
        public string? BuildingTypeDisplay { get; init; }
        public decimal? NumberOfFloors { get; init; }
        public string? ModelName { get; init; }
        public string? BuildingOwner { get; init; }
    }

    private sealed class ValuationRow
    {
        public decimal? AppraisedValue { get; init; }
        public decimal? ForcedSaleValue { get; init; }
        public decimal? InsuranceValue { get; init; }
    }

    private sealed class ParamRow
    {
        public string? Code { get; init; }
        public string? Description { get; init; }
    }
}
