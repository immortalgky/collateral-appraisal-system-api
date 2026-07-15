using System.Text.Json;
using Appraisal.Contracts.Appraisals;
using Collateral.Contracts;
using Collateral.CollateralMasters.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Shared.Time;

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
    ILogger<CollateralMasterUpsertService> logger,
    IDateTimeProvider dateTimeProvider) : ICollateralMasterUpsertService
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

        // -----------------------------------------------------------------------
        // Block-project branch (PRJ) — runs BEFORE the per-property loop.
        // Block appraisals have no Properties rows so the per-property loop below
        // is a no-op for them; this branch fills that gap independently.
        // -----------------------------------------------------------------------
        if (appraisal.Project is not null)
        {
            await UpsertProjectAsync(appraisal, ct);
        }

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
        // One-collateral-per-appraisal: determine the SINGLE primary component up front.
        // This is pure data (no DB access) so it's stable for the whole method:
        //   - If ANY Land/LB property exists, the collapsed land master is UNCONDITIONALLY the
        //     primary (land always wins — confirmed product rule).
        //   - Otherwise, the group with the lowest GroupNumber is primary (ties keep the original
        //     property order — same tie-break the snapshot builder already used).
        // Every OTHER group's CollateralMaster becomes a typed ALIAS of the primary (IsMaster=false,
        // ParentMasterId=primary.Id) while keeping its own type detail — see CreateCondoAlias /
        // CreateMachineAlias / CreateLeaseholdAlias / CollateralMaster.DemoteToAlias.
        // -----------------------------------------------------------------------
        const string LandPrimaryGroupKey = "land:all";
        string? primaryGroupKey = landOrLbProperties.Count > 0
            ? LandPrimaryGroupKey
            : grouped.Count > 0
                ? grouped.OrderBy(g => g.GroupNumber ?? int.MaxValue).First().GroupKey
                : null;

        // Resolved once the primary group's master has been processed. Non-primary groups alias
        // to this master's Id. Passes are ordered (primary group first within each pass) so this
        // is populated before any non-primary group needs it — see the reordering below.
        CollateralMaster? primaryMaster = null;

        // -----------------------------------------------------------------------
        // Pass 1: Land + Condo + Machine — process each group
        // Pass-1 cache for Leasehold's underlying-resolution.
        // -----------------------------------------------------------------------
        var landMasterByPropertyId = new Dictionary<Guid, CollateralMaster>();
        // Track IsMaster for each group so we can build the snapshot. NOTE: a single group that
        // holds two non-land types (e.g. Condo + Leasehold sharing a GroupNumber) overwrites this
        // dict by GroupKey — the reconciliation step below must NOT rely on this dict alone, see
        // resolvedNonLandMasters.
        var groupIsMasters = new Dictionary<string, CollateralMaster>(); // groupKey → IsMaster
        // Track newly-created + existing aliases for each land group (for snapshot + UnitPrice propagation)
        var groupAliases = new Dictionary<string, List<CollateralMaster>>(); // groupKey → alias list
        // Track the resolved CollateralType per group (for engagement stamping)
        var groupCollateralTypes = new Dictionary<string, string>(); // groupKey → CollateralType code
        // Every non-land master/alias resolved or created in passes 1 & 2 — the reconciliation
        // safety net below walks THIS list (not groupIsMasters, which can lose entries when one
        // group holds two non-land types) so no resolved master is ever missed.
        var resolvedNonLandMasters = new List<CollateralMaster>();

        // Land: ONE IsMaster per appraisal. All Land/LB titles across ALL groups collapse into a
        // single IsMaster + aliases set (first title = IsMaster, every other title = alias).
        // UpsertLandGroupAsync already does "first title → IsMaster, all remaining titles across all
        // passed properties → aliases", so feeding it the full land set yields exactly one master.
        // Land is unconditionally the appraisal's primary when present — see primaryGroupKey above.
        CollateralMaster? landMaster = null;
        var landAliases = new List<CollateralMaster>();
        string? landCollateralType = null;
        int? landGroupNumber = null;
        if (landOrLbProperties.Count > 0)
        {
            (landMaster, landAliases, landCollateralType) =
                await UpsertLandGroupAsync(landOrLbProperties, appraisal, buildingProperties, ct);
            foreach (var lp in landOrLbProperties)
                landMasterByPropertyId[lp.PropertyId] = landMaster;
            // Primary ordering uses the lowest GroupNumber among the land groups.
            landGroupNumber = grouped
                .Where(g => g.Properties.Any(p => p.PropertyTypeCode is "L" or "LB"))
                .Min(g => g.GroupNumber ?? int.MaxValue);
            primaryMaster = landMaster;
        }

        // Condo / Machine: one IsMaster row per appraisal overall — only the group matching
        // primaryGroupKey stays IsMaster=true; every other non-land group becomes a typed alias of
        // the primary. The primary-matching group (if it lives in this pass) is processed FIRST so
        // primaryMaster is resolved before any alias creation needs its Id.
        foreach (var group in grouped
                     .Where(g => g.Properties.All(p => p.PropertyTypeCode is not "L" and not "LB"))
                     .OrderBy(g => g.GroupKey == primaryGroupKey ? 0 : 1))
        {
            var condoInGroup = group.Properties.Where(p => p.PropertyTypeCode == "U").ToList();
            var machineInGroup = group.Properties.Where(p => p.PropertyTypeCode == "MAC").ToList();
            bool isPrimaryGroup = group.GroupKey == primaryGroupKey;

            if (condoInGroup.Count > 0)
            {
                // Condo — typically one per group (singleton)
                var master = await UpsertCondoAsync(condoInGroup.First(), appraisal, isPrimaryGroup, primaryMaster?.Id, ct);
                groupIsMasters[group.GroupKey] = master;
                resolvedNonLandMasters.Add(master);
                if (isPrimaryGroup) primaryMaster = master;
            }
            else if (machineInGroup.Count > 0)
            {
                var master = await UpsertMachineAsync(machineInGroup.First(), appraisal, isPrimaryGroup, primaryMaster?.Id, ct);
                groupIsMasters[group.GroupKey] = master;
                resolvedNonLandMasters.Add(master);
                if (isPrimaryGroup) primaryMaster = master;
            }
        }

        // -----------------------------------------------------------------------
        // Pass 2: Leasehold (depends on underlying master already existing or created)
        // Same primary-first reordering as pass 1.
        // -----------------------------------------------------------------------
        var leaseholdGroups = grouped
            .Where(g => g.Properties.Any(p => p.PropertyTypeCode is "LSL" or "LSB" or "LS"))
            .OrderBy(g => g.GroupKey == primaryGroupKey ? 0 : 1)
            .ToList();

        foreach (var group in leaseholdGroups)
        {
            var lhProperty = group.Properties.First(p => p.PropertyTypeCode is "LSL" or "LSB" or "LS");
            bool isPrimaryGroup = group.GroupKey == primaryGroupKey;
            var master = await UpsertLeaseholdAsync(
                lhProperty, appraisal, landOrLbProperties, condoProperties, landMasterByPropertyId,
                isPrimaryGroup, primaryMaster?.Id, ct);
            groupIsMasters[group.GroupKey] = master;
            resolvedNonLandMasters.Add(master);
            if (isPrimaryGroup) primaryMaster = master;
        }

        // -----------------------------------------------------------------------
        // Reconciliation safety net: enforce "exactly one IsMaster per appraisal" even in the rare
        // ordering edge case where a non-primary group had to be resolved before the primary was
        // known (e.g. the primary is a Leasehold-only group with no Land/Condo/Machine in the same
        // appraisal — Leasehold is processed last, in pass 2). Also catches legacy standalone
        // masters left over from before this model that a group's dedup key happened to match.
        // Walks resolvedNonLandMasters (not groupIsMasters, which loses entries when a single
        // group holds two non-land types — e.g. Condo + Leasehold sharing a GroupNumber — because
        // the second type's write overwrites the first in that dict). Deduped by Id since the same
        // master can appear more than once (e.g. resolved once per property that maps to it).
        //
        // CORE RULE: a row that already owns engagement history was appraised standalone as its
        // OWN collateral — it must stay IsMaster (cross-appraisal reuse), never demoted. Only
        // engagement-free rows may become aliases. No-ops for every row already created correctly
        // via the alias factories above.
        // -----------------------------------------------------------------------
        if (primaryMaster is not null)
        {
            foreach (var groupMaster in resolvedNonLandMasters
                         .GroupBy(m => m.Id)
                         .Select(g => g.First()))
            {
                if (groupMaster.Id == primaryMaster.Id || !groupMaster.IsMaster)
                    continue;

                if (groupMaster.Engagements.Count == 0)
                {
                    groupMaster.DemoteToAlias(primaryMaster.Id);
                    logger.LogInformation(
                        "ProcessAppraisalAsync: demoted CollateralMaster {MasterId} ({Type}) to a typed alias " +
                        "of primary {PrimaryId} for AppraisalId={AppraisalId}",
                        groupMaster.Id, groupMaster.CollateralType, primaryMaster.Id, appraisalId);
                }
                else
                {
                    logger.LogWarning(
                        "ProcessAppraisalAsync: {Type} master {MasterId} was appraised standalone (has " +
                        "{Count} engagement(s)); keeping it as its own IsMaster rather than demoting under " +
                        "primary {PrimaryId} for AppraisalId={AppraisalId}.",
                        groupMaster.CollateralType, groupMaster.Id, groupMaster.Engagements.Count,
                        primaryMaster.Id, appraisalId);
                }
            }
        }

        // -----------------------------------------------------------------------
        // Build the snapshot bucket list: ONE consolidated land bucket (all land titles across all
        // groups, under the single IsMaster) + every non-land group (condo / machine / leasehold).
        // This collapses all land groups into one snapshot group, matching the
        // one-IsMaster-per-appraisal model. Non-land groups reference their own (possibly aliased)
        // master row.
        // -----------------------------------------------------------------------
        var snapshotBuckets = new List<PropertyGroupBucket>();
        if (landMaster is not null)
        {
            snapshotBuckets.Add(new PropertyGroupBucket(LandPrimaryGroupKey, null, landGroupNumber, landOrLbProperties));
            groupIsMasters[LandPrimaryGroupKey] = landMaster;
            groupAliases[LandPrimaryGroupKey] = landAliases;
            groupCollateralTypes[LandPrimaryGroupKey] = landCollateralType!;
        }
        snapshotBuckets.AddRange(grouped
            .Where(g => g.Properties.All(p => p.PropertyTypeCode is not "L" and not "LB")));

        // -----------------------------------------------------------------------
        // Build the single engagement snapshot covering ALL groups, anchored on the SAME primary
        // resolved above (primaryGroupKey / primaryMaster) — one-collateral-per-appraisal model.
        // -----------------------------------------------------------------------
        if (snapshotBuckets.Count > 0 && primaryGroupKey is not null)
        {
            var groupSnapshots = BuildGroupSnapshots(snapshotBuckets, groupIsMasters, groupAliases, appraisal, buildingProperties, primaryGroupKey);

            var primaryGroup = snapshotBuckets.First(g => g.GroupKey == primaryGroupKey);

            if (primaryMaster is not null)
            {
                var snapshot = SnapshotBuilder.BuildAppraisalSnapshot(groupSnapshots);

                // Resolve engagement-time values from the primary group.
                groupCollateralTypes.TryGetValue(primaryGroup.GroupKey, out var primaryCollateralType);

                // For Condo / Machine, use the master's current CollateralType.
                var appraisedCollateralType = primaryCollateralType ?? primaryMaster.CollateralType;

                // Land area from the primary land property's LandIdentity (sq.wa).
                decimal? landAreaInSqWa = null;
                var primaryLandProps = primaryGroup.Properties
                    .Where(p => p.PropertyTypeCode is "L" or "LB")
                    .ToList();
                if (primaryLandProps.Count > 0)
                    landAreaInSqWa = primaryLandProps[0].LandIdentity?.LandArea;

                // Group-level appraisal value — PricingInfo is the same instance across all
                // properties in the group (set at group level; see AppraisalForCollateralResult).
                // Use the first in-scope property of the primary group that carries a PricingInfo.
                var primaryPricingProp = primaryGroup.Properties
                    .FirstOrDefault(p => p.PricingInfo is not null);
                var engagementAppraisalValue = primaryPricingProp?.PricingInfo?.AppraisalValue;

                AppendEngagement(primaryMaster, appraisal, snapshot, appraisedCollateralType, landAreaInSqWa, engagementAppraisalValue);

                // Append building rows to the engagement for every building in the appraisal.
                // We no longer match BuildingIdentity.BuiltOnTitleNumber against the land titles:
                // that ordinal match was fragile (dirty data such as a trailing space — e.g.
                // "619257 " vs land title "619257" — silently dropped the building). Instead we
                // assume every building in the appraisal sits on this appraisal's land and attach
                // it to the primary (IsMaster) land engagement. Typed buildings are ordered first
                // so the regulatory export's representative building (Sequence=1) carries a type.
                var buildingsForPrimaryGroup = buildingProperties
                    .Where(b => b.BuildingIdentity is not null)
                    .OrderByDescending(b => !string.IsNullOrWhiteSpace(b.BuildingIdentity!.BuildingTypeCode))
                    .ToList();

                if (buildingsForPrimaryGroup.Count > 0 && primaryMaster.Engagements.Count > 0)
                {
                    // The engagement we just appended is always the last one.
                    var newEngagement = primaryMaster.Engagements[^1];
                    for (int seq = 0; seq < buildingsForPrimaryGroup.Count; seq++)
                    {
                        var b = buildingsForPrimaryGroup[seq];
                        // BuildingValue is intentionally null for v1: b.PricingInfo is the GROUP's
                        // shared pricing instance, so assigning it to every building row would
                        // duplicate the group total. A proper per-building value requires extending
                        // BuildingIdentityForCollateral with the building's own pricing component
                        // (separate task). The column is nullable to leave room for that.
                        // BuildingTypeCode is NOT NULL on the entity; a type-less building still
                        // attaches (for its area) with an empty code → blank type/name in the export.
                        newEngagement.AddBuilding(
                            buildingTypeCode: b.BuildingIdentity!.BuildingTypeCode ?? string.Empty,
                            buildingArea: b.BuildingIdentity.BuildingArea,
                            buildingValue: null,
                            sequence: seq + 1,
                            buildingAge: b.BuildingIdentity.BuildingAge,
                            numberOfFloors: b.BuildingIdentity.NumberOfFloors);
                    }
                }
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
                // Dedup key is TitleNumber + TitleType + Province + District + SubDistrict
                // (+ nullable SurveyNumber/LandParcelNumber/Rawang). LandOffice is descriptive, not a key field.
                var land = p.LandIdentity;
                if (land is null || !land.Titles.Any(t => !string.IsNullOrWhiteSpace(t.TitleNumber)))
                    missing.Add("TitleNumber");
                if (land is null || !land.Titles.Any(t => !string.IsNullOrWhiteSpace(t.TitleType)))
                    missing.Add("TitleType");
                if (string.IsNullOrWhiteSpace(land?.Province)) missing.Add("Province");
                if (string.IsNullOrWhiteSpace(land?.District)) missing.Add("District");
                if (string.IsNullOrWhiteSpace(land?.SubDistrict)) missing.Add("SubDistrict");
                break;
            }
            case "U":
            {
                // Dedup key is CondoRegistrationNumber + BuildingNumber + FloorNumber + RoomNumber
                // + Province + District + SubDistrict. LandOffice/TitleNumber/TitleType are not key fields.
                var condo = p.CondoIdentity;
                if (string.IsNullOrWhiteSpace(condo?.CondoRegistrationNumber)) missing.Add("CondoRegistrationNumber");
                if (string.IsNullOrWhiteSpace(condo?.BuildingNumber)) missing.Add("BuildingNumber");
                if (string.IsNullOrWhiteSpace(condo?.FloorNumber)) missing.Add("FloorNumber");
                if (string.IsNullOrWhiteSpace(condo?.RoomNumber)) missing.Add("RoomNumber");
                if (string.IsNullOrWhiteSpace(condo?.Province)) missing.Add("Province");
                if (string.IsNullOrWhiteSpace(condo?.District)) missing.Add("District");
                if (string.IsNullOrWhiteSpace(condo?.SubDistrict)) missing.Add("SubDistrict");
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
                bool hasTier1 = !string.IsNullOrWhiteSpace(m?.RegistrationNumber);
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
    /// <summary>
    /// Resolves or creates the land IsMaster + aliases for the group.
    /// Returns the IsMaster, all aliases, and the resolved CollateralType for this engagement.
    /// </summary>
    private async Task<(CollateralMaster IsMaster, List<CollateralMaster> Aliases, string CollateralType)> UpsertLandGroupAsync(
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
                land.Province!, land.District!, land.SubDistrict!,
                title.TitleType, title.TitleNumber,
                title.SurveyNumber, title.LandParcelNumber, title.Rawang, ct);

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

        // Determine the CollateralType based on whether ANY building exists in the appraisal.
        // Primary land property's code (L or LB) is the baseline type.
        var primaryPropertyTypeCode = landPropertiesInGroup[0].PropertyTypeCode; // "L" or "LB"
        // If the appraisal contains any building, the land is Land & Building. We no longer
        // require BuildingIdentity.BuiltOnTitleNumber to match a land title — that ordinal match
        // was fragile against dirty data (trailing spaces, null links) and is the same reason
        // building rows attach to the primary engagement without title matching (see
        // ProcessAppraisalAsync). A building in the appraisal sits on this appraisal's land.
        var appraisalHasBuilding = allBuildingProperties.Any(b => b.BuildingIdentity is not null);
        var resolvedCollateralType = appraisalHasBuilding
            ? CollateralTypes.LandWithBuilding // "LB"
            : primaryPropertyTypeCode;

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
                surveyNumber: firstTitle.SurveyNumber,
                landParcelNumber: firstTitle.LandParcelNumber,
                rawang: firstTitle.Rawang,
                street: null, village: null,
                latitude: null, longitude: null,
                collateralType: resolvedCollateralType);
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
                    surveyNumber: t.SurveyNumber,
                    landParcelNumber: t.LandParcelNumber,
                    rawang: t.Rawang,
                    collateralType: resolvedCollateralType);
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
                var tKey = BuildTitleKey(lpLand.Province!, lpLand.District!, lpLand.SubDistrict!, t.TitleType, t.TitleNumber, t.SurveyNumber, t.LandParcelNumber, t.Rawang);
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
                        surveyNumber: t.SurveyNumber,
                        landParcelNumber: t.LandParcelNumber,
                        rawang: t.Rawang,
                        collateralType: resolvedCollateralType);
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

        // LATEST-wins: flip CollateralType on the IsMaster AND every alias to the current
        // appraisal's classification. CollateralType is stored per row, so all title rows in the
        // group must agree — e.g. a previously-bare L group upgrades to LB when a building is
        // appraised on it. UpdateCollateralType early-returns when unchanged (no spurious events)
        // and has no IsMaster guard, so it is safe on aliases.
        master.UpdateCollateralType(resolvedCollateralType);
        foreach (var alias in newAliases)
            alias.UpdateCollateralType(resolvedCollateralType);

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
                "UnitPrice / BuildingValue / AppraisalValue will be null on this master.",
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
            AppraisalDate: appraisal.CompletedAt ?? dateTimeProvider.ApplicationNow,
            IsUnderConstruction: isUnderConstruction,
            OverallConstructionProgressPercent: overallPct,
            // UnitPrice: cost approach only — FinalValueAdjusted from PricingFinalValue (PR-8).
            UnitPrice: pricingInfo?.UnitPrice,
            // BuildingValue: cost approach only — from PricingFinalValue.BuildingValue (PR-8).
            BuildingValue: pricingInfo?.BuildingValue,
            // AppraisalValue: from PricingFinalValue (all approaches) (PR-8).
            AppraisalValue: pricingInfo?.AppraisalValue
        );

        master.UpsertFromLandAppraisal(upsertData);

        // -----------------------------------------------------------------------
        // Step 5: Propagate UnitPrice to alias rows (PR-8).
        // Per the three-value model spec, UnitPrice is stamped on every master in the group
        // (IsMaster + all aliases). BuildingValue and AppraisalValue are IsMaster only.
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

        return (master, newAliases, resolvedCollateralType);
    }

    /// <summary>
    /// When the resolved master for the appraisal's PRIMARY group turns out to be a typed alias
    /// (e.g. this component was demoted under a different primary in a prior appraisal, and this
    /// appraisal's composition now makes it the primary in its own right), PROMOTES it back to a
    /// standalone IsMaster instead of walking to its (foreign, unrelated) parent — an alias never
    /// owns engagements (DemoteToAlias's guard), so promotion is always safe and correct: this row
    /// IS the collateral this appraisal is valuing, not whatever it used to be aliased under.
    /// </summary>
    private CollateralMaster PromotePrimaryIfAlias(CollateralMaster master, string componentType)
    {
        if (master.IsMaster) return master;

        logger.LogInformation(
            "{ComponentType} master {MasterId} resolved as the appraisal's primary but was a typed " +
            "alias (ParentMasterId={ParentId}); promoting it to a standalone IsMaster " +
            "(one-collateral-per-appraisal model).",
            componentType, master.Id, master.ParentMasterId);

        master.PromoteToMaster();
        return master;
    }

    private async Task<CollateralMaster> UpsertCondoAsync(
        AppraisalPropertyForCollateral p,
        AppraisalForCollateralResult appraisal,
        bool isPrimary,
        Guid? primaryMasterId,
        CancellationToken ct)
    {
        var condo = p.CondoIdentity!;

        var master = await repo.FindCondoByDedupKey(
            condo.CondoRegistrationNumber!, condo.BuildingNumber!,
            condo.FloorNumber!, condo.RoomNumber!,
            condo.Province!, condo.District!, condo.SubDistrict!, ct);

        if (master is null)
        {
            // New component. Non-primary components are born as typed aliases of the appraisal's
            // primary collateral (one-collateral-per-appraisal model) whenever the primary is
            // already known; otherwise they're born as regular masters and the reconciliation
            // step in ProcessAppraisalAsync demotes them once the primary is resolved.
            master = !isPrimary && primaryMasterId is { } pid
                ? CollateralMaster.CreateCondoAlias(
                    parentMasterId: pid,
                    ownerName: condo.OwnerName ?? string.Empty,
                    landOfficeCode: condo.LandOffice,
                    condoRegistrationNumber: condo.CondoRegistrationNumber!,
                    buildingNumber: condo.BuildingNumber!,
                    floorNumber: condo.FloorNumber!,
                    roomNumber: condo.RoomNumber!,
                    province: condo.Province!,
                    district: condo.District!,
                    subDistrict: condo.SubDistrict!,
                    condoName: condo.CondoName)
                : CollateralMaster.CreateCondo(
                    ownerName: condo.OwnerName ?? string.Empty,
                    landOfficeCode: condo.LandOffice,
                    condoRegistrationNumber: condo.CondoRegistrationNumber!,
                    buildingNumber: condo.BuildingNumber!,
                    floorNumber: condo.FloorNumber!,
                    roomNumber: condo.RoomNumber!,
                    province: condo.Province!,
                    district: condo.District!,
                    subDistrict: condo.SubDistrict!,
                    condoName: condo.CondoName);
            repo.Add(master);
        }
        else if (isPrimary)
        {
            master = PromotePrimaryIfAlias(master, "Condo");
        }
        else if (master.IsMaster && primaryMasterId is { } parentId)
        {
            // Legacy standalone master (or a master created before the primary was known within
            // this same call) discovered as a non-primary component. Only demote it to a typed
            // alias when it has NEVER been engaged standalone — a row with engagement history IS
            // a real collateral in its own right and must stay IsMaster (cross-appraisal reuse).
            if (master.Engagements.Count == 0)
            {
                master.DemoteToAlias(parentId);
            }
            else
            {
                logger.LogWarning(
                    "ProcessAppraisalAsync: {Type} master {MasterId} was appraised standalone (has " +
                    "{Count} engagement(s)); keeping it as its own IsMaster rather than demoting under " +
                    "primary {PrimaryId} for AppraisalId={AppraisalId}.",
                    master.CollateralType, master.Id, master.Engagements.Count, parentId, appraisal.AppraisalId);
            }
        }
        // else: dedup key already resolved to an alias row — upsert detail directly on THAT row.
        // Do NOT re-anchor to its parent (that assumed land-only aliases and is wrong for typed
        // component aliases under the one-collateral-per-appraisal model).

        // Pricing values — sourced from PricingFinalValue of the selected approach (PR-8).
        var pricingInfo = p.PricingInfo;
        if (pricingInfo is null)
        {
            logger.LogWarning(
                "No PricingInfo for condo PropertyId={PropertyId} in AppraisalId={AppraisalId}. " +
                "UnitPrice / BuildingValue / AppraisalValue will be null on this master.",
                p.PropertyId, appraisal.AppraisalId);
        }

        var upsertData = new CondoUpsertData(
            OwnerName: condo.OwnerName,
            CondoName: condo.CondoName,
            UsableArea: condo.UsableArea,
            LocationType: condo.LocationType,
            BuildingAge: condo.BuildingAge,
            ConstructionYear: condo.ConstructionYear,
            ModelName: condo.ModelName,
            // GPS coordinates (Phase 1 — geo filter prerequisite)
            Latitude: condo.Latitude,
            Longitude: condo.Longitude,
            AppraisalId: appraisal.AppraisalId,
            AppraisalNumber: appraisal.AppraisalNumber ?? string.Empty,
            AppraisalDate: appraisal.CompletedAt ?? dateTimeProvider.ApplicationNow,
            // UnitPrice: cost approach only — FinalValueAdjusted from PricingFinalValue (PR-8).
            UnitPrice: pricingInfo?.UnitPrice,
            // BuildingValue: cost approach only — from PricingFinalValue.BuildingValue (PR-8).
            BuildingValue: pricingInfo?.BuildingValue,
            // AppraisalValue: from PricingFinalValue (all approaches) (PR-8).
            AppraisalValue: pricingInfo?.AppraisalValue
        );

        master.UpsertFromCondoAppraisal(upsertData);
        return master;
    }

    private async Task<CollateralMaster> UpsertMachineAsync(
        AppraisalPropertyForCollateral p,
        AppraisalForCollateralResult appraisal,
        bool isPrimary,
        Guid? primaryMasterId,
        CancellationToken ct)
    {
        var m = p.MachineryIdentity!;

        var master = await repo.FindMachineForUpsert(
            m.RegistrationNumber, m.SerialNo, m.Brand, m.Model, m.Manufacturer, ct);

        if (master is null)
        {
            master = !isPrimary && primaryMasterId is { } pid
                ? CollateralMaster.CreateMachineAlias(
                    parentMasterId: pid,
                    ownerName: m.OwnerName ?? string.Empty,
                    machineRegistrationNo: m.RegistrationNumber,
                    serialNo: m.SerialNo,
                    brand: m.Brand,
                    model: m.Model,
                    manufacturer: m.Manufacturer)
                : CollateralMaster.CreateMachine(
                    ownerName: m.OwnerName ?? string.Empty,
                    machineRegistrationNo: m.RegistrationNumber,
                    serialNo: m.SerialNo,
                    brand: m.Brand,
                    model: m.Model,
                    manufacturer: m.Manufacturer);
            repo.Add(master);
        }
        else if (isPrimary)
        {
            master = PromotePrimaryIfAlias(master, "Machine");
        }
        else if (master.IsMaster && primaryMasterId is { } parentId)
        {
            if (master.Engagements.Count == 0)
            {
                master.DemoteToAlias(parentId);
            }
            else
            {
                logger.LogWarning(
                    "ProcessAppraisalAsync: {Type} master {MasterId} was appraised standalone (has " +
                    "{Count} engagement(s)); keeping it as its own IsMaster rather than demoting under " +
                    "primary {PrimaryId} for AppraisalId={AppraisalId}.",
                    master.CollateralType, master.Id, master.Engagements.Count, parentId, appraisal.AppraisalId);
            }
        }
        // else: dedup key already resolved to an alias row — upsert detail directly on THAT row.

        var upsertData = new MachineUpsertData(
            IncomingRegistrationNo: m.RegistrationNumber,
            AppraisalId: appraisal.AppraisalId,
            AppraisalNumber: appraisal.AppraisalNumber ?? string.Empty,
            AppraisalDate: appraisal.CompletedAt ?? dateTimeProvider.ApplicationNow,
            LifeYear: m.LifeYear
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
        bool isPrimary,
        Guid? primaryMasterId,
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
                    landId.Province!, landId.District!, landId.SubDistrict!,
                    title.TitleType, title.TitleNumber,
                    title.SurveyNumber, title.LandParcelNumber, title.Rawang, ct);

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
                    surveyNumber: title.SurveyNumber,
                    landParcelNumber: title.LandParcelNumber,
                    rawang: title.Rawang,
                    street: null, village: null,
                    latitude: null, longitude: null);
                repo.Add(underlyingMaster);
            }
        }
        else if (condoProperties.FirstOrDefault() is { } condoSibling &&
                 condoSibling.CondoIdentity is { } condoId)
        {
            underlyingMaster = await repo.FindCondoByDedupKey(
                condoId.CondoRegistrationNumber!, condoId.BuildingNumber!,
                condoId.FloorNumber!, condoId.RoomNumber!,
                condoId.Province!, condoId.District!, condoId.SubDistrict!, ct);

            if (underlyingMaster is null)
            {
                underlyingMaster = CollateralMaster.CreateCondo(
                    ownerName: string.Empty,
                    landOfficeCode: condoId.LandOffice,
                    condoRegistrationNumber: condoId.CondoRegistrationNumber!,
                    buildingNumber: condoId.BuildingNumber!,
                    floorNumber: condoId.FloorNumber!,
                    roomNumber: condoId.RoomNumber!,
                    province: condoId.Province!,
                    district: condoId.District!,
                    subDistrict: condoId.SubDistrict!,
                    condoName: null);
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
            // Pass the resolved code so a fresh LS/LSB master is born with the right discriminator
            // — UpdateCollateralType below then no-ops on the insert path and only fires
            // CollateralTypeChangedEvent for true L→LB-style upgrades.
            leaseMaster = !isPrimary && primaryMasterId is { } pid
                ? CollateralMaster.CreateLeaseholdAlias(
                    parentMasterId: pid,
                    lessee: lh.LesseeName!,
                    leaseRegistrationNo: lh.ContractNo!,
                    underlyingMasterId: underlyingMaster.Id,
                    lessor: lh.LessorName!,
                    leaseTermStart: leaseTermStart,
                    collateralType: p.PropertyTypeCode)
                : CollateralMaster.CreateLeasehold(
                    lessee: lh.LesseeName!,
                    leaseRegistrationNo: lh.ContractNo!,
                    underlyingMasterId: underlyingMaster.Id,
                    lessor: lh.LessorName!,
                    leaseTermStart: leaseTermStart,
                    collateralType: p.PropertyTypeCode);
            repo.Add(leaseMaster);
        }
        else if (isPrimary)
        {
            leaseMaster = PromotePrimaryIfAlias(leaseMaster, "Leasehold");
        }
        else if (leaseMaster.IsMaster && primaryMasterId is { } parentId)
        {
            if (leaseMaster.Engagements.Count == 0)
            {
                leaseMaster.DemoteToAlias(parentId);
            }
            else
            {
                logger.LogWarning(
                    "ProcessAppraisalAsync: {Type} master {MasterId} was appraised standalone (has " +
                    "{Count} engagement(s)); keeping it as its own IsMaster rather than demoting under " +
                    "primary {PrimaryId} for AppraisalId={AppraisalId}.",
                    leaseMaster.CollateralType, leaseMaster.Id, leaseMaster.Engagements.Count, parentId, appraisal.AppraisalId);
            }
        }
        // else: dedup key already resolved to an alias row — upsert detail directly on THAT row.

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
            AppraisalDate: appraisal.CompletedAt ?? dateTimeProvider.ApplicationNow
        );

        // LATEST-wins: flip master CollateralType to the current appraisal's classification.
        // The leasehold property's PropertyTypeCode ("LSL", "LSB", or "LS") is the authoritative
        // input. Mirrors the Land path's UpdateCollateralType call so an LS/LSB appraisal applied
        // to a previously bare-LSL master upgrades the discriminator correctly.
        leaseMaster.UpdateCollateralType(p.PropertyTypeCode);

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
        IReadOnlyList<AppraisalPropertyForCollateral> allBuildingProperties,
        string primaryGroupKey)
    {
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
                // Non-land types: one entry per AppraisalProperty in the group. Role reflects the
                // row's ACTUAL IsMaster flag — a non-primary group's row may be a typed alias
                // (one-collateral-per-appraisal model) even though it still carries its own detail.
                var role = isMasterRow.IsMaster ? "isMaster" : "alias";
                foreach (var prop in group.Properties)
                {
                    if (prop.PropertyTypeCode == "U")
                    {
                        propertyEntries.Add(SnapshotBuilder.BuildCondoPropertyEntry(
                            isMasterRow.Id,
                            prop,
                            role: role,
                            unitPrice: isMasterRow.CondoDetail?.UnitPrice ?? prop.PricingInfo?.UnitPrice));
                    }
                    else if (prop.PropertyTypeCode == "MAC")
                    {
                        propertyEntries.Add(SnapshotBuilder.BuildMachinePropertyEntry(isMasterRow.Id, prop, role: role));
                    }
                    else if (prop.PropertyTypeCode is "LSL" or "LSB" or "LS")
                    {
                        var lhUnderlyingMasterId = isMasterRow.LeaseholdDetail?.UnderlyingMasterId ?? Guid.Empty;
                        var lhUnderlyingType = isMasterRow.LeaseholdDetail is not null ? "Land" : "Unknown";
                        propertyEntries.Add(SnapshotBuilder.BuildLeaseholdPropertyEntry(
                            isMasterRow.Id, prop, role: role, lhUnderlyingMasterId, lhUnderlyingType));
                    }
                }
            }

            // Group-level values (from the IsMaster master detail row)
            decimal? buildingCost = isMasterRow.LandDetail?.BuildingValue
                                    ?? isMasterRow.CondoDetail?.BuildingValue;
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
                BuildingValue = buildingCost,
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
            keys.Add(BuildTitleKey(ld.Province, ld.District, ld.SubDistrict, ld.TitleType, ld.TitleNumber, ld.SurveyNumber, ld.LandParcelNumber, ld.Rawang));
        foreach (var a in aliases)
        {
            if (a.LandDetail is { } ald)
                keys.Add(BuildTitleKey(ald.Province, ald.District, ald.SubDistrict, ald.TitleType, ald.TitleNumber, ald.SurveyNumber, ald.LandParcelNumber, ald.Rawang));
        }
        return keys;
    }

    // In-memory dedup key — MUST mirror the DB dedup key / UX_LandDetails_DedupKey_Active
    // (Province + District + SubDistrict + TitleType + TitleNumber + SurveyNumber +
    //  LandParcelNumber + Rawang). LandOfficeCode is NOT part of the key.
    private static string BuildTitleKey(
        string province, string amphur, string tambon,
        string titleType, string titleNo,
        string? surveyNumber, string? landParcelNumber, string? rawang)
        => $"{province}|{amphur}|{tambon}|{titleType}|{titleNo}|{surveyNumber}|{landParcelNumber}|{rawang}";

    /// <summary>
    /// Translates the Appraisal-side <see cref="ProjectUnitForCollateral"/> DTOs into
    /// Collateral-module <see cref="Collateral.CollateralMasters.Models.ProjectUnit"/> entities
    /// ready for insertion. Branches on ProjectType to call the correct factory.
    ///
    /// PurchaseBy translation: the DTO carries the enum NAME string ("Cash"/"Loan"/null).
    /// We parse it into <see cref="Collateral.CollateralMasters.Models.UnitPurchaseMethod"/> and
    /// apply domain invariants via <c>SetSaleInfo</c> (when method is known) or
    /// <c>MarkSold</c> (when sold but method unknown — bypasses the invariant to allow
    /// the user to correct via BUM screen).
    /// </summary>
    private static IReadOnlyList<CollateralMasters.Models.ProjectUnit> MapProjectUnits(
        Guid collateralMasterId,
        ProjectForCollateral proj)
    {
        var units = new List<CollateralMasters.Models.ProjectUnit>(proj.Units.Count);
        bool isCondo = string.Equals(proj.ProjectType, "U", StringComparison.OrdinalIgnoreCase);

        foreach (var dto in proj.Units)
        {
            ProjectUnit unit = isCondo
                ? ProjectUnit.CreateCondo(
                    collateralMasterId: collateralMasterId,
                    sequenceNumber: dto.SequenceNumber,
                    floor: dto.Floor,
                    towerName: dto.TowerName,
                    condoRegistrationNumber: dto.CondoRegistrationNumber,
                    roomNumber: dto.RoomNumber,
                    modelType: dto.ModelType,
                    usableArea: dto.UsableArea,
                    sellingPrice: dto.SellingPrice)
                : ProjectUnit.CreateLandAndBuilding(
                    collateralMasterId: collateralMasterId,
                    sequenceNumber: dto.SequenceNumber,
                    plotNumber: dto.PlotNumber,
                    houseNumber: dto.HouseNumber,
                    modelType: dto.ModelType,
                    numberOfFloors: dto.NumberOfFloors,
                    landArea: dto.LandArea,
                    usableArea: dto.UsableArea,
                    sellingPrice: dto.SellingPrice);

            // Apply sale-status. When PurchaseBy parses successfully we use SetSaleInfo (enforces
            // the Loan→LoanBankName invariant). When the unit is sold but PurchaseBy is unknown
            // (null or unrecognised string) we call MarkSold, which bypasses the invariant — the
            // user corrects via the BUM screen (consistent with Appraisal MarkSoldByReappraisal).
            if (dto.IsSold)
            {
                if (dto.PurchaseBy is not null
                    && Enum.TryParse<CollateralMasters.Models.UnitPurchaseMethod>(dto.PurchaseBy, out var method)
                    && Enum.IsDefined(method))
                {
                    unit.SetSaleInfo(isSold: true, purchaseBy: method, loanBankName: dto.LoanBankName);
                }
                else
                {
                    unit.MarkSold();
                }
            }

            unit.SetLastAppraisedValue(dto.AppraisedValue);

            units.Add(unit);
        }

        return units;
    }

    private void AppendEngagement(
        CollateralMaster primaryMaster,
        AppraisalForCollateralResult appraisal,
        string snapshot,
        string? appraisedCollateralType = null,
        decimal? landAreaInSqWa = null,
        decimal? appraisalValue = null)
    {
        Guid? companyId = appraisal.CompanyId.HasValue() && Guid.TryParse(appraisal.CompanyId, out var parsedCompanyId)
            ? parsedCompanyId
            : (Guid?)null;

        // Freeze the cost-approach Land/Building split from the just-upserted primary master, so the
        // outbound Collateral Result interface never recomputes from later-overwritten master state.
        decimal? landValue = null;
        decimal? buildingValue = null;
        var ld = primaryMaster.LandDetail;
        if (ld is not null && ld.UnitPrice is not null
            && primaryMaster.CollateralType is CollateralTypes.Land or CollateralTypes.LandWithBuilding)
        {
            if (landAreaInSqWa is not null)
                landValue = ld.UnitPrice.Value * landAreaInSqWa.Value;
            // BuildingValue intentionally stays null for bare Land — only L&B carries a building cost.
            if (primaryMaster.CollateralType == CollateralTypes.LandWithBuilding)
                buildingValue = ld.BuildingValue;
        }

        primaryMaster.AppendEngagement(
            appraisalId: appraisal.AppraisalId,
            appraisalNumber: appraisal.AppraisalNumber ?? string.Empty,
            requestId: appraisal.RequestId,
            requestNumber: appraisal.RequestNumber ?? string.Empty,
            appraisalType: appraisal.AppraisalType,
            appraisalDate: appraisal.CompletedAt ?? dateTimeProvider.ApplicationNow,
            appraiserUserId: appraisal.AppraiserUserId,
            appraisalCompanyId: companyId,
            appraisalCompanyName: appraisal.CompanyName,
            constructionInspectionFeeAmount: appraisal.ConstructionInspectionFeeAmount,
            snapshot: snapshot,
            createdAt: dateTimeProvider.ApplicationNow,
            appraisedCollateralType: appraisedCollateralType,
            landAreaInSqWa: landAreaInSqWa,
            appraisalValue: appraisalValue,
            forcedSaleValue: appraisal.ForcedSaleValue,
            internalAppraiserName: appraisal.AppraiserName,
            landValue: landValue,
            buildingValue: buildingValue,
            appraisalCompanyCode: appraisal.CompanyCode);
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

    // -------------------------------------------------------------------------
    // Block-project (PRJ) branch
    // -------------------------------------------------------------------------

    /// <summary>
    /// Upserts a single PRJ CollateralMaster for a block-project appraisal.
    ///
    /// Lineage dedup: if the appraisal carries a PrevAppraisalId, we look for an existing
    /// PRJ master whose LastAppraisalId matches that previous appraisal (i.e. the master was
    /// last updated by the prior appraisal in the same reappraisal chain). If found, we update
    /// it in-place. Otherwise we create a fresh master.
    ///
    /// Does NOT call SaveChangesAsync — changes are included in the single save at the end of
    /// ProcessAppraisalAsync.
    /// </summary>
    private async Task UpsertProjectAsync(AppraisalForCollateralResult appraisal, CancellationToken ct)
    {
        var proj = appraisal.Project!;

        // Serialize the project snapshot for the engagement audit record.
        // NOTE: this is used only for CollateralEngagement.Snapshot (audit trail), NOT for
        // ProjectDetail storage. ProjectDetail.StructureJson has been removed in Phase 1.
        var structureJson = JsonSerializer.Serialize(proj);

        // --- Lineage dedup ---
        CollateralMaster? master = null;

        if (appraisal.PrevAppraisalId.HasValue)
        {
            master = await repo.FindProjectMasterByLastAppraisalIdAsync(appraisal.PrevAppraisalId.Value, ct);
        }

        if (master is null)
        {
            // No existing lineage master found — create a fresh PRJ master.
            master = CollateralMaster.CreateProject(proj.ProjectType, proj.ProjectName);
            repo.Add(master);
            logger.LogInformation(
                "UpsertProjectAsync: created new PRJ master {MasterId} for AppraisalId={AppraisalId}",
                master.Id, appraisal.AppraisalId);
        }
        else
        {
            logger.LogInformation(
                "UpsertProjectAsync: reusing PRJ master {MasterId} via PrevAppraisalId={PrevAppraisalId} for AppraisalId={AppraisalId}",
                master.Id, appraisal.PrevAppraisalId, appraisal.AppraisalId);
        }

        // --- Map DTO units → Collateral ProjectUnit entities ---
        var collateralUnits = MapProjectUnits(master.Id, proj);

        // --- Replace happens through the tracked ProjectDetail.Units collection (ProjectDetail.ReplaceUnits
        // clears + re-adds). FindProjectMasterByLastAppraisalIdAsync eagerly loads Units, so EF deletes the
        // orphaned old rows and inserts the new ones in the SAME SaveChanges — atomic for both first-appraisal
        // (empty collection) and reappraisal (full replace). No eager ExecuteDeleteAsync (would commit before
        // the insert, risking unit loss if the later save throws). ---

        // --- Upsert last-known data ---
        var upsertData = new ProjectUpsertData(
            ProjectType: proj.ProjectType,
            ProjectName: proj.ProjectName,
            Developer: proj.Developer,
            Address: proj.Address,
            Province: proj.Province,
            Latitude: proj.Latitude,
            Longitude: proj.Longitude,
            TotalUnits: proj.TotalUnits,
            RemainingUnits: proj.RemainingUnits,
            ProjectSellingPrice: proj.ProjectSellingPrice,
            Units: collateralUnits,
            CustomerName: appraisal.CustomerName,
            AppraisalId: appraisal.AppraisalId,
            AppraisalNumber: appraisal.AppraisalNumber ?? string.Empty,
            AppraisalDate: appraisal.CompletedAt ?? dateTimeProvider.ApplicationNow
        );

        master.UpsertFromProjectAppraisal(upsertData);

        // --- Single engagement (idempotent via unique AppraisalId constraint) ---
        AppendEngagement(
            master,
            appraisal,
            snapshot: structureJson,
            appraisedCollateralType: CollateralTypes.Project,
            landAreaInSqWa: null,
            appraisalValue: proj.ProjectSellingPrice);
    }
}

file static class StringExtensions
{
    internal static bool HasValue(this string? s) =>
        !string.IsNullOrWhiteSpace(s);
}
