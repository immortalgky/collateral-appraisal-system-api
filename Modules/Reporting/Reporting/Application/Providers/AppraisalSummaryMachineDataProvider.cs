using Reporting.Application.Formatting;
using Reporting.Application.Models;
using Reporting.Application.Services;
using Shared.Configuration;

namespace Reporting.Application.Providers;

/// <summary>
/// Assembles an <see cref="AppraisalSummaryModel"/> for FSD §2.1.3.3
/// "ใบสรุปรายงานการประเมิน – เครื่องจักร".
///
/// Common queries (Q1–Q14 + ColTypeMap) are delegated to
/// <see cref="AppraisalSummaryCommonLoader"/> (itself batched in Phase C).
///
/// Phase C — this provider batches its own 2 machine-specific queries into one
/// QueryMultiple call (single round-trip):
///   RS01  QM1  appraisal.MachineryAppraisalSummaries — appraisal-level summary
///   RS02  QM2  appraisal.PropertyGroupItems + MachineryAppraisalDetails — per-group detail
///
/// Column notes:
///   - MachineryAppraisalSummaries uses schema=appraisal (DbContext default schema).
///   - MachineAge is a decimal? on the entity; the config does not call HasColumnName,
///     so the DB column is "MachineAge".
///   - YearOfManufacture and Quantity are int? on the entity; no HasColumnName override.
///   - The FSD §2.1.3.3 "ประเภทเครื่องจักร" field maps to InIndustrial (industry category).
///   - "สภาพความต้องการของตลาด" maps to MachineryAppraisalSummary.MarketDemand.
///   - Per FSD: พื้นที่/จำนวน and ราคาต่อหน่วย columns are BLANK for machine groups.
///     A subtotal row "รวมมูลค่าเครื่องจักร" is added after all group rows.
/// </summary>
public sealed class AppraisalSummaryMachineDataProvider(
    ISqlConnectionFactory connectionFactory,
    ISystemConfigurationReader configReader,
    ILogger<AppraisalSummaryMachineDataProvider> logger)
    : IReportDataProvider
{
    public string ReportTypeKey => "appraisal-summary-machine";

    public async Task<object> GetModelAsync(string entityId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(entityId, out var appraisalId))
            throw new NotFoundException("Appraisal", entityId);

        using var connection = connectionFactory.CreateNewConnection();

        var forceSaleRateDefault = await configReader.GetDecimalAsync("ForceSaleRateDefaultPct", 70m, cancellationToken);

        // ── Common data (Q1–Q14 + ColTypeMap) ───────────────────────────────────
        var common = await AppraisalSummaryCommonLoader.LoadAsync(connection, appraisalId, forceSaleRateDefault, cancellationToken);
        if (common is null)
            throw new NotFoundException("Appraisal", entityId);

        // ── Batch: 2 machine-specific result sets, single round-trip ─────────────
        const string batchSql = """
            -- RS01: QM1 — Machinery appraisal summary (appraisal-level)
            SELECT
                mas.InIndustrial,
                mas.MarketDemand,
                mas.Proprietor,
                mas.Owner,
                mas.MachineAddress,
                mas.Latitude,
                mas.Longitude,
                mas.Obligation,
                mas.SurveyedNumber
            FROM appraisal.MachineryAppraisalSummaries mas
            WHERE mas.AppraisalId = @AppraisalId;

            -- RS02: QM2 — Per-group machine detail rows
            SELECT
                pgi.PropertyGroupId,
                pgi.SequenceInGroup,
                mad.MachineName,
                mad.RegistrationNumber,
                mad.Brand,
                mad.Model,
                mad.Series,
                mad.EngineNo,
                mad.ChassisNo,
                mad.SerialNo,
                mad.Manufacturer,
                mad.YearOfManufacture,
                mad.MachineAge,
                mad.ReplacementValue,
                mad.ConditionValue,
                mad.MachineCondition,
                mad.OwnerName,
                mad.Location,
                mad.Quantity,
                mad.RegistrationStatus,
                mad.InstallationStatus,
                mad.IsPriceCertified,
                mad.ConditionUse
            FROM appraisal.PropertyGroupItems pgi
            JOIN appraisal.AppraisalProperties ap ON ap.Id = pgi.AppraisalPropertyId
            JOIN appraisal.MachineryAppraisalDetails mad ON mad.AppraisalPropertyId = ap.Id
            WHERE ap.AppraisalId = @AppraisalId
            ORDER BY pgi.PropertyGroupId, pgi.SequenceInGroup;
            """;

        var p = new DynamicParameters();
        p.Add("AppraisalId", appraisalId);

        MachSummaryRow? machSummary;
        List<GroupMachineDetailRow> groupMachineRows;

        using (var multi = await connection.QueryMultipleAsync(batchSql, p))
        {
            // RS01
            machSummary = await multi.ReadFirstOrDefaultAsync<MachSummaryRow>();

            // RS02
            groupMachineRows = (await multi.ReadAsync<GroupMachineDetailRow>()).ToList();
        }

        // GPS from the machinery summary table
        var gps = ThaiAddressFormatter.FormatGps(machSummary?.Latitude, machSummary?.Longitude);

        var collateralAddress = string.IsNullOrWhiteSpace(machSummary?.MachineAddress)
            ? null
            : machSummary.MachineAddress.Trim();

        var machineByGroup = groupMachineRows
            .GroupBy(r => r.PropertyGroupId)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.SequenceInGroup).ToList());

        // ── Build per-group summary rows (machine / vehicle / vessel groups only) ──
        var machineFamily = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MAC", "VEH", "VES" };
        var machineGroupList = common.GroupRows
            .Where(g => g.PropertyType != null && machineFamily.Contains(g.PropertyType))
            .ToList();

        // SurveyedNumber is appraisal-level ("global for all machines"). It is only a valid
        // per-group count when there is a single machine group; with multiple groups it would
        // print the appraisal-wide total on every group, so we fall back to per-group counts.
        var singleMachineGroup = machineGroupList.Count == 1;

        var summaryGroups = machineGroupList
            .Select(g =>
        {
            machineByGroup.TryGetValue(g.GroupId, out var machRows);
            machRows ??= [];

            // Per-group count: detail rows if present, else the group's property count.
            // For a single machine group, prefer the appraiser-entered surveyed count — but only
            // when it is a positive value; a stored 0 (default/unset) means "not entered" and must
            // fall back to the per-group count rather than hiding the header line.
            int perGroupCount = machRows.Count > 0 ? machRows.Count : g.PropertyCount;
            int machineCount = singleMachineGroup && machSummary?.SurveyedNumber is int surveyed && surveyed > 0
                ? surveyed
                : perGroupCount;
            // Header line(s) above the numbered item list. A group can mix registration states
            // (the sample reports show a registered batch and an unregistered one in the same
            // appraisal), so emit one line per state that is actually present rather than the
            // blanket "จดทะเบียนกรรมสิทธิ์" claim this used to print for every machine.
            var detailLines = BuildRegistrationHeaderLines(machRows);

            // Fallback for the pre-migration case where no detail rows were loaded: keep the old
            // single-line header driven by the count so the row never renders empty.
            var collateralDetails = detailLines.Count == 0 && machineCount > 0
                ? $"จดทะเบียนกรรมสิทธิ์เครื่องจักร จำนวน {machineCount} รายการ"
                : null;

            // One entry per machine — rendered as a numbered list (1., 2., …).
            var detailItems = new List<string>();
            foreach (var m in machRows)
            {
                var parts = new List<string>();

                if (!string.IsNullOrWhiteSpace(m.MachineName))
                    parts.Add(m.MachineName);
                if (!string.IsNullOrWhiteSpace(m.Brand))
                    parts.Add($"ยี่ห้อ {m.Brand}");
                if (!string.IsNullOrWhiteSpace(m.Model))
                    parts.Add($"รุ่น {m.Model}");
                if (!string.IsNullOrWhiteSpace(m.RegistrationNumber))
                    parts.Add($"ทะเบียนเลขที่ {m.RegistrationNumber}");
                if (!string.IsNullOrWhiteSpace(m.SerialNo))
                    parts.Add($"หมายเลขเครื่อง {m.SerialNo}");
                if (m.YearOfManufacture.HasValue)
                    parts.Add($"ปีที่ผลิต {m.YearOfManufacture}");
                if (!string.IsNullOrWhiteSpace(m.MachineCondition))
                    parts.Add($"สภาพ{m.MachineCondition}");

                // The appraiser records "surveyed but missing" on ConditionUse; the reports call it out
                // per item rather than dropping the machine from the list.
                if (m.ConditionUse == NotFoundCondition)
                    parts.Add("(สำรวจไม่พบ)");

                // IsPriceCertified is the ไม่ประเมินมูลค่า flag: certifying a price and appraising a
                // value are one decision here, not two, so there is only ever one phrase to print.
                if (!m.IsPriceCertified)
                    parts.Add("(ไม่ประเมินมูลค่า)");

                if (parts.Count > 0)
                    detailItems.Add(string.Join(" ", parts));
            }

            return new SummaryGroupRow
            {
                GroupNumber = g.GroupNumber,
                GroupName = g.GroupName,
                PropertyType = "เครื่องจักร",
                CollateralDetails = collateralDetails,
                CollateralDetailLines = detailLines.Count > 0 ? detailLines : null,
                DetailItems = detailItems,
                AreaOrUnit = null,
                PricePerAreaOrUnit = null,
                AppraisalValue = g.GroupAppraisalValue,
                Condition = null,
                Remark = null
            };
        }).ToList();

        // วิธีการประเมิน — scoped to the methods of the machine groups actually shown.
        var methodFlags = AppraisalSummaryCommonLoader.FlagsForGroups(
            common.GroupMethodTypes,
            machineGroupList.Select(g => g.GroupId));

        // ── Build model ──────────────────────────────────────────────────────────
        var model = new AppraisalSummaryModel
        {
            AppraisalBookNumber = common.AppraisalNumber,
            AppraisalDate = common.AppraisalDate,
            CustomerName = common.CustomerName,
            AoName = common.AoName,
            AppraisalPurpose = common.AppraisalPurpose,
            // Machine form: property type is fixed (header + appraiser opinion).
            PropertyType = "เครื่องจักร",
            SummaryPropertyType = "เครื่องจักร",
            // ที่ตั้งทรัพย์สิน from the Request detail (same as land-building); fall back to the machine's own address.
            CollateralAddress = common.CollateralAddress ?? collateralAddress,
            AdministrativeDistrict = common.AdministrativeDistrict,
            LandOffice = null,
            OldAppraisalValue = common.PrevAppraisedValue,
            HasPrevAppraisal = common.HasPrevAppraisal,
            IsReAppraisal = string.Equals(common.AppraisalType, "ReAppraisal", StringComparison.OrdinalIgnoreCase),
            Appraiser = common.Appraiser,
            LoanValue = common.LoanValue,
            Groups = summaryGroups,
            TotalAppraisalValue = summaryGroups.Count > 0 ? summaryGroups.Sum(g => g.AppraisalValue ?? 0m) : common.TotalAppraisalValue,
            BuildingCoverageAmount = common.BuildingCoverageAmount,
            ForcedSaleValue = common.ForcedSaleValue,
            Condition = common.Condition,
            Remark = common.Remark,
            // กรรมสิทธิ์เครื่องจักร = registered title holder. Prefer the appraisal-level
            // Proprietor (ผู้ถือกรรมสิทธิ์) captured on the machinery summary; fall back to the
            // possessor (Owner) then a per-machine owner name.
            LandOwner = FirstNonBlank(
                machSummary?.Proprietor,
                machSummary?.Owner,
                groupMachineRows.Select(r => r.OwnerName).FirstOrDefault(o => !string.IsNullOrWhiteSpace(o))),
            EntryExitRights = null,
            BuildingOwner = null,
            LandCondition = null,
            Obligation = string.IsNullOrWhiteSpace(machSummary?.Obligation)
                ? AppraisalSummaryModel.NoObligationText
                : machSummary!.Obligation,
            CityPlan = null,
            Gps = gps,
            GovernmentAssessedValue = null,
            Utilization = null,
            MachineType = machSummary?.InIndustrial,
            MarketDemandConditions = machSummary?.MarketDemand,
            IsWqs = methodFlags.IsWqs,
            IsSaleGrid = methodFlags.IsSaleGrid,
            IsCost = methodFlags.IsCost,
            IsIncome = methodFlags.IsIncome,
            IsHypothesis = methodFlags.IsHypothesis,
            IsLeasehold = methodFlags.IsLeasehold,
            IsProfitRent = methodFlags.IsProfitRent,
            AppraiserComment = common.AppraiserComment,
            AppraisalStaffName = common.StaffName,
            AppraisalStaffPosition = common.StaffPosition,
            AppraisalCheckerName = common.CheckerName,
            AppraisalCheckerPosition = common.CheckerPosition,
            AppraisalVerifyName = common.VerifyName,
            AppraisalVerifyPosition = common.VerifyPosition,
            MeetingNumber = common.Review?.MeetingNo,
            MeetingDate = common.Review?.MeetingDate,
            ApprovalDate = common.ApprovalDate,
            IsCompleted = common.IsCompleted,
            ShowMeeting = common.ShowMeeting,
            ApproverDecisionApproved = common.ApproverDecisionApproved,
            Approvers = common.Approvers,
            ApproverSummaryComment = common.CommitteeOpinion
        };

        logger.LogDebug(
            "AppraisalSummaryMachine model assembled for appraisal {AppraisalId}: " +
            "{GroupCount} groups, {ApproverCount} approvers, showMeeting={ShowMeeting}",
            appraisalId, summaryGroups.Count, common.Approvers.Count, common.ShowMeeting);

        return model;
    }

    /// <summary>Returns the first argument that is not null/whitespace, or null.</summary>
    private static string? FirstNonBlank(params string?[] values)
        => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>MachineStatus parameter code for "อยู่ระหว่างการจัดซื้อ" (valued from a quotation).</summary>
    private const string UnderProcurementStatus = "2";

    /// <summary>
    /// One header line per registration state present in the group, in report order. A group with
    /// a single state therefore still gets exactly one line, matching the previous output shape.
    /// </summary>
    /// <summary>ConditionUse parameter code for "not found" on the survey.</summary>
    private const string NotFoundCondition = "03";

    private static List<string> BuildRegistrationHeaderLines(List<GroupMachineDetailRow> rows)
    {
        // Counts are ROWS, matching the numbered list printed underneath, so a reader can check the
        // header against the items. (Quantity is deliberately not summed here: the cost-approach
        // section reports that separately as สำรวจพบ, and mixing the two units in one cell would
        // make the header disagree with the list.)
        var registered = rows.Count(r => r.RegistrationStatus);
        var unregisteredInstalled = rows
            .Count(r => !r.RegistrationStatus && r.InstallationStatus != UnderProcurementStatus);
        var underProcurement = rows
            .Count(r => !r.RegistrationStatus && r.InstallationStatus == UnderProcurementStatus);

        var lines = new List<string>();
        if (registered > 0)
        {
            lines.Add($"เครื่องจักรและอุปกรณ์ที่ได้จดทะเบียนกรรมสิทธิ์ จำนวน {registered} เครื่อง");

            // One ร.2/1 booklet usually covers several machines, and the appraiser records that by
            // typing the same RegistrationNumber on each of them — so the numbers are grouped rather
            // than listed per machine. A registered machine with no number recorded contributes to
            // the header count above but gets no line of its own.
            var byBooklet = rows
                .Where(r => r.RegistrationStatus && !string.IsNullOrWhiteSpace(r.RegistrationNumber))
                .GroupBy(r => r.RegistrationNumber!.Trim(), StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal);

            foreach (var booklet in byBooklet)
                lines.Add($"ตามสำเนาทะเบียนเครื่องจักร (ร.2/1) เลขที่ {booklet.Key} จำนวน {booklet.Count()} เครื่อง");
        }
        if (unregisteredInstalled > 0)
            lines.Add($"เครื่องจักรและอุปกรณ์ที่ยังไม่ได้รับการจดทะเบียน จำนวน {unregisteredInstalled} เครื่อง");
        if (underProcurement > 0)
            lines.Add($"เครื่องจักรและอุปกรณ์ที่ไม่จดทะเบียนอยู่ระหว่างการติดตั้ง จำนวน {underProcurement} เครื่อง");

        return lines;
    }

    // ── Private flat DTOs for Dapper mapping ─────────────────────────────────────

    private sealed class MachSummaryRow
    {
        public string? InIndustrial { get; init; }
        public string? MarketDemand { get; init; }
        public string? Proprietor { get; init; }
        public string? Owner { get; init; }
        public string? MachineAddress { get; init; }
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
        public string? Obligation { get; init; }
        public int? SurveyedNumber { get; init; }
    }

    private sealed class GroupMachineDetailRow
    {
        public Guid PropertyGroupId { get; init; }
        public int SequenceInGroup { get; init; }
        public string? MachineName { get; init; }
        public string? RegistrationNumber { get; init; }
        public string? Brand { get; init; }
        public string? Model { get; init; }
        public string? Series { get; init; }
        public string? EngineNo { get; init; }
        public string? ChassisNo { get; init; }
        public string? SerialNo { get; init; }
        public string? Manufacturer { get; init; }
        public int? YearOfManufacture { get; init; }
        public decimal? MachineAge { get; init; }
        public decimal? ReplacementValue { get; init; }
        public decimal? ConditionValue { get; init; }
        public string? MachineCondition { get; init; }
        public string? OwnerName { get; init; }
        public string? Location { get; init; }
        public int? Quantity { get; init; }
        public bool RegistrationStatus { get; init; }
        public string? InstallationStatus { get; init; }
        public bool IsPriceCertified { get; init; }
        public string? ConditionUse { get; init; }
    }
}
