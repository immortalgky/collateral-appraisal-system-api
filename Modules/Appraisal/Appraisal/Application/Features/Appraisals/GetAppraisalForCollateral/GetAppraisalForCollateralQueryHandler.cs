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
    AppraisalDbContext dbContext
) : IQueryHandler<GetAppraisalForCollateralQuery, AppraisalForCollateralResult?>
{
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
        // DateTime.UtcNow handled at the upsert layer for safety).
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

        var properties = appraisal.Properties
            .Select(p => MapProperty(p, appraisedValueLookup))
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

        // CI fee is captured on the AppraisalFee linked to the latest non-rejected assignment.
        // Reused as the appraisal fee when a future CI appraisal targets the same collateral
        // (CI bypasses normal tier/quotation pipeline).
        decimal? constructionInspectionFee = null;
        if (latestAssignment is not null)
        {
            constructionInspectionFee = await dbContext.AppraisalFees
                .AsNoTracking()
                .Where(f => f.AssignmentId == latestAssignment.Id)
                .Select(f => f.ConstructionInspectionFeeAmount)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new AppraisalForCollateralResult(
            AppraisalId: appraisal.Id,
            AppraisalNumber: appraisal.AppraisalNumber,
            AppraisalType: appraisal.AppraisalType,
            CompletedAt: completedAt,
            RequestId: appraisal.RequestId,
            RequestNumber: null,    // not stored on Appraisal; caller leaves null
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
            .Where(pa => pa.PropertyGroupId != null && groupIds.Contains(pa.PropertyGroupId!.Value))
            .Select(pa => new { pa.PropertyGroupId, pa.FinalAppraisedValue })
            .ToListAsync(ct);

        // Step 3: join in-memory — PropertyGroupId → FinalAppraisedValue
        var groupValues = pricingAnalyses
            .GroupBy(pa => pa.PropertyGroupId!.Value)
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

    private static AppraisalPropertyForCollateral MapProperty(
        AppraisalProperty p,
        Dictionary<Guid, decimal?> appraisedValueLookup)
    {
        appraisedValueLookup.TryGetValue(p.Id, out var appraisedValue);

        var landIdentity = p.LandDetail is not null
            ? new LandIdentityForCollateral(
                Province: p.LandDetail.Address?.Province,
                District: p.LandDetail.Address?.District,
                SubDistrict: p.LandDetail.Address?.SubDistrict,
                LandOffice: p.LandDetail.Address?.LandOffice,
                Titles: p.LandDetail.Titles
                    .Select(t => new LandTitleForCollateral(t.Id, t.TitleNumber, t.TitleType))
                    .ToList()
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
                TitleNumber: p.CondoDetail.TitleNumber,
                TitleType: p.CondoDetail.TitleType
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
                RegistrationNo: p.MachineryDetail.RegistrationNo,
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
                BuiltOnTitleNumber: p.BuildingDetail.BuiltOnTitleNumber
            )
            : null;

        var constructionInspection = p.ConstructionInspection is not null
            ? MapConstructionInspection(p.ConstructionInspection)
            : null;

        return new AppraisalPropertyForCollateral(
            PropertyId: p.Id,
            PropertyTypeCode: p.PropertyType.Code,
            AppraisedValue: appraisedValue,
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
