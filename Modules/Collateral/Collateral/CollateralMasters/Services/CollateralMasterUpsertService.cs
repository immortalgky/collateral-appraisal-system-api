using Appraisal.Application.Features.Appraisals.GetAppraisalForCollateral;
using Collateral.CollateralMasters.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Collateral.CollateralMasters.Services;

/// <summary>
/// Implements the core write path: given a completed appraisal, finds or creates a
/// CollateralMaster for each in-scope property, upserts last-known data, and appends
/// a SINGLE engagement row per appraisal (unique by AppraisalId) anchored to the primary
/// IsMaster (the IsMaster of the principal/lowest-group-number group).
///
/// PR-4 redesign:
/// - Iterates by PropertyGroup within the appraisal.
/// - Each group produces one IsMaster + zero-or-more alias master rows.
/// - IsMaster/ParentMasterId are stable: set on first appraisal, never flip.
/// - Alias-alone handling: if a property's existing master is an alias whose parent IsMaster is
///   absent from this appraisal, the service resolves to the parent IsMaster and proceeds
///   gracefully (no exception). Validation upstream at the Request module prevents invalid submissions.
/// - Exactly one CollateralEngagement row per appraisal, idempotent via unique(AppraisalId).
/// </summary>
public class CollateralMasterUpsertService(
    ICollateralMasterRepository repo,
    ISender mediator,
    ILogger<CollateralMasterUpsertService> logger) : ICollateralMasterUpsertService
{
    // SQL Server unique-constraint violation error number
    private const int SqlUniqueConstraintViolation = 2627;
    private const int SqlUniqueIndexViolation = 2601;

    public async Task ProcessAppraisalAsync(Guid appraisalId, CancellationToken ct = default)
    {
        logger.LogInformation("ProcessAppraisalAsync started for AppraisalId={AppraisalId}", appraisalId);

        var appraisal = await mediator.Send(new GetAppraisalForCollateralQuery(appraisalId), ct);
        if (appraisal is null)
            throw new NotFoundException("Appraisal", appraisalId);

        if (string.IsNullOrWhiteSpace(appraisal.RequestNumber))
            logger.LogWarning(
                "RequestNumber is empty for AppraisalId={AppraisalId} RequestId={RequestId}. " +
                "No matching row found in request.Requests — engagement will store empty RequestNumber.",
                appraisalId, appraisal.RequestId);

        var allProperties = appraisal.Properties;

        // -----------------------------------------------------------------------
        // Classify properties
        // -----------------------------------------------------------------------
        var landOrLbProperties = allProperties
            .Where(p => p.PropertyTypeCode is "L" or "LB")
            .ToList();
        var buildingProperties = allProperties
            .Where(p => p.PropertyTypeCode is "B" or "LB")
            .ToList();
        var condoProperties = allProperties
            .Where(p => p.PropertyTypeCode == "U")
            .ToList();
        var machineryProperties = allProperties
            .Where(p => p.PropertyTypeCode == "MAC")
            .ToList();
        var leaseholdProperties = allProperties
            .Where(p => p.PropertyTypeCode is "LSL" or "LSB" or "LS")
            .ToList();

        var inScopeProperties = landOrLbProperties
            .Concat(condoProperties)
            .Concat(machineryProperties)
            .Concat(leaseholdProperties)
            .ToList();

        // -----------------------------------------------------------------------
        // Validation gate — per-type required identity fields
        // -----------------------------------------------------------------------
        ValidateAllProperties(inScopeProperties);

        // -----------------------------------------------------------------------
        // Group in-scope properties by PropertyGroupId.
        // Properties with no PropertyGroupId (ungrouped) each form their own implicit group.
        // -----------------------------------------------------------------------
        // PropertyGroupId comes from PropertyGroupItem via GetAppraisalForCollateralQueryHandler.
        // GroupNumber (from PropertyGroup) determines the principal group (lowest number = primary).
        var grouped = GroupPropertiesByGroup(inScopeProperties);

        // -----------------------------------------------------------------------
        // Pass 1: Land + Condo + Machine — process each group
        // Pass-1 cache for Leasehold's underlying-resolution.
        // -----------------------------------------------------------------------
        var landMasterByPropertyId = new Dictionary<Guid, CollateralMaster>();
        // Track IsMaster for each group so we can build the snapshot
        var groupIsMasters = new Dictionary<string, CollateralMaster>(); // groupKey → IsMaster
        // Track newly-created + existing aliases for each land group (for snapshot + UnitPrice propagation)
        var groupAliases = new Dictionary<string, List<CollateralMaster>>(); // groupKey → alias list

        foreach (var group in grouped)
        {
            var landInGroup = group.Properties.Where(p => p.PropertyTypeCode is "L" or "LB").ToList();
            var condoInGroup = group.Properties.Where(p => p.PropertyTypeCode == "U").ToList();
            var machineInGroup = group.Properties.Where(p => p.PropertyTypeCode == "MAC").ToList();

            // Land/LB properties in this group share an IsMaster
            if (landInGroup.Count > 0)
            {
                var (master, newAliases) = await UpsertLandGroupAsync(landInGroup, appraisal, buildingProperties, ct);
                foreach (var lp in landInGroup)
                    landMasterByPropertyId[lp.PropertyId] = master;
                groupIsMasters[group.GroupKey] = master;
                groupAliases[group.GroupKey] = newAliases;
            }
            else if (condoInGroup.Count > 0)
            {
                // Condo — typically one per group (singleton)
                var master = await UpsertCondoAsync(condoInGroup.First(), appraisal, ct);
                groupIsMasters[group.GroupKey] = master;
            }
            else if (machineInGroup.Count > 0)
            {
                var master = await UpsertMachineAsync(machineInGroup.First(), appraisal, ct);
                groupIsMasters[group.GroupKey] = master;
            }
        }

        // -----------------------------------------------------------------------
        // Pass 2: Leasehold (depends on underlying master already existing or created)
        // -----------------------------------------------------------------------
        var leaseholdGroups = grouped.Where(g =>
            g.Properties.Any(p => p.PropertyTypeCode is "LSL" or "LSB" or "LS")).ToList();

        foreach (var group in leaseholdGroups)
        {
            var lhProperty = group.Properties.First(p => p.PropertyTypeCode is "LSL" or "LSB" or "LS");
            var master = await UpsertLeaseholdAsync(lhProperty, appraisal, landOrLbProperties, condoProperties, landMasterByPropertyId, ct);
            groupIsMasters[group.GroupKey] = master;
        }

        // -----------------------------------------------------------------------
        // Build the single engagement snapshot covering ALL groups.
        // Primary = group with the lowest GroupNumber (or lowest implicit sequence for ungrouped).
        // -----------------------------------------------------------------------
        if (grouped.Count > 0)
        {
            var groupSnapshots = BuildGroupSnapshots(grouped, groupIsMasters, groupAliases, appraisal, buildingProperties);

            // Primary IsMaster = IsMaster of the principal group (first in ordered list)
            var primaryGroup = grouped.OrderBy(g => g.GroupNumber ?? int.MaxValue).First();
            var primaryMaster = groupIsMasters.GetValueOrDefault(primaryGroup.GroupKey);

            if (primaryMaster is not null)
            {
                var snapshot = SnapshotBuilder.BuildAppraisalSnapshot(groupSnapshots);
                AppendEngagement(primaryMaster, appraisal, snapshot);
            }
        }

        // -----------------------------------------------------------------------
        // Persist — domain events fire inside DispatchDomainEventInterceptor
        // -----------------------------------------------------------------------
        try
        {
            await repo.SaveChangesAsync(ct);
            logger.LogInformation(
                "ProcessAppraisalAsync completed for AppraisalId={AppraisalId}: {Count} in-scope properties processed",
                appraisalId, inScopeProperties.Count);
        }
        catch (DbUpdateException dbEx) when (IsEngagementUniqueViolation(dbEx))
        {
            // Idempotency: a concurrent consumer already inserted the engagement row
            // for this AppraisalId. Treat as success.
            logger.LogWarning(
                "ProcessAppraisalAsync: duplicate engagement detected for AppraisalId={AppraisalId} — treated as idempotent no-op",
                appraisalId);
        }
        catch (DbUpdateException dbEx) when (IsUniqueConstraintViolation(dbEx))
        {
            // Different unique-index violation (e.g. concurrent master/alias creation
            // colliding on LandDetails dedup key). This is NOT idempotent — surface for retry.
            var indexName = ExtractViolatedIndexName(dbEx);
            logger.LogError(dbEx,
                "ProcessAppraisalAsync: non-engagement unique-constraint violation for AppraisalId={AppraisalId}, Index={IndexName} — surfacing for retry",
                appraisalId, indexName ?? "<unknown>");
            throw;
        }
    }

    // -------------------------------------------------------------------------
    // Group helper
    // -------------------------------------------------------------------------

    private sealed record PropertyGroupBucket(
        string GroupKey,       // stable key for this group (GroupId string or "ungrouped:{propertyId}")
        Guid? GroupId,
        int? GroupNumber,
        IReadOnlyList<AppraisalPropertyForCollateral> Properties);

    private static List<PropertyGroupBucket> GroupPropertiesByGroup(
        IReadOnlyList<AppraisalPropertyForCollateral> properties)
    {
        // Properties with a PropertyGroupId are grouped together.
        // Properties without (PropertyGroupId == null) each form their own implicit singleton group.
        var grouped = new Dictionary<string, (Guid? GroupId, int? GroupNumber, List<AppraisalPropertyForCollateral> Props)>();

        foreach (var p in properties)
        {
            string key;
            Guid? groupId;
            int? groupNumber;

            if (p.PropertyGroupId.HasValue)
            {
                key = p.PropertyGroupId.Value.ToString();
                groupId = p.PropertyGroupId;
                groupNumber = p.GroupNumber;
            }
            else
            {
                // Ungrouped property — treat as its own group
                key = $"ungrouped:{p.PropertyId}";
                groupId = null;
                groupNumber = null;
            }

            if (!grouped.TryGetValue(key, out var bucket))
            {
                bucket = (groupId, groupNumber, []);
                grouped[key] = bucket;
            }
            bucket.Props.Add(p);
        }

        return grouped
            .Select(kv => new PropertyGroupBucket(
                kv.Key,
                kv.Value.GroupId,
                kv.Value.GroupNumber,
                kv.Value.Props))
            .ToList();
    }

    // -------------------------------------------------------------------------
    // Validation
    // -------------------------------------------------------------------------

    private static void ValidateAllProperties(List<AppraisalPropertyForCollateral> properties)
    {
        foreach (var p in properties)
        {
            var missing = GetMissingFields(p);
            if (missing.Count > 0)
                throw new MissingIdentityKeyException(p.PropertyId, p.PropertyTypeCode, missing);
        }
    }

    private static List<string> GetMissingFields(AppraisalPropertyForCollateral p)
    {
        var missing = new List<string>();

        switch (p.PropertyTypeCode)
        {
            case "L" or "LB":
            {
                var land = p.LandIdentity;
                if (land is null || !land.Titles.Any(t => !string.IsNullOrWhiteSpace(t.TitleNumber)))
                    missing.Add("TitleNumber");
                if (land is null || !land.Titles.Any(t => !string.IsNullOrWhiteSpace(t.TitleType)))
                    missing.Add("TitleType");
                if (string.IsNullOrWhiteSpace(land?.LandOffice)) missing.Add("LandOfficeCode");
                if (string.IsNullOrWhiteSpace(land?.Province)) missing.Add("Province");
                if (string.IsNullOrWhiteSpace(land?.District)) missing.Add("District");
                if (string.IsNullOrWhiteSpace(land?.SubDistrict)) missing.Add("SubDistrict");
                break;
            }
            case "U":
            {
                var condo = p.CondoIdentity;
                if (string.IsNullOrWhiteSpace(condo?.LandOffice)) missing.Add("LandOfficeCode");
                if (string.IsNullOrWhiteSpace(condo?.CondoRegistrationNumber)) missing.Add("CondoRegistrationNumber");
                if (string.IsNullOrWhiteSpace(condo?.BuildingNumber)) missing.Add("BuildingNumber");
                if (string.IsNullOrWhiteSpace(condo?.FloorNumber)) missing.Add("FloorNumber");
                if (string.IsNullOrWhiteSpace(condo?.RoomNumber)) missing.Add("RoomNumber");
                if (string.IsNullOrWhiteSpace(condo?.TitleNumber)) missing.Add("TitleNumber");
                if (string.IsNullOrWhiteSpace(condo?.TitleType)) missing.Add("TitleType");
                break;
            }
            case "LSL" or "LSB" or "LS":
            {
                var lh = p.LeaseholdIdentity;
                if (string.IsNullOrWhiteSpace(lh?.ContractNo)) missing.Add("ContractNo");
                if (string.IsNullOrWhiteSpace(lh?.LessorName)) missing.Add("Lessor");
                if (string.IsNullOrWhiteSpace(lh?.LesseeName)) missing.Add("Lessee");
                if (lh?.LeaseStartDate is null) missing.Add("LeaseTermStart");
                break;
            }
            case "MAC":
            {
                var m = p.MachineryIdentity;
                bool hasTier1 = !string.IsNullOrWhiteSpace(m?.RegistrationNo);
                bool hasTier2 = !string.IsNullOrWhiteSpace(m?.SerialNo)
                             && !string.IsNullOrWhiteSpace(m?.Brand)
                             && !string.IsNullOrWhiteSpace(m?.Model)
                             && !string.IsNullOrWhiteSpace(m?.Manufacturer);
                if (!hasTier1 && !hasTier2)
                    missing.Add("RegistrationNo or (SerialNo+Brand+Model+Manufacturer)");
                break;
            }
        }

        return missing;
    }

    // -------------------------------------------------------------------------
    // Per-type upsert helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Processes all Land/LB properties in a single group.
    /// Each property in the group is a separate title deed (LandTitle) but they all belong to
    /// the same physical plot grouping. In the existing model, one AppraisalProperty has multiple
    /// LandTitle entries (multi-title within one property), which map to IsMaster + aliases.
    ///
    /// When a group has multiple Land properties, we merge them all into one IsMaster group:
    /// the first property's first title becomes the IsMaster; all remaining titles across all
    /// properties become aliases.
    ///
    /// Returns the IsMaster row and the list of ALL alias rows for this group
    /// (both newly-created ones and any pre-existing ones loaded from the DB).
    /// The alias list is used for UnitPrice propagation (Step 5) and snapshot generation.
    /// Newly-created aliases are tracked in memory so they are visible before SaveChangesAsync —
    /// EF Core queries do not return Added-but-unsaved entities, so we combine both sources.
    /// </summary>
    private async Task<(CollateralMaster IsMaster, List<CollateralMaster> Aliases)> UpsertLandGroupAsync(
        IReadOnlyList<AppraisalPropertyForCollateral> landPropertiesInGroup,
        AppraisalForCollateralResult appraisal,
        List<AppraisalPropertyForCollateral> allBuildingProperties,
        CancellationToken ct)
    {
        // Collect all valid titles across all land properties in this group.
        // Each land property may have multiple LandTitle rows (multi-title support).
        var allTitlesWithOwner = landPropertiesInGroup
            .SelectMany(p => p.LandIdentity!.Titles
                .Where(t => !string.IsNullOrWhiteSpace(t.TitleNumber))
                .Select(t => (Property: p, Title: t)))
            .ToList();

        if (allTitlesWithOwner.Count == 0)
            throw new MissingIdentityKeyException(landPropertiesInGroup[0].PropertyId, "L", ["TitleNumber"]);

        var land = landPropertiesInGroup[0].LandIdentity!;

        // -----------------------------------------------------------------------
        // Step 1: For each title, look up any existing row (master or alias).
        // Resolve hits to their IsMaster row and collect distinct master IDs.
        // -----------------------------------------------------------------------
        var matchedMasterIds = new HashSet<Guid>();
        var resolvedMasters = new Dictionary<string, CollateralMaster>(); // id → master

        foreach (var (_, title) in allTitlesWithOwner)
        {
            var hit = await repo.FindLandByDedupKeyIncludingAliases(
                land.LandOffice!, land.Province!, land.District!, land.SubDistrict!,
                title.TitleType, title.TitleNumber, null, null, ct);

            if (hit is null) continue;

            CollateralMaster masterRow;
            if (hit.IsMaster)
            {
                masterRow = hit;
            }
            else
            {
                // Hit is an alias — load its parent
                var parent = await repo.FindByIdWithEngagementsAsync(hit.ParentMasterId!.Value, ct);
                if (parent is null)
                    throw new InvalidOperationException(
                        $"Alias row {hit.Id} references ParentMasterId={hit.ParentMasterId} which could not be found.");
                masterRow = parent;
            }

            matchedMasterIds.Add(masterRow.Id);
            resolvedMasters[masterRow.Id.ToString()] = masterRow;
        }

        // -----------------------------------------------------------------------
        // Step 2: Decide — empty / exactly-1 / conflict
        // Track newly-created aliases in memory so Step 5 can propagate UnitPrice to them
        // without relying on a DB query (EF Core won't return Added-but-unsaved entities).
        // -----------------------------------------------------------------------
        CollateralMaster master;
        var newAliases = new List<CollateralMaster>();

        if (matchedMasterIds.Count == 0)
        {
            // No existing group — create new IsMaster row with the FIRST title
            var (firstLandProp, firstTitle) = allTitlesWithOwner.First();
            var firstLand = firstLandProp.LandIdentity!;
            master = CollateralMaster.CreateLand(
                ownerName: string.Empty,
                landOfficeCode: firstLand.LandOffice!,
                province: firstLand.Province!,
                district: firstLand.District!,
                subDistrict: firstLand.SubDistrict!,
                titleType: firstTitle.TitleType,
                titleNumber: firstTitle.TitleNumber,
                surveyNumber: null,
                landParcelNumber: null,
                street: null, village: null,
                latitude: null, longitude: null);
            repo.Add(master);

            // Create alias rows for the remaining titles
            foreach (var (lp, t) in allTitlesWithOwner.Skip(1))
            {
                var lpLand = lp.LandIdentity!;
                var alias = CollateralMaster.CreateLandAlias(
                    parentMasterId: master.Id,
                    landOfficeCode: lpLand.LandOffice!,
                    province: lpLand.Province!,
                    district: lpLand.District!,
                    subDistrict: lpLand.SubDistrict!,
                    titleType: t.TitleType,
                    titleNumber: t.TitleNumber,
                    surveyNumber: null,
                    landParcelNumber: null);
                repo.Add(alias);
                newAliases.Add(alias);
            }
        }
        else if (matchedMasterIds.Count == 1)
        {
            // Existing group found — reuse master
            master = resolvedMasters[matchedMasterIds.First().ToString()];

            // Alias-alone guard: if any title hit was actually an alias whose IsMaster parent
            // is NOT in the resolved matches, that's a group composition violation.
            // (Already handled above by resolving aliases to their parents — if the parent is
            // in the DB and reachable, we use it. The guard is implicit: matchedMasterIds.Count==1
            // means all titles resolved to the same group's IsMaster, which is correct.)

            // Ensure all current appraisal titles have alias rows in this group.
            // existingAliases is used for title-key dedup; it will also be returned as the
            // full alias list (new aliases will be appended to newAliases below).
            var existingAliases = await repo.FindAliasesByParentMasterIdAsync(master.Id, ct);
            var existingTitleKeys = BuildExistingGroupTitleKeys(master, existingAliases);

            foreach (var (lp, t) in allTitlesWithOwner)
            {
                var lpLand = lp.LandIdentity!;
                var tKey = BuildTitleKey(lpLand.LandOffice!, lpLand.Province!, lpLand.District!, lpLand.SubDistrict!, t.TitleType, t.TitleNumber);
                if (!existingTitleKeys.Contains(tKey))
                {
                    // New title not yet in this group — create alias
                    var alias = CollateralMaster.CreateLandAlias(
                        parentMasterId: master.Id,
                        landOfficeCode: lpLand.LandOffice!,
                        province: lpLand.Province!,
                        district: lpLand.District!,
                        subDistrict: lpLand.SubDistrict!,
                        titleType: t.TitleType,
                        titleNumber: t.TitleNumber,
                        surveyNumber: null,
                        landParcelNumber: null);
                    repo.Add(alias);
                    newAliases.Add(alias);
                }
            }

            // Include all pre-existing aliases so the snapshot and UnitPrice propagation
            // see the complete alias list, not only the newly-created ones.
            newAliases.AddRange(existingAliases);
        }
        else
        {
            // More than 1 distinct master matched → cross-group title collision — admin must resolve
            var idList = string.Join(", ", matchedMasterIds);
            throw new ConflictException(
                $"The titles in this appraisal span multiple existing CollateralMaster groups: [{idList}]. " +
                "Admin merge is required before this appraisal can be processed.");
        }

        // -----------------------------------------------------------------------
        // Step 3: Graceful alias resolution for data-corruption edge case.
        // If the resolved master is still an alias (should not happen via the normal path
        // above — only possible on data corruption), resolve to its parent rather than fail.
        // Validation of alias-alone scenarios is enforced upstream at the Request module.
        // -----------------------------------------------------------------------
        if (!master.IsMaster)
        {
            logger.LogWarning(
                "Land master {MasterId} is unexpectedly still an alias with ParentMasterId={ParentId}. " +
                "Resolving to parent IsMaster (alias-alone guard removed in PR-7).",
                master.Id, master.ParentMasterId!.Value);

            var parent = await repo.FindByIdWithEngagementsAsync(master.ParentMasterId!.Value, ct);
            if (parent is null)
                throw new InvalidOperationException(
                    $"Alias row {master.Id} references ParentMasterId={master.ParentMasterId} which could not be found.");
            master = parent;
        }

        // -----------------------------------------------------------------------
        // Step 4: Update IsMaster with last-known + construction + appraisal data
        // -----------------------------------------------------------------------
        var titleNumbers = allTitlesWithOwner.Select(x => x.Title.TitleNumber).ToHashSet();
        var buildingsForThisGroup = allBuildingProperties
            .Where(b => b.BuildingIdentity?.BuiltOnTitleNumber is { } btn && titleNumbers.Contains(btn))
            .ToList();

        // Use the primary land property for last-known populate fields
        var primaryProp = landPropertiesInGroup[0];
        var ci = primaryProp.ConstructionInspection;
        bool isUnderConstruction = ci is not null && ci.OverallCurrentProgressPercent < 100m;
        decimal? overallPct = ci?.OverallCurrentProgressPercent;

        // Pricing values — sourced from the selected approach's PricingFinalValue (PR-8).
        // PricingInfo is shared across all properties in the same group (set at group level).
        // IsMaster gets all three values; aliases get only UnitPrice (applied further below).
        var pricingInfo = primaryProp.PricingInfo;
        if (pricingInfo is null)
        {
            logger.LogWarning(
                "No PricingInfo for land group containing PropertyId={PropertyId} in AppraisalId={AppraisalId}. " +
                "UnitPrice / BuildingCost / AppraisalValue will be null on this master.",
                primaryProp.PropertyId, appraisal.AppraisalId);
        }

        var upsertData = new LandUpsertData(
            OwnerName: land.OwnerName,
            LandShapeType: land.LandShapeType,
            LandZoneType: land.LandZoneType,
            UrbanPlanningType: land.UrbanPlanningType,
            AccessRoadWidth: land.AccessRoadWidth,
            RoadFrontage: land.RoadFrontage,
            LandArea: land.LandArea,
            Street: land.Street,
            Village: land.Village,
            Latitude: land.Latitude,
            Longitude: land.Longitude,
            AppraisalId: appraisal.AppraisalId,
            AppraisalNumber: appraisal.AppraisalNumber ?? string.Empty,
            AppraisalDate: appraisal.CompletedAt ?? DateTime.UtcNow,
            IsUnderConstruction: isUnderConstruction,
            OverallConstructionProgressPercent: overallPct,
            // UnitPrice: cost approach only — FinalValueAdjusted from PricingFinalValue (PR-8).
            UnitPrice: pricingInfo?.UnitPrice,
            // BuildingCost: cost approach only — from PricingFinalValue.BuildingCost (PR-8).
            BuildingCost: pricingInfo?.BuildingCost,
            // AppraisalValue: from PricingFinalValue (all approaches) (PR-8).
            AppraisalValue: pricingInfo?.AppraisalValue
        );

        master.UpsertFromLandAppraisal(upsertData);

        // -----------------------------------------------------------------------
        // Step 5: Propagate UnitPrice to alias rows (PR-8).
        // Per the three-value model spec, UnitPrice is stamped on every master in the group
        // (IsMaster + all aliases). BuildingCost and AppraisalValue are IsMaster only.
        //
        // IMPORTANT: newAliases already contains both newly-created aliases (not yet in DB) and
        // pre-existing aliases loaded earlier (matchedMasterIds.Count == 1 branch). We do NOT
        // issue another FindAliasesByParentMasterIdAsync here because EF Core queries skip
        // Added-but-unsaved entities, which would leave newly-created aliases with UnitPrice=null.
        // -----------------------------------------------------------------------
        if (pricingInfo?.UnitPrice is not null)
        {
            foreach (var alias in newAliases)
            {
                alias.LandDetail?.UpdateValues(
                    unitPrice: pricingInfo.UnitPrice,
                    buildingCost: null,
                    appraisalValue: null);
            }
        }

        return (master, newAliases);
    }

    private async Task<CollateralMaster> UpsertCondoAsync(
        AppraisalPropertyForCollateral p,
        AppraisalForCollateralResult appraisal,
        CancellationToken ct)
    {
        var condo = p.CondoIdentity!;

        var master = await repo.FindCondoByDedupKey(
            condo.LandOffice!, condo.CondoRegistrationNumber!, condo.BuildingNumber!,
            condo.FloorNumber!, condo.RoomNumber!, condo.TitleNumber!, condo.TitleType!, ct);

        if (master is null)
        {
            master = CollateralMaster.CreateCondo(
                ownerName: condo.OwnerName ?? string.Empty,
                landOfficeCode: condo.LandOffice!,
                condoRegistrationNumber: condo.CondoRegistrationNumber!,
                buildingNumber: condo.BuildingNumber!,
                floorNumber: condo.FloorNumber!,
                roomNumber: condo.RoomNumber!,
                titleNumber: condo.TitleNumber!,
                titleType: condo.TitleType!,
                condoName: condo.CondoName,
                province: condo.Province);
            repo.Add(master);
        }

        if (!master.IsMaster)
        {
            // Condo dedup-key matched an alias row.
            // Resolve to the parent IsMaster and process against that parent.
            // Validation upstream at the Request module prevents invalid alias-alone submissions.
            logger.LogWarning(
                "Condo master {MasterId} is an alias with ParentMasterId={ParentId}. " +
                "Resolving to parent IsMaster (graceful re-anchor, PR-7).",
                master.Id, master.ParentMasterId!.Value);

            var parent = await repo.FindByIdWithEngagementsAsync(master.ParentMasterId!.Value, ct);
            if (parent is null)
                throw new InvalidOperationException(
                    $"Condo alias {master.Id} references ParentMasterId={master.ParentMasterId} which could not be found.");
            master = parent;
        }

        // Pricing values — sourced from PricingFinalValue of the selected approach (PR-8).
        var pricingInfo = p.PricingInfo;
        if (pricingInfo is null)
        {
            logger.LogWarning(
                "No PricingInfo for condo PropertyId={PropertyId} in AppraisalId={AppraisalId}. " +
                "UnitPrice / BuildingCost / AppraisalValue will be null on this master.",
                p.PropertyId, appraisal.AppraisalId);
        }

        var upsertData = new CondoUpsertData(
            OwnerName: condo.OwnerName,
            CondoName: condo.CondoName,
            Province: condo.Province,
            UsableArea: condo.UsableArea,
            LocationType: condo.LocationType,
            BuildingAge: condo.BuildingAge,
            ConstructionYear: condo.ConstructionYear,
            ModelName: condo.ModelName,
            AppraisalId: appraisal.AppraisalId,
            AppraisalNumber: appraisal.AppraisalNumber ?? string.Empty,
            AppraisalDate: appraisal.CompletedAt ?? DateTime.UtcNow,
            // UnitPrice: cost approach only — FinalValueAdjusted from PricingFinalValue (PR-8).
            UnitPrice: pricingInfo?.UnitPrice,
            // BuildingCost: cost approach only — from PricingFinalValue.BuildingCost (PR-8).
            BuildingCost: pricingInfo?.BuildingCost,
            // AppraisalValue: from PricingFinalValue (all approaches) (PR-8).
            AppraisalValue: pricingInfo?.AppraisalValue
        );

        master.UpsertFromCondoAppraisal(upsertData);
        return master;
    }

    private async Task<CollateralMaster> UpsertMachineAsync(
        AppraisalPropertyForCollateral p,
        AppraisalForCollateralResult appraisal,
        CancellationToken ct)
    {
        var m = p.MachineryIdentity!;

        var master = await repo.FindMachineForUpsert(
            m.RegistrationNo, m.SerialNo, m.Brand, m.Model, m.Manufacturer, ct);

        if (master is null)
        {
            master = CollateralMaster.CreateMachine(
                ownerName: m.OwnerName ?? string.Empty,
                machineRegistrationNo: m.RegistrationNo,
                serialNo: m.SerialNo,
                brand: m.Brand,
                model: m.Model,
                manufacturer: m.Manufacturer);
            repo.Add(master);
        }

        var upsertData = new MachineUpsertData(
            IncomingRegistrationNo: m.RegistrationNo,
            AppraisalId: appraisal.AppraisalId,
            AppraisalNumber: appraisal.AppraisalNumber ?? string.Empty,
            AppraisalDate: appraisal.CompletedAt ?? DateTime.UtcNow
        );

        master.UpsertFromMachineAppraisal(upsertData);
        return master;
    }

    private async Task<CollateralMaster> UpsertLeaseholdAsync(
        AppraisalPropertyForCollateral p,
        AppraisalForCollateralResult appraisal,
        List<AppraisalPropertyForCollateral> landProperties,
        List<AppraisalPropertyForCollateral> condoProperties,
        Dictionary<Guid, CollateralMaster> landMasterByPropertyId,
        CancellationToken ct)
    {
        var lh = p.LeaseholdIdentity!;

        if (lh.LeaseStartDate is null)
            throw new MissingIdentityKeyException(p.PropertyId, p.PropertyTypeCode, ["LeaseTermStart"]);

        var leaseTermStart = DateOnly.FromDateTime(lh.LeaseStartDate.Value);

        // ---- Resolve or create the underlying master ----
        CollateralMaster? underlyingMaster = null;

        var landSibling = landProperties.FirstOrDefault();
        if (landSibling?.LandIdentity is { } landId && landId.Titles.Any(t => !string.IsNullOrWhiteSpace(t.TitleNumber)))
        {
            var title = landId.Titles.First(t => !string.IsNullOrWhiteSpace(t.TitleNumber));

            landMasterByPropertyId.TryGetValue(landSibling.PropertyId, out underlyingMaster);

            if (underlyingMaster is null)
            {
                var landHit = await repo.FindLandByDedupKeyIncludingAliases(
                    landId.LandOffice!, landId.Province!, landId.District!, landId.SubDistrict!,
                    title.TitleType, title.TitleNumber, null, null, ct);

                if (landHit is not null && !landHit.IsMaster)
                {
                    underlyingMaster = await repo.FindByIdWithEngagementsAsync(landHit.ParentMasterId!.Value, ct);
                }
                else
                {
                    underlyingMaster = landHit;
                }
            }

            if (underlyingMaster is null)
            {
                underlyingMaster = CollateralMaster.CreateLand(
                    ownerName: string.Empty,
                    landOfficeCode: landId.LandOffice!,
                    province: landId.Province!,
                    district: landId.District!,
                    subDistrict: landId.SubDistrict!,
                    titleType: title.TitleType,
                    titleNumber: title.TitleNumber,
                    surveyNumber: null,
                    landParcelNumber: null,
                    street: null, village: null,
                    latitude: null, longitude: null);
                repo.Add(underlyingMaster);
            }
        }
        else if (condoProperties.FirstOrDefault() is { } condoSibling &&
                 condoSibling.CondoIdentity is { } condoId)
        {
            underlyingMaster = await repo.FindCondoByDedupKey(
                condoId.LandOffice!, condoId.CondoRegistrationNumber!, condoId.BuildingNumber!,
                condoId.FloorNumber!, condoId.RoomNumber!, condoId.TitleNumber!, condoId.TitleType!, ct);

            if (underlyingMaster is null)
            {
                underlyingMaster = CollateralMaster.CreateCondo(
                    ownerName: string.Empty,
                    landOfficeCode: condoId.LandOffice!,
                    condoRegistrationNumber: condoId.CondoRegistrationNumber!,
                    buildingNumber: condoId.BuildingNumber!,
                    floorNumber: condoId.FloorNumber!,
                    roomNumber: condoId.RoomNumber!,
                    titleNumber: condoId.TitleNumber!,
                    titleType: condoId.TitleType!,
                    condoName: null,
                    province: condoId.Province);
                repo.Add(underlyingMaster);
            }
        }
        else
        {
            throw new MissingIdentityKeyException(
                p.PropertyId, p.PropertyTypeCode,
                ["UnderlyingProperty — no Land or Condo sibling found in the same appraisal"]);
        }

        // ---- Find or create the leasehold master itself ----
        var leaseMaster = await repo.FindLeaseholdByDedupKey(
            lh.ContractNo!, underlyingMaster.Id, lh.LessorName!, lh.LesseeName!, leaseTermStart, ct);

        if (leaseMaster is null)
        {
            leaseMaster = CollateralMaster.CreateLeasehold(
                lessee: lh.LesseeName!,
                leaseRegistrationNo: lh.ContractNo!,
                underlyingMasterId: underlyingMaster.Id,
                lessor: lh.LessorName!,
                leaseTermStart: leaseTermStart);
            repo.Add(leaseMaster);
        }

        DateOnly? leaseTermEnd = lh.LeaseEndDate.HasValue
            ? DateOnly.FromDateTime(lh.LeaseEndDate.Value)
            : null;

        int? leaseTermMonths = null;
        if (leaseTermEnd.HasValue)
        {
            var start = leaseTermStart;
            var end = leaseTermEnd.Value;
            leaseTermMonths = (end.Year - start.Year) * 12 + (end.Month - start.Month);
            if (leaseTermMonths < 0) leaseTermMonths = 0;
        }

        var leaseholdUpsertData = new LeaseholdUpsertData(
            LeaseTermEnd: leaseTermEnd,
            LeaseTermMonths: leaseTermMonths,
            AppraisalId: appraisal.AppraisalId,
            AppraisalNumber: appraisal.AppraisalNumber ?? string.Empty,
            AppraisalDate: appraisal.CompletedAt ?? DateTime.UtcNow
        );

        leaseMaster.UpsertFromLeaseholdAppraisal(leaseholdUpsertData);
        return leaseMaster;
    }

    // -------------------------------------------------------------------------
    // Snapshot building
    // -------------------------------------------------------------------------

    private static List<PropertyGroupSnapshot> BuildGroupSnapshots(
        IReadOnlyList<PropertyGroupBucket> groups,
        IReadOnlyDictionary<string, CollateralMaster> groupIsMasters,
        IReadOnlyDictionary<string, List<CollateralMaster>> groupAliases,
        AppraisalForCollateralResult appraisal,
        IReadOnlyList<AppraisalPropertyForCollateral> allBuildingProperties)
    {
        // Principal group = lowest GroupNumber (or first in list if all null)
        var primaryGroupKey = groups
            .OrderBy(g => g.GroupNumber ?? int.MaxValue)
            .First()
            .GroupKey;

        var snapshots = new List<PropertyGroupSnapshot>();

        foreach (var group in groups.OrderBy(g => g.GroupNumber ?? int.MaxValue))
        {
            if (!groupIsMasters.TryGetValue(group.GroupKey, out var isMasterRow))
                continue;

            bool isPrimary = group.GroupKey == primaryGroupKey;
            groupAliases.TryGetValue(group.GroupKey, out var aliases);

            // Build property entries — one entry per CollateralMaster row (IsMaster + aliases).
            // For Land groups: the IsMaster and each alias get their own entry, each carrying the
            // title that corresponds to their dedup key and their own collateralMasterId.
            // For non-Land groups (Condo, Machine, Leasehold): always a single IsMaster entry.
            var propertyEntries = new List<object>();

            var landPropsInGroup = group.Properties
                .Where(p => p.PropertyTypeCode is "L" or "LB")
                .ToList();

            if (landPropsInGroup.Count > 0)
            {
                // Flatten all land titles from all properties in this group into a lookup by title key.
                // We need to find the AppraisalProperty that contributed each title so we can pass
                // the right property context (address, coordinates, pricingInfo) per master entry.
                var allTitlesInGroup = landPropsInGroup
                    .SelectMany(p => p.LandIdentity!.Titles
                        .Where(t => !string.IsNullOrWhiteSpace(t.TitleNumber))
                        .Select(t => (Property: p, Title: t)))
                    .ToList();

                // Emit one entry for the IsMaster row
                if (isMasterRow.LandDetail is { } isMasterLd)
                {
                    // Find the title in the appraisal data that matches this IsMaster's dedup key
                    var matchingTitle = allTitlesInGroup
                        .FirstOrDefault(x =>
                            string.Equals(x.Title.TitleNumber, isMasterLd.TitleNumber, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(x.Title.TitleType, isMasterLd.TitleType, StringComparison.OrdinalIgnoreCase));

                    // Use shared address/context from the primary land property for the IsMaster entry
                    var primaryLandProp = matchingTitle.Property ?? landPropsInGroup[0];
                    var isMasterTitle = matchingTitle.Title;
                    var isMasterUnitPrice = isMasterRow.LandDetail.UnitPrice;

                    propertyEntries.Add(SnapshotBuilder.BuildLandMasterEntry(
                        collateralMasterId: isMasterRow.Id,
                        property: primaryLandProp,
                        role: "isMaster",
                        titleNumber: isMasterLd.TitleNumber,
                        titleType: isMasterLd.TitleType,
                        unitPrice: isMasterUnitPrice));
                }

                // Emit one entry per alias row
                foreach (var aliasRow in aliases ?? [])
                {
                    if (aliasRow.LandDetail is not { } aliasLd) continue;

                    var matchingAlias = allTitlesInGroup
                        .FirstOrDefault(x =>
                            string.Equals(x.Title.TitleNumber, aliasLd.TitleNumber, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(x.Title.TitleType, aliasLd.TitleType, StringComparison.OrdinalIgnoreCase));

                    var aliasProp = matchingAlias.Property ?? landPropsInGroup[0];
                    var aliasUnitPrice = aliasRow.LandDetail.UnitPrice;

                    propertyEntries.Add(SnapshotBuilder.BuildLandMasterEntry(
                        collateralMasterId: aliasRow.Id,
                        property: aliasProp,
                        role: "alias",
                        titleNumber: aliasLd.TitleNumber,
                        titleType: aliasLd.TitleType,
                        unitPrice: aliasUnitPrice));
                }
            }
            else
            {
                // Non-land types: one entry per AppraisalProperty in the group
                foreach (var prop in group.Properties)
                {
                    if (prop.PropertyTypeCode == "U")
                    {
                        propertyEntries.Add(SnapshotBuilder.BuildCondoPropertyEntry(
                            isMasterRow.Id,
                            prop,
                            role: "isMaster",
                            unitPrice: isMasterRow.CondoDetail?.UnitPrice ?? prop.PricingInfo?.UnitPrice));
                    }
                    else if (prop.PropertyTypeCode == "MAC")
                    {
                        propertyEntries.Add(SnapshotBuilder.BuildMachinePropertyEntry(isMasterRow.Id, prop, role: "isMaster"));
                    }
                    else if (prop.PropertyTypeCode is "LSL" or "LSB" or "LS")
                    {
                        var lhUnderlyingMasterId = isMasterRow.LeaseholdDetail?.UnderlyingMasterId ?? Guid.Empty;
                        var lhUnderlyingType = isMasterRow.LeaseholdDetail is not null ? "Land" : "Unknown";
                        propertyEntries.Add(SnapshotBuilder.BuildLeaseholdPropertyEntry(
                            isMasterRow.Id, prop, role: "isMaster", lhUnderlyingMasterId, lhUnderlyingType));
                    }
                }
            }

            // Group-level values (from the IsMaster master detail row)
            decimal? buildingCost = isMasterRow.LandDetail?.BuildingCost
                                    ?? isMasterRow.CondoDetail?.BuildingCost;
            decimal? groupAppraisalValue = isMasterRow.LandDetail?.AppraisalValue
                                           ?? isMasterRow.CondoDetail?.AppraisalValue;

            // Construction inspections for this group (land + buildings on those lands)
            var titleNumbers = group.Properties
                .Where(p => p.LandIdentity is not null)
                .SelectMany(p => p.LandIdentity!.Titles.Select(t => t.TitleNumber))
                .ToHashSet();

            var ciProperties = group.Properties
                .Where(p => p.ConstructionInspection is not null)
                .Concat(allBuildingProperties.Where(b =>
                    b.BuildingIdentity?.BuiltOnTitleNumber is { } btn && titleNumbers.Contains(btn)
                    && b.ConstructionInspection is not null))
                .ToList();

            var ciList = SnapshotBuilder.BuildConstructionInspectionsForGroup(ciProperties);

            snapshots.Add(new PropertyGroupSnapshot
            {
                GroupId = group.GroupId?.ToString(),
                GroupNumber = group.GroupNumber,
                IsMasterId = isMasterRow.Id.ToString(),
                IsPrimary = isPrimary,
                BuildingCost = buildingCost,
                AppraisalValue = groupAppraisalValue,
                Properties = propertyEntries,
                ConstructionInspections = ciList
            });
        }

        return snapshots;
    }

    // -------------------------------------------------------------------------
    // Shared helpers
    // -------------------------------------------------------------------------

    private static HashSet<string> BuildExistingGroupTitleKeys(
        CollateralMaster master,
        List<CollateralMaster> aliases)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (master.LandDetail is { } ld)
            keys.Add(BuildTitleKey(ld.LandOfficeCode, ld.Province, ld.District, ld.SubDistrict, ld.TitleType, ld.TitleNumber));
        foreach (var a in aliases)
        {
            if (a.LandDetail is { } ald)
                keys.Add(BuildTitleKey(ald.LandOfficeCode, ald.Province, ald.District, ald.SubDistrict, ald.TitleType, ald.TitleNumber));
        }
        return keys;
    }

    private static string BuildTitleKey(
        string landOffice, string province, string amphur, string tambon,
        string titleType, string titleNo)
        => $"{landOffice}|{province}|{amphur}|{tambon}|{titleType}|{titleNo}";

    private static void AppendEngagement(
        CollateralMaster primaryMaster,
        AppraisalForCollateralResult appraisal,
        string snapshot)
    {
        Guid? companyId = appraisal.CompanyId.HasValue() && Guid.TryParse(appraisal.CompanyId, out var parsedCompanyId)
            ? parsedCompanyId
            : (Guid?)null;

        primaryMaster.AppendEngagement(
            appraisalId: appraisal.AppraisalId,
            appraisalNumber: appraisal.AppraisalNumber ?? string.Empty,
            requestId: appraisal.RequestId,
            requestNumber: appraisal.RequestNumber ?? string.Empty,
            appraisalType: appraisal.AppraisalType,
            appraisalDate: appraisal.CompletedAt ?? DateTime.UtcNow,
            appraiserUserId: appraisal.AppraiserUserId,
            appraisalCompanyId: companyId,
            appraisalCompanyName: appraisal.CompanyName,
            constructionInspectionFeeAmount: appraisal.ConstructionInspectionFeeAmount,
            snapshot: snapshot);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is SqlException sqlEx)
            return sqlEx.Number is SqlUniqueConstraintViolation or SqlUniqueIndexViolation;
        return false;
    }

    private static bool IsEngagementUniqueViolation(DbUpdateException ex)
    {
        if (!IsUniqueConstraintViolation(ex)) return false;
        return ex.Entries.Any(e => e.Entity is CollateralEngagement);
    }

    private static string? ExtractViolatedIndexName(DbUpdateException ex)
    {
        if (ex.InnerException is not SqlException sqlEx) return null;
        var msg = sqlEx.Message;
        var idx = msg.IndexOf("unique index '", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var start = idx + "unique index '".Length;
        var end = msg.IndexOf('\'', start);
        return end > start ? msg[start..end] : null;
    }
}

file static class StringExtensions
{
    internal static bool HasValue(this string? s) =>
        !string.IsNullOrWhiteSpace(s);
}
