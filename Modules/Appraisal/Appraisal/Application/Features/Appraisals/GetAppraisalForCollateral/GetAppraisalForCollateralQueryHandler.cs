using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalForCollateral;

/// <summary>
/// Handles the cross-module MediatR query from the Collateral module.
/// Returns all appraisal data needed for CollateralMaster upsert.
/// Uses EF Core AsNoTracking — read-only path, no aggregate behavior.
/// </summary>
public class GetAppraisalForCollateralQueryHandler(
    AppraisalDbContext dbContext,
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetAppraisalForCollateralQuery, AppraisalForCollateralResult?>
{
    // Condo collateral title type is always DEED — the underlying land title (BuiltOnTitleNumber)
    // is, by domain rule, a chanote/deed.
    private const string CondoDefaultTitleType = "DEED";

    public async Task<AppraisalForCollateralResult?> Handle(
        GetAppraisalForCollateralQuery query,
        CancellationToken cancellationToken)
    {
        var appraisal = await dbContext.Appraisals
            .AsNoTracking()
            .AsSplitQuery()
            .Include(a => a.Properties)
                .ThenInclude(p => p.LandDetail)
                    .ThenInclude(l => l!.Titles)
            .Include(a => a.Properties)
                .ThenInclude(p => p.CondoDetail)
            .Include(a => a.Properties)
                .ThenInclude(p => p.LeaseAgreementDetail)
            .Include(a => a.Properties)
                .ThenInclude(p => p.MachineryDetail)
            .Include(a => a.Properties)
                .ThenInclude(p => p.BuildingDetail)
            .Include(a => a.Properties)
                .ThenInclude(p => p.ConstructionInspection)
                    .ThenInclude(ci => ci!.WorkDetails)
            .Include(a => a.Assignments)
            // Groups + Items are owned by Appraisal — must navigate through the root aggregate.
            // They are loaded here so BuildAppraisedValueLookupAsync can work in-memory.
            .Include(a => a.Groups)
            .FirstOrDefaultAsync(a => a.Id == query.AppraisalId, cancellationToken);

        if (appraisal is null)
            return null;

        // Derive appraiser and company from the latest non-rejected/cancelled assignment.
        // We don't require Completed status — the appraisal aggregate can complete via
        // committee approval before the assignment row is independently marked Completed,
        // and we still want to record who did the work.
        var latestAssignment = appraisal.Assignments
            .Where(a => a.AssignmentStatus.Code != "Rejected" && a.AssignmentStatus.Code != "Cancelled")
            .OrderByDescending(a => a.AssignedAt)
            .ThenByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .FirstOrDefault();

        // AppraiserUserId fallback chain: prefer the directly-assigned user; fall back to the
        // internal appraiser (followup) or the external appraiser (the human at the company).
        var appraiserUserId = latestAssignment?.AssigneeUserId
                              ?? latestAssignment?.InternalAppraiserId
                              ?? latestAssignment?.ExternalAppraiserId;
        var companyId = latestAssignment?.AssigneeCompanyId;

        // Company name is denormalized on `ExternalAppraiserName` when set; otherwise null.
        // Cross-module lookup against auth.Companies is a v1.x follow-up.
        var companyName = latestAssignment?.ExternalAppraiserName;

        // AppraisalDate represents the *visit* (appointment) date, not the system completion.
        // Source: latest non-Cancelled appointment for the assignment, regardless of status.
        // Fallbacks: appraisal.CompletedAt → latestAssignment.CompletedAt (final fallback
        // ApplicationNow handled at the upsert layer for safety).
        DateTime? appointmentDate = null;
        if (latestAssignment is not null)
        {
            appointmentDate = await dbContext.Appointments
                .AsNoTracking()
                .Where(ap => ap.AssignmentId == latestAssignment.Id
                    && ap.Status != "Cancelled")
                .OrderByDescending(ap => ap.AppointmentDateTime)
                .Select(ap => (DateTime?)ap.AppointmentDateTime)
                .FirstOrDefaultAsync(cancellationToken);
        }
        var completedAt = appointmentDate
                          ?? appraisal.CompletedAt
                          ?? latestAssignment?.CompletedAt;

        // Resolve per-property appraised value via PropertyGroup → PricingAnalysis.
        // PropertyGroup and PropertyGroupItem are owned entities; they were loaded above
        // via Include(a => a.Groups). We join their in-memory data to PricingAnalyses
        // (which IS a regular DbSet) to get FinalAppraisedValue.
        var appraisedValueLookup = await BuildAppraisedValueLookupAsync(
            dbContext, appraisal, cancellationToken);

        // Resolve per-group pricing values (UnitPrice / BuildingCost / AppraisalValue)
        // from the selected cost-approach method's PricingFinalValue.
        var pricingLookup = await BuildPricingLookupAsync(
            dbContext, appraisal, cancellationToken);

        var groupMembershipLookup = BuildGroupMembershipLookup(appraisal);

        var properties = appraisal.Properties
            .Select(p => MapProperty(p, appraisedValueLookup, pricingLookup, groupMembershipLookup))
            .ToList();

        // Appraisal-level total: ValuationAnalyses is 1:1 with Appraisal (unique index on AppraisalId)
        // and AppraisalFinalValuesChangedEventHandler maintains its AppraisedValue as the sum across
        // all PropertyGroups. Stamped onto each engagement so the engagement reflects the true
        // appraisal total (e.g. land + buildings combined for Land appraisals).
        var appraisalTotal = await dbContext.ValuationAnalyses
            .AsNoTracking()
            .Where(v => v.AppraisalId == appraisal.Id)
            .Select(v => (decimal?)v.AppraisedValue)
            .FirstOrDefaultAsync(cancellationToken);

        // Stamp onto the engagement "the inspection fee the NEXT Progressive appraisal should charge",
        // so the fee chains uniformly: original → 1st inspection → 2nd inspection → …
        //   - Original (non-Progressive): the quoted future-inspection fee
        //     (AppraisalFee.ConstructionInspectionFeeAmount, set via UpdateConstructionInspectionFeeCommand).
        //   - Progressive (this appraisal IS an inspection): it charges its fee as the appraisal-fee
        //     line (FeeCode "01") and leaves ConstructionInspectionFeeAmount null — so carry forward
        //     that line's amount (ex-VAT) for the next inspection. We sum only FeeCode "01" so ad-hoc
        //     surcharges (travel "02"/urgent "03") on this visit don't propagate to the next.
        decimal? constructionInspectionFee = null;
        if (latestAssignment is not null)
        {
            constructionInspectionFee = appraisal.AppraisalType == AppraisalTypes.Progressive
                ? await dbContext.AppraisalFees
                    .AsNoTracking()
                    .Where(f => f.AssignmentId == latestAssignment.Id)
                    .Select(f => (decimal?)f.Items
                        .Where(i => i.FeeCode == "01")
                        .Sum(i => i.FeeAmount))
                    .FirstOrDefaultAsync(cancellationToken)
                : await dbContext.AppraisalFees
                    .AsNoTracking()
                    .Where(f => f.AssignmentId == latestAssignment.Id)
                    .Select(f => f.ConstructionInspectionFeeAmount)
                    .FirstOrDefaultAsync(cancellationToken);
        }

        // RequestNumber lives on request.Requests (cross-module), not on the Appraisal aggregate.
        // Use a lightweight Dapper sub-query — same pattern as vw_AppraisalList.sql line 46.
        var requestNumber = await connectionFactory.QueryFirstOrDefaultAsync<string?>(
            "SELECT RequestNumber FROM request.Requests WHERE Id = @RequestId",
            new { RequestId = appraisal.RequestId });

        return new AppraisalForCollateralResult(
            AppraisalId: appraisal.Id,
            AppraisalNumber: appraisal.AppraisalNumber,
            AppraisalType: appraisal.AppraisalType,
            CompletedAt: completedAt,
            RequestId: appraisal.RequestId,
            RequestNumber: requestNumber,
            AppraiserUserId: appraiserUserId,
            CompanyId: companyId,
            CompanyName: companyName,
            AppraisedValue: appraisalTotal,
            ConstructionInspectionFeeAmount: constructionInspectionFee,
            Properties: properties
        );
    }

    /// <summary>
    /// Returns a dictionary of AppraisalPropertyId → FinalAppraisedValue.
    ///
    /// PropertyGroup and PropertyGroupItem are both owned entities of Appraisal and cannot
    /// be queried through their own DbSets (throws at runtime per the project's owned-entity rule).
    /// Instead we:
    ///   1. Read the already-loaded Groups.Items from the in-memory appraisal to build a
    ///      map of PropertyGroupId → [AppraisalPropertyId list].
    ///   2. Query PricingAnalyses (a regular DbSet) for those group ids.
    ///   3. Join in-memory to produce the final AppraisalPropertyId → FinalAppraisedValue map.
    /// </summary>
    private static async Task<Dictionary<Guid, decimal?>> BuildAppraisedValueLookupAsync(
        AppraisalDbContext dbContext,
        Domain.Appraisals.Appraisal appraisal,
        CancellationToken ct)
    {
        // Step 1: build in-memory map of PropertyGroupId → list of AppraisalPropertyIds
        var groupToProperties = appraisal.Groups
            .SelectMany(g => g.Items.Select(i => new { GroupId = g.Id, i.AppraisalPropertyId }))
            .ToList();

        if (groupToProperties.Count == 0)
            return new Dictionary<Guid, decimal?>();

        var groupIds = groupToProperties.Select(x => x.GroupId).Distinct().ToList();

        // Step 2: query PricingAnalyses (regular DbSet) for these group ids
        var pricingAnalyses = await dbContext.PricingAnalyses
            .AsNoTracking()
            .Where(pa => pa.SubjectType == PricingAnalysisSubjectType.PropertyGroup
                         && pa.AnchorId != null
                         && groupIds.Contains(pa.AnchorId!.Value))
            .Select(pa => new { AnchorId = pa.AnchorId, pa.FinalAppraisedValue })
            .ToListAsync(ct);

        // Step 3: join in-memory — AnchorId (= PropertyGroupId) → FinalAppraisedValue
        var groupValues = pricingAnalyses
            .GroupBy(pa => pa.AnchorId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(x => x.FinalAppraisedValue).FirstOrDefault(v => v.HasValue));

        // Step 4: map AppraisalPropertyId → FinalAppraisedValue via groupToProperties
        var result = new Dictionary<Guid, decimal?>();
        foreach (var gtp in groupToProperties)
        {
            if (result.ContainsKey(gtp.AppraisalPropertyId))
                continue; // already resolved for this property
            groupValues.TryGetValue(gtp.GroupId, out var val);
            result[gtp.AppraisalPropertyId] = val;
        }

        return result;
    }

    /// <summary>
    /// Builds a lookup of AppraisalPropertyId → PricingInfoForCollateral.
    ///
    /// For each PropertyGroup we query PricingAnalysis → Approaches → Methods → FinalValue and
    /// extract the values from the selected approach's selected method. "Cost approach" is
    /// identified by PricingAnalysisApproach.ApproachType == "Cost" and IsSelected == true.
    ///
    /// Field mapping (per user spec):
    ///   UnitPrice     ← method.FinalValue.FinalValueAdjusted   (the adjusted per-unit rate)
    ///   BuildingCost  ← method.FinalValue.BuildingCost (non-null when HasBuildingCost)
    ///   AppraisalValue← method.FinalValue.AppraisalPrice
    ///                   ?? method.FinalValue.FinalValueAdjusted
    ///                   ?? method.FinalValue.FinalValueRounded
    ///
    /// For non-cost approaches we still capture AppraisalValue (but UnitPrice + BuildingCost = null).
    /// Every property in the same group gets the same PricingInfoForCollateral instance.
    /// </summary>
    private static async Task<Dictionary<Guid, PricingInfoForCollateral>> BuildPricingLookupAsync(
        AppraisalDbContext dbContext,
        Domain.Appraisals.Appraisal appraisal,
        CancellationToken ct)
    {
        // Step 1: build in-memory map of AppraisalPropertyId → GroupId
        var propertyToGroup = appraisal.Groups
            .SelectMany(g => g.Items.Select(i => new { GroupId = g.Id, i.AppraisalPropertyId }))
            .ToList();

        if (propertyToGroup.Count == 0)
            return new Dictionary<Guid, PricingInfoForCollateral>();

        var groupIds = propertyToGroup.Select(x => x.GroupId).Distinct().ToList();

        // Step 2: load PricingAnalysis with Approaches → Methods → FinalValue for these groups.
        // We project to anonymous types to avoid loading unnecessary child collections.
        var analyses = await dbContext.PricingAnalyses
            .AsNoTracking()
            .Where(pa => pa.SubjectType == PricingAnalysisSubjectType.PropertyGroup
                         && pa.AnchorId != null
                         && groupIds.Contains(pa.AnchorId!.Value))
            .Select(pa => new
            {
                AnchorId = pa.AnchorId,
                Approaches = pa.Approaches.Select(a => new
                {
                    a.ApproachType,
                    a.IsSelected,
                    Methods = a.Methods.Select(m => new
                    {
                        m.IsSelected,
                        FinalValue = m.FinalValue == null ? null : new
                        {
                            m.FinalValue.HasBuildingCost,
                            m.FinalValue.BuildingCost,
                            m.FinalValue.AppraisalPrice,
                            m.FinalValue.FinalValueAdjusted,
                            m.FinalValue.FinalValueRounded
                        }
                    }).ToList()
                }).ToList()
            })
            .ToListAsync(ct);

        // Step 3: for each group, find the pricing info from the selected approach/method
        var groupPricing = new Dictionary<Guid, PricingInfoForCollateral>();

        foreach (var pa in analyses)
        {
            if (pa.AnchorId is null) continue;

            // Prefer the selected cost approach; fall back to any selected approach; then first
            var selectedApproach = pa.Approaches.FirstOrDefault(a => a.IsSelected && a.ApproachType == "Cost")
                ?? pa.Approaches.FirstOrDefault(a => a.IsSelected)
                ?? pa.Approaches.FirstOrDefault();

            if (selectedApproach is null) continue;

            var selectedMethod = selectedApproach.Methods.FirstOrDefault(m => m.IsSelected)
                ?? selectedApproach.Methods.FirstOrDefault();

            if (selectedMethod is null) continue;

            bool isCostApproach = selectedApproach.ApproachType == "Cost"
                && selectedMethod.FinalValue?.HasBuildingCost == true;

            decimal? unitPrice    = isCostApproach ? selectedMethod.FinalValue?.FinalValueAdjusted : null;
            decimal? buildingCost = isCostApproach ? selectedMethod.FinalValue?.BuildingCost : null;

            // AppraisalValue is always the user-confirmed final total regardless of approach type.
            var fv = selectedMethod.FinalValue;
            decimal? appraisalValue = fv?.AppraisalPrice
                                      ?? fv?.FinalValueAdjusted
                                      ?? fv?.FinalValueRounded;

            groupPricing[pa.AnchorId.Value] = new PricingInfoForCollateral(
                IsCostApproach: isCostApproach,
                UnitPrice: unitPrice,
                BuildingCost: buildingCost,
                AppraisalValue: appraisalValue
            );
        }

        // Step 4: map AppraisalPropertyId → PricingInfoForCollateral via propertyToGroup
        var result = new Dictionary<Guid, PricingInfoForCollateral>();
        foreach (var pg in propertyToGroup)
        {
            if (result.ContainsKey(pg.AppraisalPropertyId)) continue;
            groupPricing.TryGetValue(pg.GroupId, out var info);
            if (info is not null)
                result[pg.AppraisalPropertyId] = info;
        }

        return result;
    }

    /// <summary>
    /// Builds a lookup of AppraisalPropertyId → (PropertyGroupId, GroupNumber, SequenceInGroup)
    /// from the already-loaded appraisal.Groups.
    /// Used by MapProperty to surface group identity on each property DTO.
    /// </summary>
    private static Dictionary<Guid, (Guid GroupId, int GroupNumber, int SequenceInGroup)> BuildGroupMembershipLookup(
        Domain.Appraisals.Appraisal appraisal)
    {
        var lookup = new Dictionary<Guid, (Guid, int, int)>();
        foreach (var group in appraisal.Groups)
        {
            foreach (var item in group.Items)
            {
                // Each property belongs to at most one group (unique constraint on PropertyGroupItem)
                lookup[item.AppraisalPropertyId] = (group.Id, group.GroupNumber, item.SequenceInGroup);
            }
        }
        return lookup;
    }

    private static AppraisalPropertyForCollateral MapProperty(
        AppraisalProperty p,
        Dictionary<Guid, decimal?> appraisedValueLookup,
        Dictionary<Guid, PricingInfoForCollateral> pricingLookup,
        Dictionary<Guid, (Guid GroupId, int GroupNumber, int SequenceInGroup)> groupMembershipLookup)
    {
        appraisedValueLookup.TryGetValue(p.Id, out var appraisedValue);
        pricingLookup.TryGetValue(p.Id, out var pricingInfo);

        // Group membership — null when property is not in any group
        groupMembershipLookup.TryGetValue(p.Id, out var groupInfo);
        Guid? propertyGroupId = groupInfo == default ? null : groupInfo.GroupId;
        int? groupNumber = groupInfo == default ? null : groupInfo.GroupNumber;
        int? sequenceInGroup = groupInfo == default ? null : groupInfo.SequenceInGroup;

        var landIdentity = p.LandDetail is not null
            ? new LandIdentityForCollateral(
                Province: p.LandDetail.Address?.Province,
                District: p.LandDetail.Address?.District,
                SubDistrict: p.LandDetail.Address?.SubDistrict,
                LandOffice: p.LandDetail.Address?.LandOffice,
                Titles: p.LandDetail.Titles
                    .Select(t => new LandTitleForCollateral(t.Id, t.TitleNumber, t.TitleType))
                    .ToList(),
                // Phase C: last-known populate fields from LandAppraisalDetail
                OwnerName: p.LandDetail.OwnerName,
                Street: p.LandDetail.Street,
                Village: p.LandDetail.Village,
                Latitude: p.LandDetail.Coordinates?.Latitude,
                Longitude: p.LandDetail.Coordinates?.Longitude,
                LandShapeType: p.LandDetail.LandShapeType,
                LandZoneType: p.LandDetail.LandZoneType?.FirstOrDefault(),
                UrbanPlanningType: p.LandDetail.UrbanPlanningType,
                AccessRoadWidth: p.LandDetail.AccessRoadWidth,
                RoadFrontage: p.LandDetail.RoadFrontage,
                LandArea: p.LandDetail.TotalLandAreaInSqWa == 0m ? null : p.LandDetail.TotalLandAreaInSqWa
            )
            : null;

        var condoIdentity = p.CondoDetail is not null
            ? new CondoIdentityForCollateral(
                CondoRegistrationNumber: p.CondoDetail.CondoRegistrationNumber,
                BuildingNumber: p.CondoDetail.BuildingNumber,
                FloorNumber: p.CondoDetail.FloorNumber,
                RoomNumber: p.CondoDetail.RoomNumber,
                Province: p.CondoDetail.Address?.Province,
                LandOffice: p.CondoDetail.Address?.LandOffice,
                TitleNumber: p.CondoDetail.BuiltOnTitleNumber,
                TitleType: CondoDefaultTitleType,
                // Phase C: last-known populate fields from CondoAppraisalDetail
                OwnerName: p.CondoDetail.OwnerName,
                CondoName: p.CondoDetail.CondoName,
                UsableArea: p.CondoDetail.UsableArea,
                LocationType: p.CondoDetail.LocationType,
                BuildingAge: p.CondoDetail.BuildingAge,
                ConstructionYear: p.CondoDetail.ConstructionYear,
                ModelName: p.CondoDetail.ModelName,
                // Phase 1: GPS coordinates for geo filter support
                Latitude: p.CondoDetail.Coordinates?.Latitude,
                Longitude: p.CondoDetail.Coordinates?.Longitude
            )
            : null;

        var leaseholdIdentity = p.LeaseAgreementDetail is not null
            ? new LeaseholdIdentityForCollateral(
                ContractNo: p.LeaseAgreementDetail.ContractNo,
                LessorName: p.LeaseAgreementDetail.LessorName,
                LesseeName: p.LeaseAgreementDetail.LesseeName,
                LeaseStartDate: p.LeaseAgreementDetail.LeaseStartDate,
                LeaseEndDate: p.LeaseAgreementDetail.LeaseEndDate
            )
            : null;

        var machineryIdentity = p.MachineryDetail is not null
            ? new MachineryIdentityForCollateral(
                RegistrationNumber: p.MachineryDetail.RegistrationNumber,
                SerialNo: p.MachineryDetail.SerialNo,
                Brand: p.MachineryDetail.Brand,
                Model: p.MachineryDetail.Model,
                Manufacturer: p.MachineryDetail.Manufacturer,
                Location: p.MachineryDetail.Location,
                OwnerName: p.MachineryDetail.OwnerName
            )
            : null;

        var buildingIdentity = p.BuildingDetail is not null
            ? new BuildingIdentityForCollateral(
                BuiltOnTitleNumber: p.BuildingDetail.BuiltOnTitleNumber,
                BuildingTypeCode: p.BuildingDetail.BuildingType,
                BuildingArea: p.BuildingDetail.TotalBuildingArea
            )
            : null;

        var constructionInspection = p.ConstructionInspection is not null
            ? MapConstructionInspection(p.ConstructionInspection)
            : null;

        return new AppraisalPropertyForCollateral(
            PropertyId: p.Id,
            PropertyTypeCode: p.PropertyType.Code,
            PropertyGroupId: propertyGroupId,
            GroupNumber: groupNumber,
            SequenceInGroup: sequenceInGroup,
            AppraisedValue: appraisedValue,
            PricingInfo: pricingInfo,
            LandIdentity: landIdentity,
            CondoIdentity: condoIdentity,
            LeaseholdIdentity: leaseholdIdentity,
            MachineryIdentity: machineryIdentity,
            BuildingIdentity: buildingIdentity,
            ConstructionInspection: constructionInspection
        );
    }

    private static ConstructionInspectionForCollateral MapConstructionInspection(
        Domain.Appraisals.ConstructionInspection ci)
    {
        List<ConstructionWorkDetailForCollateral>? workDetails = null;

        if (ci.IsFullDetail)
        {
            workDetails = ci.WorkDetails
                .OrderBy(d => d.DisplayOrder)
                .Select(d => new ConstructionWorkDetailForCollateral(
                    WorkDetailId: d.Id,
                    ConstructionWorkGroupId: d.ConstructionWorkGroupId,
                    ConstructionWorkItemId: d.ConstructionWorkItemId,
                    WorkItemName: d.WorkItemName,
                    DisplayOrder: d.DisplayOrder,
                    ProportionPct: d.ProportionPct,
                    PreviousProgressPct: d.PreviousProgressPct,
                    CurrentProgressPct: d.CurrentProgressPct,
                    CurrentProportionPct: d.CurrentProportionPct,
                    ConstructionValue: d.ConstructionValue
                ))
                .ToList();
        }

        return new ConstructionInspectionForCollateral(
            InspectionId: ci.Id,
            IsFullDetail: ci.IsFullDetail,
            OverallCurrentProgressPercent: ci.OverallCurrentProgressPercent,
            WorkDetails: workDetails,
            SummaryCurrentProgressPct: ci.SummaryCurrentProgressPct,
            SummaryCurrentValue: ci.SummaryCurrentValue,
            SummaryPreviousProgressPct: ci.SummaryPreviousProgressPct,
            SummaryPreviousValue: ci.SummaryPreviousValue,
            SummaryDetail: ci.SummaryDetail,
            Remark: ci.Remark
        );
    }
}
