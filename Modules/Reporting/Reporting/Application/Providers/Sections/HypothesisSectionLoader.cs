using System.Data;
using System.Globalization;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the "วิธีสมมติฐาน (Hypothesis)" section model — the summary ledger exactly as the pricing
/// screen lays it out (LandBuildingSummaryTab / CondominiumSummaryTab: same bands, rows, Thai labels
/// and rate / qty / total / ratio cells), the per-model analysis and the uploaded unit listing.
///
/// Data sources (Dapper read-only):
///   Q1  PricingMethodPicker — which Hypothesis methods print.
///   Q2  appraisal.HypothesisAnalyses (ha.*) + PricingFinalValues.IndicatedValue (the typed-over total).
///       Owned summary columns are EF-auto-named LandBuildingSummary_{Prop} / CondominiumSummary_{Prop}
///       (HypothesisAnalysisConfiguration.cs); read by name from the row so all ~130 need no alias.
///   Q3  HypothesisLandBuildingUnitRows / HypothesisCondominiumUnitRows — the unit listing.
///   L&amp;B only:
///   Q4  HypothesisCostItems Kind = Other (99), Category ProjectDevCost / ProjectCost — the appraiser's
///       own lines the ledger prints inside those bands. The seeded kinds mirror summary fields and the
///       CostOfBuilding kind no longer feeds the value (DataFix 20260921180000), so neither prints again.
///   Q5  HypothesisModelBuildingMappings + the mapped building's Final Cost Value — the per-house cost
///       of each house model (FSD C19/C21). Same rule as PricingPropertyDataService.BuildingFinalCostValuesSql
///       (override, else depreciated sum rounded to 1,000; only buildings of this group count).
///
/// Returns an empty list when no Hypothesis method exists for the appraisal.
/// </summary>
internal static class HypothesisSectionLoader
{
    public static async Task<IReadOnlyList<HypothesisSection>> LoadAllAsync(
        IDbConnection connection,
        Guid appraisalId,
        CancellationToken ct = default)
    {
        var methodsPerGroup = await PricingMethodPicker.LoadAsync(connection, appraisalId, ["Hypothesis"], ct);
        if (methodsPerGroup.Count == 0)
            return [];

        var sections = new List<HypothesisSection>(methodsPerGroup.Count);
        foreach (var method in methodsPerGroup)
        {
            var section = await LoadOneAsync(connection, method);
            if (section is not null)
                sections.Add(section);
        }

        return sections;
    }

    private static async Task<HypothesisSection?> LoadOneAsync(IDbConnection connection, PickedMethod method)
    {
        var mp = new DynamicParameters();
        mp.Add("MethodId", method.PricingMethodId);
        mp.Add("GroupId", method.PropertyGroupId);

        const string headerSql = """
            SELECT ha.*, pfv.IndicatedValue AS IndicatedValue
            FROM appraisal.HypothesisAnalyses ha
            LEFT JOIN appraisal.PricingFinalValues pfv ON pfv.PricingMethodId = ha.PricingMethodId
            WHERE ha.PricingMethodId = @MethodId
            """;
        var raw = (object?)await connection.QuerySingleOrDefaultAsync(headerSql, mp);
        if (raw is not IDictionary<string, object> header)
            return null;

        var isCondo = ToDecimal(header, "Variant") == 2m;

        const string lbUnitsSql = """
            SELECT r.SequenceNumber AS Sequence, r.PlanNo, r.HouseNo, r.ModelName AS Model, r.Location, r.FloorNo,
                   r.LandAreaSqWa, r.UsableAreaSqM, r.SellingPrice, r.Remark1, r.Remark2
            FROM appraisal.HypothesisLandBuildingUnitRows r
            JOIN appraisal.HypothesisAnalyses ha ON ha.Id = r.HypothesisAnalysisId
            WHERE ha.PricingMethodId = @MethodId
            ORDER BY r.SequenceNumber, r.Id
            """;
        const string condoUnitsSql = """
            SELECT r.SequenceNumber AS Sequence, r.FloorNo, r.Building, r.AptNo, r.Apartment, r.ModelType AS Model,
                   r.UsableAreaSqM, r.SellingPrice, r.Remark1, r.Remark2
            FROM appraisal.HypothesisCondominiumUnitRows r
            JOIN appraisal.HypothesisAnalyses ha ON ha.Id = r.HypothesisAnalysisId
            WHERE ha.PricingMethodId = @MethodId
            ORDER BY r.SequenceNumber, r.Id
            """;
        var units = (await connection.QueryAsync<HypothesisUnitRow>(isCondo ? condoUnitsSql : lbUnitsSql, mp)).ToList();

        if (isCondo)
            return Build(method, header, units, [], []);

        const string otherItemsSql = """
            SELECT ci.Category, ci.Description, ci.Amount, ci.CategoryRatio
            FROM appraisal.HypothesisCostItems ci
            JOIN appraisal.HypothesisAnalyses ha ON ha.Id = ci.HypothesisAnalysisId
            WHERE ha.PricingMethodId = @MethodId AND ci.Kind = 99 AND ci.Category IN (2, 3)
            ORDER BY ci.Category, ci.DisplaySequence, ci.Id
            """;
        var others = (await connection.QueryAsync<OtherCostRow>(otherItemsSql, mp)).ToList();

        const string mappingsSql = """
            SELECT m.ModelName, m.AppraisalPropertyId, m.TotalCost,
                   b.PropertyName AS BuildingName, b.SequenceNumber AS BuildingSequence, b.FinalCostValue
            FROM appraisal.HypothesisModelBuildingMappings m
            JOIN appraisal.HypothesisAnalyses ha ON ha.Id = m.HypothesisAnalysisId
            OUTER APPLY (
                SELECT bad.PropertyName, ap.SequenceNumber,
                       ISNULL(COALESCE(bad.BuildingCostValue, ROUND(SUM(bdd.PriceAfterDepreciation), -3)), 0) AS FinalCostValue
                FROM appraisal.BuildingAppraisalDetails bad
                JOIN appraisal.AppraisalProperties ap ON ap.Id = bad.AppraisalPropertyId
                JOIN appraisal.PropertyGroupItems pgi ON pgi.AppraisalPropertyId = ap.Id AND pgi.PropertyGroupId = @GroupId
                LEFT JOIN appraisal.BuildingDepreciationDetails bdd ON bdd.BuildingAppraisalDetailId = bad.Id
                WHERE bad.AppraisalPropertyId = m.AppraisalPropertyId
                GROUP BY bad.Id, bad.PropertyName, ap.SequenceNumber, bad.BuildingCostValue
            ) b
            WHERE ha.PricingMethodId = @MethodId
            """;
        var mappings = (await connection.QueryAsync<MappingRow>(mappingsSql, mp)).ToList();

        return Build(method, header, units, others, mappings);
    }

    // ── Pure assembly (unit-tested) ─────────────────────────────────────────────────

    internal static HypothesisSection Build(
        PickedMethod method,
        IDictionary<string, object> header,
        IReadOnlyList<HypothesisUnitRow> units,
        IReadOnlyList<OtherCostRow> others,
        IReadOnlyList<MappingRow> mappings)
    {
        var isCondo = ToDecimal(header, "Variant") == 2m;
        var models = Models(units);
        var indicated = ToDecimal(header, "IndicatedValue");

        return new HypothesisSection
        {
            GroupNumber = method.GroupNumber,
            GroupName = method.GroupName,
            Context = method.Context,
            Variant = isCondo ? 2 : 1,
            HasRatio = !isCondo,
            Bands = isCondo
                ? CondoBands(new Summary(header, "CondominiumSummary_"), indicated)
                : LandBuildingBands(new Summary(header, "LandBuildingSummary_"), indicated, models, others, mappings),
            Models = models,
            Units = units,
            UnitTotal = units.Count == 0 ? null : Aggregate("รวม", units),
        };
    }

    private static List<HypothesisLedgerBand> LandBuildingBands(
        Summary s, decimal? indicated, IReadOnlyList<HypothesisModelRow> models,
        IReadOnlyList<OtherCostRow> others, IReadOnlyList<MappingRow> mappings)
    {
        const string sqWa = "ตร.วา", unit = "หน่วย", ofRevenue = "ของรายได้รวม";

        var land = new List<HypothesisLedgerRow>
        {
            Row("พื้นที่รวม", qty: U(N(s["TotalArea"]), sqWa)),
            Row("พื้นที่ขาย", rate: Pct(s["SellingAreaPercent"], "ของพื้นที่รวม"), qty: U(N(s["SellingArea"]), sqWa)),
        };
        land.AddRange(models.Select(m => Sub(m.Name,
            rate: $"{I(m.Units)} {unit} · เฉลี่ย {N(m.AvgLandAreaSqWa)} {sqWa}", qty: U(N(m.TotalLandAreaSqWa), sqWa))));
        land.Add(Row("พื้นที่สาธารณูปโภค", rate: Pct(s["PublicUtilityAreaPercent"], "ของพื้นที่รวม"), qty: U(N(s["PublicUtilityArea"]), sqWa)));
        AddRemark(land, s.Text("Remark"));

        var rev = new List<HypothesisLedgerRow>();
        if (models.Count > 0)
            rev.Add(Row("แบบบ้าน", rate: "ตามแบบบ้านจากไฟล์ที่อัปโหลด"));
        rev.AddRange(models.Select(m => Sub(m.Name, rate: U(I(m.Units), unit), total: N(m.TotalSellingPrice))));
        rev.Add(Tot("รวมประมาณการรายได้โครงการ", N(s["TotalRevenue"])));

        // C19–C21 per house model: the mapped building's value per house × units, or the typed-over total.
        var totalPdc = s["TotalProjectDevCost"];
        var pdc = new List<HypothesisLedgerRow>();
        if (models.Count > 0)
            pdc.Add(Row("ต้นทุนก่อสร้างต่อหลัง", rate: "จากอาคารที่จับคู่ในแท็บต้นทุนอาคาร"));
        foreach (var m in models)
        {
            var map = mappings.FirstOrDefault(x => SameModel(x.ModelName, m.Name));
            var perHouse = map?.FinalCostValue ?? 0m;
            var modelTotal = map?.TotalCost ?? perHouse * m.Units;
            var building = map?.BuildingName is { Length: > 0 } bn ? bn
                : map?.BuildingSequence is { } seq ? $"อาคาร {seq}" : null;
            var flag = (map?.TotalCost is not null ? " (แก้ยอดรวมเอง)" : "")
                       + (map?.AppraisalPropertyId is null ? " (ยังไม่เลือกอาคาร)" : "");
            pdc.Add(Sub(m.Name + (building is null ? "" : $" · {building}") + flag,
                rate: U(N(perHouse), "บาท/หน่วย"), qty: U(I(m.Units), unit), total: N(modelTotal),
                ratio: totalPdc is > 0m ? Pct(modelTotal * 100m / totalPdc) : null));
        }
        pdc.Add(Row("ค่าก่อสร้างสาธารณูปโภค", U(N(s["PublicUtilityRatePerSqWa"]), "บาท/ตร.วา"),
            U(N(s["PublicUtilityAreaForCost"]), sqWa), N(s["PublicUtilityCost"]), Pct(s["PublicUtilityCostRatio"])));
        pdc.Add(Row("ค่าถมดิน (ส่วนที่เหลือ)", U(N(s["LandFillingRatePerSqWa"]), "บาท/ตร.วา"),
            U(N(s["LandFillingArea"]), sqWa), N(s["LandFillingCost"]), Pct(s["LandFillingCostRatio"])));
        pdc.AddRange(OtherRows(others, 2));
        pdc.Add(Row("เงินสำรองเผื่อเหลือเผื่อขาด", Pct(s["ContingencyPercent"], "ของต้นทุนพัฒนาโครงการ"),
            total: N(s["ContingencyAmount"]), ratio: Pct(s["ContingencyRatio"])));
        pdc.Add(Tot("รวมประมาณการต้นทุนพัฒนาโครงการ", N(totalPdc), Pct(s["TotalDevCostRatio"])));

        var pc = new List<HypothesisLedgerRow>
        {
            Row("ค่าธรรมเนียมขออนุญาตจัดสรร", U(N(s["AllocationPermitFee"]), "บาท"),
                total: N(s["AllocationPermitFee"]), ratio: Pct(s["AllocationPermitFeeRatio"])),
            Row("ค่าแบ่งแยกโฉนด", U(N(s["LandTitleFeePerPlot"]), "บาท/แปลง"), U(I(s["TotalPlots"]), unit),
                N(s["LandTitleFeeTotal"]), Pct(s["LandTitleFeeRatio"])),
            Row("ค่าวิชาชีพและควบคุมงานก่อสร้าง", U(N(s["ProfessionalFeePerMonth"]), "บาท/เดือน"),
                U(I(s["ProfessionalFeeMonths"]), "เดือน (ก่อสร้าง)"), N(s["ProfessionalFeeTotal"]), Pct(s["ProfessionalFeeRatio"])),
            Row("ค่าบริหารจัดการโครงการ", U(N(s["AdminCostPerMonth"]), "บาท/เดือน"),
                U(I(s["AdminCostMonths"]), "เดือน (ขาย)"), N(s["AdminCostTotal"]), Pct(s["AdminCostRatio"])),
            Row("ค่าใช้จ่ายการขายและโฆษณา", Pct(s["SellingAdvPercent"], ofRevenue),
                total: N(s["SellingAdvTotal"]), ratio: Pct(s["SellingAdvRatio"])),
        };
        pc.AddRange(OtherRows(others, 3));
        pc.Add(Row("เงินสำรองเผื่อเหลือเผื่อขาด", Pct(s["ProjectContingencyPercent"], "ของต้นทุนโครงการ"),
            total: N(s["ProjectContingencyAmount"]), ratio: Pct(s["ProjectContingencyRatio"])));
        pc.Add(Tot("รวมประมาณการต้นทุนโครงการ", N(s["TotalProjectCost"]), Pct(s["TotalProjectCostRatio"])));

        return
        [
            Band("รายละเอียดพื้นที่ดินที่ประเมิน", land),
            Band("ประมาณการรายได้โครงการ", rev),
            Band("ประมาณการระยะเวลาขาย",
                Row("ระยะเวลาขายโดยประมาณ", U(I(s["EstSalesPeriod"]), "หลัง/เดือน"),
                    U(I(s["TotalUnits"]), unit), U(I(s["EstimatedDurationMonths"]), "เดือน"))),
            Band("ประมาณการต้นทุนพัฒนาโครงการ", pdc),
            Band("ประมาณการระยะเวลาก่อสร้าง",
                Row("ระยะเวลาก่อสร้างโดยประมาณ", U(I(s["EstConstructionPeriod"]), "หลัง/เดือน"),
                    U(I(s["TotalUnitsForConstruction"]), unit), U(I(s["EstimatedConstructionDurationMonths"]), "เดือน"))),
            Band("ประมาณการต้นทุนโครงการ", pc),
            Band("ภาษีและค่าธรรมเนียมรัฐ",
                Row("ค่าธรรมเนียมการโอน", Pct(s["TransferFeePercent"], ofRevenue),
                    total: N(s["TransferFeeAmount"]), ratio: Pct(s["TransferFeeRatio"])),
                Row("ภาษีธุรกิจเฉพาะ", Pct(s["SpecificBizTaxPercent"], ofRevenue),
                    total: N(s["SpecificBizTaxAmount"]), ratio: Pct(s["SpecificBizTaxRatio"])),
                Tot("รวมภาษีและค่าธรรมเนียมรัฐ", N(s["TotalGovTax"]), Pct(s["TotalGovTaxRatio"]))),
            Band("ค่าความเสี่ยง",
                Row("ค่าความเสี่ยง", Pct(s["RiskPremiumPercent"], ofRevenue), total: N(s["RiskPremiumAmount"]))),
            Band("รวมต้นทุนและค่าใช้จ่ายในการพัฒนา",
                Tot("รวมต้นทุนและค่าใช้จ่ายในการพัฒนา", N(s["TotalDevCostsAndExpenses"]))),
            Band("มูลค่าทรัพย์สินสุดท้าย",
                Row("มูลค่าทรัพย์สินปัจจุบัน", "รายได้ − ต้นทุนรวม", total: N(s["CurrentPropertyValue"])),
                Row("อัตราคิดลด", Pct(s["DiscountRate"])),
                Row("ปัจจัยคิดลด", Factor(s["DiscountRateFactor"], s["EstimatedDurationMonths"])),
                Tot("มูลค่าทรัพย์สินสุดท้าย", N(s["FinalPropertyValue"])),
                Final(indicated, s["TotalAssetValueRounded"]),
                Tot("มูลค่าต่อตารางวา (ปัดเศษ ±100)", U(N(s["TotalAssetValuePerSqWa"]), "บาท/ตร.วา"))),
        ];
    }

    private static List<HypothesisLedgerBand> CondoBands(Summary s, decimal? indicated)
    {
        const string sqM = "ตร.ม.", unit = "หน่วย", ofIncome = "ของรายได้โครงการ", ofArea = "ของพื้นที่ก่อสร้างรวม";

        var land = new List<HypothesisLedgerRow>
        {
            Row("พื้นที่ตามโฉนด", s["AreaTitleDeed"] is null ? "-" : $"{N(s["AreaTitleDeed"])} ตร.วา ≈", U(N(s["AreaSqM"]), sqM)),
            Row("อัตราส่วนพื้นที่อาคารต่อที่ดิน (FAR)", s["FAR"] is { } far ? $"{far.ToString("#,##0.##", Inv)} : 1" : "-"),
            Row("พื้นที่ก่อสร้างตามผังเมือง", qty: U(N(s["ConstructionAreaCityPlan"]), sqM)),
            Row("พื้นที่อาคารรวม", U(N(s["TotalBuildingArea"]), sqM)),
            Row("พื้นที่ส่วนกลาง", Pct(s["CommonAreaPercent"], ofArea), U(N(s["CommonArea"]), sqM)),
            Row("พื้นที่ขายภายใน", Pct(s["IndoorSalesAreaPercent"], ofArea), U(N(s["IndoorSalesArea"]), sqM)),
        };
        AddRemark(land, s.Text("Remark"));

        return
        [
            Band("รายละเอียดพื้นที่ดิน", land),
            Band("ประมาณการรายได้โครงการ",
                Row("พื้นที่ขายโครงการ", qty: U(N(s["ProjectSalesArea"]), sqM)),
                Row("ราคาขายเฉลี่ย", U(N(s["AveragePricePerSqM"]), "บาท/ตร.ม.")),
                Row("ราคาขายรวมโครงการ", total: N(s["TotalProjectSellingPrice"])),
                Tot("รวมประมาณการรายได้โครงการ (GDV)", N(s["TotalRevenue"]))),
            Band("ประมาณการระยะเวลาขายโครงการ",
                Row("ระยะเวลาขายโดยประมาณ", U(I(s["EstSalesDurationMonths"]), "เดือน"))),
            Band("ประมาณการค่าก่อสร้างอาคารและต้นทุนพัฒนาโครงการ (Hard Cost)",
                Row("ค่าก่อสร้างอาคารชุด", U(N(s["CondoBuildingCostPerSqM"]), "บาท/ตร.ม."),
                    U(N(s["BuildingArea"]), sqM), N(s["CondoBuildingCostTotal"])),
                Row("ขนาดห้องเฉลี่ย", U(I(s["SetAvgRoomSizeUnits"]), unit), U(N(s["AvgIndoorSalesAreaPerUnit"]), "ตร.ม./หน่วย")),
                Row("เฟอร์นิเจอร์ ชุดครัว และเครื่องปรับอากาศ", U(N(s["FurniturePerUnit"]), "บาท/หน่วย"),
                    U(I(s["FurnitureQuantity"]), unit), N(s["FurnitureTotal"])),
                Row("สาธารณูปโภคภายนอกโครงการ", U(N(s["ExternalUtilities"]), "บาท"), total: N(s["ExternalUtilitiesTotal"])),
                Row("เงินสำรองเผื่อเหลือเผื่อขาด", Pct(s["HardCostContingencyPercent"], "ของค่าก่อสร้างและต้นทุนพัฒนาโครงการ"),
                    total: N(s["HardCostContingencyAmount"])),
                Tot("รวม Hard Cost", N(s["TotalHardCost"]))),
            Band("ประมาณการระยะเวลาก่อสร้างโครงการ",
                Row("ระยะเวลาก่อสร้างโดยประมาณ", U(I(s["EstConstructionPeriodMonths"]), "เดือน"))),
            Band("ประมาณการต้นทุนโครงการ (Soft Cost)",
                Row("ค่าวิชาชีพและควบคุมงานก่อสร้าง", U(N(s["ProfessionalFeePerMonth"]), "บาท/เดือน"),
                    U(I(s["ProfessionalFeeMonths"]), "เดือน (ก่อสร้าง)"), N(s["ProfessionalFeeTotal"])),
                Row("ค่าบริหารจัดการโครงการ", U(N(s["AdminCostPerMonth"]), "บาท/เดือน"),
                    U(I(s["AdminCostMonths"]), "เดือน (ขาย)"), N(s["AdminCostTotal"])),
                Row("ค่าใช้จ่ายการขายและโฆษณา", Pct(s["SellingAdvPercent"], ofIncome), total: N(s["SellingAdvTotal"])),
                Row("ค่าออกหนังสือกรรมสิทธิ์ห้องชุด", U(N(s["TitleDeedFee"]), "บาท"), total: N(s["TitleDeedFeeTotal"])),
                Row("ค่าจัดทำรายงานผลกระทบสิ่งแวดล้อม (EIA)", U(N(s["EIACost"]), "บาท"), total: N(s["EIACostTotal"])),
                Row("ค่าธรรมเนียมจดทะเบียนอาคารชุด", U(N(s["CondoRegistrationFee"]), "บาท"), total: N(s["CondoRegistrationFeeTotal"])),
                Row("ค่าใช้จ่ายอื่น", Pct(s["OtherExpensesPercent"], "ของต้นทุนโครงการ"), total: N(s["OtherExpensesTotal"])),
                Tot("รวม Soft Cost", N(s["TotalSoftCost"]))),
            Band("ภาษีและค่าธรรมเนียมรัฐ",
                Row("ค่าธรรมเนียมการโอน", Pct(s["TransferFeePercent"], ofIncome), total: N(s["TransferFeeTotal"])),
                Row("ภาษีธุรกิจเฉพาะ", Pct(s["SpecificBizTaxPercent"], ofIncome), total: N(s["SpecificBizTaxTotal"])),
                Tot("รวมภาษีและค่าธรรมเนียมรัฐ", N(s["TotalGovTax"]))),
            Band("ประมาณการความเสี่ยงและกำไรที่คาดหวัง",
                Row("ความเสี่ยงและกำไรที่คาดหวัง", Pct(s["RiskProfitPercent"], ofIncome), total: N(s["RiskProfitTotal"]))),
            Band("รวมต้นทุนและค่าใช้จ่ายในการพัฒนาโครงการ",
                Tot("รวมต้นทุนและค่าใช้จ่ายในการพัฒนาโครงการ", N(s["TotalDevCosts"]))),
            Band("มูลค่าคงเหลือของทรัพย์สินทั้งหมดเป็นเงินสด",
                Row("มูลค่าคงเหลือของทรัพย์สินทั้งหมด", "GDV − ต้นทุนรวม", total: N(s["TotalRemainingValue"])),
                Row("อัตราคิดลด", Pct(s["DiscountRate"])),
                Row("ปัจจัยคิดลด", Factor(s["DiscountRateFactor"], s["EstSalesDurationMonths"])),
                Tot("มูลค่าคงเหลือสุดท้าย", N(s["FinalRemainingValue"])),
                Final(indicated, s["TotalAssetValueRounded"]),
                Tot("มูลค่าต่อตารางเมตร (ปัดเศษ ±100)", U(N(s["TotalAssetValuePerSqM"]), "บาท/ตร.ม."))),
        ];
    }

    // ── Per-model analysis ──────────────────────────────────────────────────────────

    /// <summary>Groups the units by model, trimmed and case-insensitive like HypothesisCalculationService.AggregateModels.</summary>
    internal static IReadOnlyList<HypothesisModelRow> Models(IReadOnlyList<HypothesisUnitRow> units) =>
        units.GroupBy(u => ModelKey(u.Model), StringComparer.OrdinalIgnoreCase)
            .Select(g => Aggregate(g.Key, g.ToList()))
            .ToList();

    private static HypothesisModelRow Aggregate(string name, IReadOnlyList<HypothesisUnitRow> rows)
    {
        var land = rows.Sum(r => r.LandAreaSqWa ?? 0m);
        var usable = rows.Sum(r => r.UsableAreaSqM ?? 0m);
        var price = rows.Sum(r => r.SellingPrice ?? 0m);
        return new HypothesisModelRow
        {
            Name = name,
            Units = rows.Count,
            TotalLandAreaSqWa = land,
            AvgLandAreaSqWa = land / rows.Count,
            TotalUsableAreaSqM = usable,
            AvgUsableAreaSqM = usable / rows.Count,
            TotalSellingPrice = price,
            AvgSellingPrice = price / rows.Count,
        };
    }

    private static string ModelKey(string? name) => name?.Trim() is { Length: > 0 } t ? t : "Unknown";

    private static bool SameModel(string? a, string b) => string.Equals(ModelKey(a), b, StringComparison.OrdinalIgnoreCase);

    // ── Row / cell helpers ──────────────────────────────────────────────────────────

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static string N(decimal? v) => v is { } d ? d.ToString("#,##0.00", Inv) : "-";
    private static string I(decimal? v) => v is { } d ? d.ToString("#,##0", Inv) : "-";
    private static string U(string number, string unit) => number == "-" ? "-" : $"{number} {unit}";
    private static string Pct(decimal? v, string? note = null) =>
        v is null ? "-" : note is null ? $"{N(v)} %" : $"{N(v)} % {note}";

    /// <summary>ปัจจัยคิดลด, 4 decimals like the screen, with the sales period it discounts over.</summary>
    private static string Factor(decimal? factor, decimal? months) =>
        factor is null ? "-"
        : months is { } m ? $"{factor.Value.ToString("0.0000", Inv)} (ตามระยะขาย {(m / 12m).ToString("0.00", Inv)} ปี ({I(m)} เดือน))"
        : factor.Value.ToString("0.0000", Inv);

    /// <summary>มูลค่าตามวิธี: the appraiser's typed-over total wins, else the computed rounded total.</summary>
    internal static HypothesisLedgerRow Final(decimal? indicated, decimal? computed)
    {
        string? note = null;
        // "แก้เอง" only when the typed total really differs from the computed one (PricingGridCommon.Edited's rule): a stored
        // indicated value that equals the computed total is not an edit.
        if (indicated is { } iv && computed is { } cv && iv != cv)
        {
            var delta = iv - cv;
            note = $"แก้เอง · ต่างจากค่าที่คำนวณ {(delta >= 0 ? "+" : "−")}{N(Math.Abs(delta))}";
        }
        return new HypothesisLedgerRow
        {
            Label = "มูลค่าตามวิธี (มูลค่าทรัพย์สินรวม ปัดหลักหมื่น)", Kind = "final", Rate = note, Total = N(indicated ?? computed),
        };
    }

    private static IEnumerable<HypothesisLedgerRow> OtherRows(IReadOnlyList<OtherCostRow> others, int category) =>
        others.Where(o => o.Category == category)
            .Select(o => Row(string.IsNullOrWhiteSpace(o.Description) ? "-" : o.Description,
                total: N(o.Amount), ratio: Pct(o.CategoryRatio)));

    private static void AddRemark(List<HypothesisLedgerRow> rows, string? remark)
    {
        if (!string.IsNullOrWhiteSpace(remark))
            rows.Add(new HypothesisLedgerRow { Label = "หมายเหตุ", Kind = "remark", Rate = remark });
    }

    private static HypothesisLedgerRow Row(string label, string? rate = null, string? qty = null, string? total = null, string? ratio = null) =>
        new() { Label = label, Rate = UnsetRate(rate, total), Qty = qty, Total = total, Ratio = ratio };

    private static HypothesisLedgerRow Sub(string model, string? rate = null, string? qty = null, string? total = null, string? ratio = null) =>
        new() { Label = $"- {model}", Kind = "sub", Rate = UnsetRate(rate, total), Qty = qty, Total = total, Ratio = ratio };

    /// <summary>
    /// A zero rate beside a non-zero total is a rate nobody set (e.g. a typed-over model total with no
    /// building mapped), not a real 0 — print "-". A real 0 rate with a 0 total stays "0.00".
    /// </summary>
    private static string? UnsetRate(string? rate, string? total) =>
        rate is not null && rate.StartsWith("0.00 ", StringComparison.Ordinal)
        && total is not null and not "-" and not "0.00" ? "-" : rate;

    private static HypothesisLedgerRow Tot(string label, string total, string? ratio = null) =>
        new() { Label = label, Kind = "tot", Total = total, Ratio = ratio };

    private static HypothesisLedgerBand Band(string title, params IEnumerable<HypothesisLedgerRow> rows) =>
        new() { Title = title, Rows = rows.ToList() };

    private static decimal? ToDecimal(IDictionary<string, object> row, string column) =>
        row.TryGetValue(column, out var v) && v is not null and not DBNull ? Convert.ToDecimal(v, Inv) : null;

    /// <summary>Reads one owned summary block (LandBuildingSummary_ / CondominiumSummary_ columns) by property name.</summary>
    private sealed class Summary(IDictionary<string, object> row, string prefix)
    {
        public decimal? this[string property] => ToDecimal(row, prefix + property);

        public string? Text(string property) =>
            row.TryGetValue(prefix + property, out var v) && v is string s ? s : null;
    }

    // ── Private DTOs ──────────────────────────────────────────────────────────────

    internal sealed class OtherCostRow
    {
        public int Category { get; init; }
        public string? Description { get; init; }
        public decimal? Amount { get; init; }
        public decimal? CategoryRatio { get; init; }
    }

    internal sealed class MappingRow
    {
        public string? ModelName { get; init; }
        public Guid? AppraisalPropertyId { get; init; }
        public decimal? TotalCost { get; init; }
        public string? BuildingName { get; init; }
        public int? BuildingSequence { get; init; }
        public decimal? FinalCostValue { get; init; }
    }
}
