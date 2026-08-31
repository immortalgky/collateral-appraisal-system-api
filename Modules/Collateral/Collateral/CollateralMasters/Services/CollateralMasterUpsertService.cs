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

    /// <summary>
    /// AppraisalProperty type codes that map to a leasehold CollateralMaster. Mirrors
    /// <c>PropertyType.IsLeaseAgreement</c> in the Appraisal module — LSU used to be missing here,
    /// so leasehold-condo properties were silently dropped without ever getting a master.
    /// </summary>
    private static readonly string[] LeaseholdPropertyTypes = CollateralTypes.LeaseholdFamily;

    private static bool IsLeaseholdType(string propertyTypeCode) =>
        LeaseholdPropertyTypes.Contains(propertyTypeCode, StringComparer.Ordinal);

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

        // Idempotency decided up front rather than by letting the unique index reject the insert.
        // CollateralEngagements is UNIQUE on AppraisalId, so a replay (CollateralBackfillJob, a
        // redelivered AppraisalCompleted message, ReplayAppraisal) must not append a second row.
        // Catching the violation afterwards was not enough: that catch wraps the whole
        // SaveChangesAsync, so a replay carrying legitimate master updates rolled those back as well
        // and still logged success. Skipping the append lets the master refresh commit normally.
        // The catch below stays as a backstop for the genuine concurrent-consumer race.
        var engagementExists = await repo.FindMasterByAppraisalIdAsync(appraisalId, ct) is not null;
        if (engagementExists)
            logger.LogInformation(
                "ProcessAppraisalAsync: AppraisalId={AppraisalId} already has an engagement — refreshing "
                + "master state only; no engagement or engagement-building rows will be appended.",
                appraisalId);


        // -----------------------------------------------------------------------
        // Block-project branch (PRJ) — runs BEFORE the per-property loop.
        // Block appraisals have no Properties rows so the per-property loop below
        // is a no-op for them; this branch fills that gap independently.
        // -----------------------------------------------------------------------
        if (appraisal.Project is not null)
        {
            await UpsertProjectAsync(appraisal, engagementExists, ct);
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
        // LSB / LS carry their own BuildingAppraisalDetail exactly like B / LB do, but they are
        // deliberately kept OUT of buildingProperties: that list also decides whether the land group
        // is typed L or LB, and a building belonging to a leasehold must not upgrade an unrelated
        // land master. It only feeds the engagement's building rows, which without it were empty for
        // every leasehold appraisal.
        var leaseholdBuildingProperties = allProperties
            .Where(p => p.PropertyTypeCode is "LSB" or "LS")
            .ToList();
        var condoProperties = allProperties
            .Where(p => p.PropertyTypeCode == "U")
            .ToList();
        var machineryProperties = allProperties
            .Where(p => p.PropertyTypeCode == "MAC")
            .ToList();
        var leaseholdProperties = allProperties
            .Where(p => IsLeaseholdType(p.PropertyTypeCode))
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

        // Masters already claimed while processing THIS appraisal. Passed to
        // FindMasterViaPreviousAppraisalAsync so two property groups cannot be handed the same
        // master by the fallback — the second group would otherwise overwrite the first group's
        // data and never get a master row of its own.
        var claimedMasterIds = new HashSet<Guid>();

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
                await UpsertLandGroupAsync(landOrLbProperties, appraisal, buildingProperties, claimedMasterIds, ct);
            foreach (var lp in landOrLbProperties)
                landMasterByPropertyId[lp.PropertyId] = landMaster;
            // Primary ordering uses the lowest GroupNumber among the land groups.
            landGroupNumber = grouped
                .Where(g => g.Properties.Any(p => p.PropertyTypeCode is "L" or "LB"))
                .Min(g => g.GroupNumber ?? int.MaxValue);
            primaryMaster = landMaster;
            claimedMasterIds.Add(landMaster.Id);
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
                var master = await UpsertCondoAsync(
                    condoInGroup.First(), appraisal, isPrimaryGroup, primaryMaster?.Id, claimedMasterIds, ct);
                groupIsMasters[group.GroupKey] = master;
                resolvedNonLandMasters.Add(master);
                claimedMasterIds.Add(master.Id);
                if (isPrimaryGroup) primaryMaster = master;
            }
            else if (machineInGroup.Count > 0)
            {
                var master = await UpsertMachineAsync(
                    machineInGroup.First(), appraisal, isPrimaryGroup, primaryMaster?.Id, claimedMasterIds, ct);
                groupIsMasters[group.GroupKey] = master;
                resolvedNonLandMasters.Add(master);
                claimedMasterIds.Add(master.Id);
                if (isPrimaryGroup) primaryMaster = master;
            }
        }

        // -----------------------------------------------------------------------
        // Pass 2: Leasehold (depends on underlying master already existing or created)
        // Same primary-first reordering as pass 1.
        // -----------------------------------------------------------------------
        var leaseholdGroups = grouped
            .Where(g => g.Properties.Any(p => IsLeaseholdType(p.PropertyTypeCode)))
            .OrderBy(g => g.GroupKey == primaryGroupKey ? 0 : 1)
            .ToList();

        // Every land row pass 1 touched (the collapsed IsMaster + its title aliases). Most are still
        // Added-but-unsaved, so the leasehold resolver has to match against these in memory before it
        // may query the DB — see ResolveUnderlyingMasterAsync.
        var pass1LandRows = landMaster is null
            ? []
            : new List<CollateralMaster> { landMaster }.Concat(landAliases).ToList();

        foreach (var group in leaseholdGroups)
        {
            var lhProperty = group.Properties.First(p => IsLeaseholdType(p.PropertyTypeCode));
            bool isPrimaryGroup = group.GroupKey == primaryGroupKey;
            var master = await UpsertLeaseholdAsync(
                lhProperty, appraisal, landOrLbProperties, condoProperties, leaseholdProperties,
                landMasterByPropertyId, pass1LandRows, isPrimaryGroup, primaryMaster?.Id,
                claimedMasterIds, ct);

            // Null means the underlying land / condo could not be resolved from any source. The
            // property is skipped with a warning rather than throwing: MissingIdentityKeyException
            // dead-letters the WHOLE appraisal, taking the land and machinery masters of unrelated
            // groups down with it — see UpsertLeaseholdAsync.
            if (master is null)
                continue;

            groupIsMasters[group.GroupKey] = master;
            resolvedNonLandMasters.Add(master);
            claimedMasterIds.Add(master.Id);
            if (isPrimaryGroup) primaryMaster = master;
        }

        // -----------------------------------------------------------------------
        // Primary fallback: the elected primary group produced no master at all.
        //
        // primaryGroupKey is picked from the property data alone (lowest GroupNumber), before any
        // group is known to be usable. Since the leasehold path now warns-and-skips instead of
        // throwing, the elected group can come back empty — its lease contract is half filled, or no
        // underlying land/condo could be resolved — while OTHER groups resolved perfectly well.
        // Leaving primaryMaster null there costs the whole appraisal its engagement, and with it the
        // HostCollateralId and every outbound interface, even though we hold masters for it.
        //
        // Fall back to the first master that did resolve, in the same group order the primary
        // election used, so the choice stays deterministic across replays.
        // -----------------------------------------------------------------------
        // (A land group always sets primaryMaster when it exists, so this only ever fires for
        // appraisals with no Land/LB at all.)
        if (primaryMaster is null && groupIsMasters.Count > 0)
        {
            var fallbackKey = grouped
                .OrderBy(g => g.GroupNumber ?? int.MaxValue)
                .Select(g => g.GroupKey)
                .FirstOrDefault(groupIsMasters.ContainsKey);

            if (fallbackKey is not null)
            {
                // It was resolved as a non-primary, so it may be an alias — the engagement must go on
                // an IsMaster row.
                primaryMaster = PromotePrimaryIfAlias(groupIsMasters[fallbackKey], "Fallback primary");
                groupIsMasters[fallbackKey] = primaryMaster;
                primaryGroupKey = fallbackKey;
            }

            if (primaryMaster is not null)
                logger.LogWarning(
                    "ProcessAppraisalAsync: the elected primary group produced no CollateralMaster for "
                    + "AppraisalId={AppraisalId} (its property was skipped). Falling back to master "
                    + "{MasterId} ({Type}) so the appraisal still gets an engagement.",
                    appraisalId, primaryMaster.Id, primaryMaster.CollateralType);
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

            // Everything in this block exists to build the engagement and its building rows, so a
            // replay skips it wholesale — appending buildings alone would attach a second set to the
            // engagement that already exists (the loop below reaches it via Engagements[^1]).
            if (primaryMaster is not null && !engagementExists)
            {
                var snapshot = SnapshotBuilder.BuildAppraisalSnapshot(groupSnapshots);

                // Resolve engagement-time values from the primary group.
                groupCollateralTypes.TryGetValue(primaryGroup.GroupKey, out var primaryCollateralType);

                // For Condo / Machine, use the master's current CollateralType.
                var appraisedCollateralType = primaryCollateralType ?? primaryMaster.CollateralType;

                // Land area from the primary land property's LandIdentity (sq.wa). LSL / LS are
                // included because they carry their own LandAppraisalDetail — a leasehold-primary
                // appraisal otherwise produced an engagement with no land area at all.
                decimal? landAreaInSqWa = null;
                var primaryLandProps = primaryGroup.Properties
                    .Where(p => p.PropertyTypeCode is "L" or "LB" or "LSL" or "LS")
                    .ToList();
                if (primaryLandProps.Count > 0)
                    landAreaInSqWa = primaryLandProps
                        .Select(p => p.LandIdentity?.LandArea)
                        .FirstOrDefault(a => a is not null);

                // Appraisal-level total from ValuationAnalyses (Σ across all PropertyGroups) — the
                // reliably-maintained appraisal value; the engagement represents this whole appraisal.
                // Fall back to the primary group's per-group PricingInfo value only if the total is absent.
                var primaryPricingProp = primaryGroup.Properties
                    .FirstOrDefault(p => p.PricingInfo is not null);
                var engagementAppraisalValue = appraisal.AppraisedValue
                                               ?? primaryPricingProp?.PricingInfo?.AppraisalValue;

                AppendEngagement(
                    primaryMaster, appraisal, snapshot, appraisedCollateralType, landAreaInSqWa,
                    engagementAppraisalValue,
                    // Group-shared cost-approach rate; null for non-cost approaches, which is what
                    // leaves LandValue null on the engagement exactly as before.
                    unitPrice: primaryPricingProp?.PricingInfo?.UnitPrice,
                    buildingCost: primaryPricingProp?.PricingInfo?.BuildingValue);

                // Append building rows to the engagement for every building in the appraisal.
                // We no longer match BuildingIdentity.BuiltOnTitleNumber against the land titles:
                // that ordinal match was fragile (dirty data such as a trailing space — e.g.
                // "619257 " vs land title "619257" — silently dropped the building). Instead we
                // assume every building in the appraisal sits on this appraisal's land and attach
                // it to the primary (IsMaster) land engagement. Typed buildings are ordered first
                // so the regulatory export's representative building (Sequence=1) carries a type.
                var buildingsForPrimaryGroup = buildingProperties
                    .Concat(leaseholdBuildingProperties)
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
        // ATTACH-ONLY: an appraisal that describes no collateral of its own, joined to the chain's
        // existing master.
        //
        // Construction-inspection appraisals routinely record only the BUILDING — or nothing at all —
        // because the inspector does not re-enter the land. `B` is not a collateral type, so
        // inScopeProperties comes out empty, no group is formed, no Upsert*Async runs, the dedup key is
        // never even queried, and therefore the chain fallback (which only fires on a dedup MISS) never
        // gets a chance. The appraisal then owns no engagement, and the construction progress never
        // reaches the collateral module: vw_RegulatoryExport reported IsUnderConstruction = 0 for all
        // 45,683 rows on U3 while 177 inspections sat below 100%.
        //
        // This is not a legacy-data artefact. On U3 the current year holds the most such appraisals of
        // any year (129), spread across the whole year and created by 20 different real user accounts.
        //
        // The rule agreed with the business: ATTACH to the master the chain already owns, and NEVER
        // create one. When the walk finds nothing we skip exactly as before — a fabricated master would
        // be permanent and there is no merge or split tool.
        //
        // Everything this branch may write: one CollateralEngagements row, its building rows, and — for
        // the L → LB upgrade below — the master's type discriminator. It touches no LandDetail, no
        // CondoDetail and no last-known field: those describe the collateral, which this appraisal does
        // not.
        // -----------------------------------------------------------------------
        if (primaryMaster is null
            && !engagementExists
            && inScopeProperties.Count == 0
            && appraisal.Project is null
            && appraisal.AncestorAppraisalIds.Count > 0)
        {
            var attachMaster = await FindMasterViaPreviousAppraisalAsync(
                appraisal,
                [
                    CollateralTypes.Land, CollateralTypes.LandWithBuilding,
                    CollateralTypes.Leasehold, CollateralTypes.LeaseholdBuilding,
                    CollateralTypes.LeaseholdWithBuilding
                ],
                "AttachOnly",
                claimedMasterIds,
                ct);

            if (attachMaster is null)
            {
                logger.LogWarning(
                    "AttachOnly: AppraisalId={AppraisalId} describes no collateral of its own "
                    + "({BuildingCount} building propert(ies), {PropertyCount} propert(ies) in total) and "
                    + "no ancestor owns a compatible master — skipping. No CollateralMaster was created.",
                    appraisalId, buildingProperties.Count, allProperties.Count);
            }
            else
            {
                // L → LB when this inspection carries a building. Without it the export throws the
                // construction data away: vw_RegulatoryExport short-circuits bare land to 0% BEFORE it
                // reads IsUnderConstruction, so a part-built building on a master still typed L reports
                // as complete. UpdateCollateralType is the model's existing LATEST-wins method and is
                // idempotent when the type already matches.
                //
                // Upgrade only. A building-less inspection is NOT evidence that the building is gone —
                // it only means this visit did not describe it — so LB is never downgraded to L. Same
                // rule, and same reasoning, as the leasehold path further down this file.
                if (buildingProperties.Count > 0)
                {
                    var upgraded = attachMaster.CollateralType switch
                    {
                        CollateralTypes.Land => CollateralTypes.LandWithBuilding,
                        CollateralTypes.Leasehold => CollateralTypes.LeaseholdBuilding,
                        _ => null,
                    };
                    if (upgraded is not null)
                    {
                        logger.LogInformation(
                            "AttachOnly: upgrading master {MasterId} from {OldType} to {NewType} — this "
                            + "inspection records a building on what was bare land (AppraisalId={AppraisalId})",
                            attachMaster.Id, attachMaster.CollateralType, upgraded, appraisalId);
                        attachMaster.UpdateCollateralType(upgraded);
                    }
                }

                // Empty snapshot: the snapshot describes the appraisal's own collateral groups and this
                // appraisal has none. Same convention as the AS400 legacy importer.
                AppendEngagement(
                    attachMaster, appraisal, snapshot: "{}",
                    appraisedCollateralType: attachMaster.CollateralType,
                    // No land property on this appraisal, so nothing to report and nothing to derive a
                    // cost-approach split from.
                    landAreaInSqWa: null,
                    appraisalValue: appraisal.AppraisedValue,
                    unitPrice: null,
                    buildingCost: null);

                // Building rows only when there are buildings. An appraisal with no property at all
                // still earns its engagement: it carries this chain's latest appraisal number, date and
                // value, which is what the regulatory export reports.
                var attachBuildings = buildingProperties
                    .Concat(leaseholdBuildingProperties)
                    .Where(b => b.BuildingIdentity is not null)
                    .OrderByDescending(b => !string.IsNullOrWhiteSpace(b.BuildingIdentity!.BuildingTypeCode))
                    .ToList();

                if (attachBuildings.Count > 0)
                {
                    var newEngagement = attachMaster.Engagements[^1];
                    for (int seq = 0; seq < attachBuildings.Count; seq++)
                    {
                        var b = attachBuildings[seq];
                        newEngagement.AddBuilding(
                            buildingTypeCode: b.BuildingIdentity!.BuildingTypeCode ?? string.Empty,
                            buildingArea: b.BuildingIdentity.BuildingArea,
                            buildingValue: null,
                            sequence: seq + 1,
                            buildingAge: b.BuildingIdentity.BuildingAge,
                            numberOfFloors: b.BuildingIdentity.NumberOfFloors);
                    }
                }

                logger.LogInformation(
                    "AttachOnly: attached AppraisalId={AppraisalId} to existing master {MasterId} "
                    + "({Type}) with {BuildingCount} building row(s); no master created",
                    appraisalId, attachMaster.Id, attachMaster.CollateralType, attachBuildings.Count);
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
                // Dedup key is Province + District + SubDistrict + TitleNumber (four columns since
                // 2026-08-09). TitleType, SurveyNumber, LandParcelNumber, Rawang and LandOffice are
                // descriptive, not key fields — a property is no longer skipped for lacking any of them.
                var land = p.LandIdentity;
                if (land is null || !land.Titles.Any(t => !string.IsNullOrWhiteSpace(t.TitleNumber)))
                    missing.Add("TitleNumber");
                if (string.IsNullOrWhiteSpace(land?.Province)) missing.Add("Province");
                if (string.IsNullOrWhiteSpace(land?.District)) missing.Add("District");
                if (string.IsNullOrWhiteSpace(land?.SubDistrict)) missing.Add("SubDistrict");
                break;
            }
            case "U":
            {
                // Dedup key is CondoRegistrationNumber + BuildingNumber + FloorNumber + RoomNumber
                // + Province + District + SubDistrict. LandOffice/TitleNumber/TitleType are not key
                // fields. Unlike Land, every key field here is still required, which is why the
                // corresponding CondoDetails columns can stay NOT NULL.
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
            // Leasehold (LSL / LSB / LS / LSU) is deliberately absent from this gate. Its fields are
            // checked in UpsertLeaseholdAsync instead, which skips the single property with a warning
            // rather than throwing: everything thrown here dead-letters the WHOLE appraisal, so one
            // half-filled lease contract also cost the land and machinery in the same appraisal their
            // masters. See MissingLeaseContractFields.
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
    // ── Dedup-key fallback: reuse the master of the previous appraisal in the chain ──────────
    //
    // Master resolution order:
    //   1. by the collateral's dedup key (physical attributes) — primary
    //   2. if that misses AND the appraisal has a PrevAppraisalId → the previous appraisal's master
    //   3. if both miss → create a new master
    //
    // Why step 2 exists: the dedup key must match character for character, so the smallest drift in
    // a title number or sub-district (a stray space, a typo, a later correction) turns a reappraisal
    // of the same property into a brand-new master. That breaks history, or worse raises the
    // cross-group ConflictException, which dead-letters without retry and has no merge tool to fix.
    //
    // Step 2 does NOT look at AppraisalType. It used to admit only ReAppraisal and Progressive, on
    // the reasoning that those are the same property by definition — but that also shut out every
    // other appraisal carrying a PrevAppraisalId (New follow-ups, appeals), which are just as often
    // the same collateral, and they silently lost their history. Business decision: a
    // PrevAppraisalId is evidence enough.
    //
    // What that costs, stated plainly: this fallback runs only AFTER the dedup key has already
    // missed, and a miss has two readings — "the identifying data drifted" (same collateral, worth
    // recovering) and "this is a different collateral" (must not bind). PrevAppraisalId cannot tell
    // them apart; it is true either way.
    //
    // Land used to carry a second gate on top: LandLocationMatches, which refused the fallback unless
    // the DEED's province + district + sub-district matched exactly. That is gone (2026-08-18), on
    // the principle that PrevAppraisalId is something the USER asserted and the system should not
    // overrule it. The deed address is not solid enough to overrule anything: it follows the LAND
    // OFFICE's division of the country, which is re-cut over time and carries its former name in
    // brackets (ลาดกระบัง(แสนแสบ)) — so the same parcel legitimately reads differently across two
    // appraisals a few years apart.
    //
    // Measured on U3 before removing it: 1,475 chains were being split into two masters by this gate.
    // 1,135 of those pairs sat within 200 m of each other and 288 more had no coordinates at all;
    // only 63 (4.3%) were in different provinces or more than 5 km apart. The pairs whose TITLE
    // NUMBERS differed — the ones that look most like separate parcels — were 96% within 200 m, i.e.
    // re-issued deeds and typos, not different land.
    //
    // The 63 are accepted as the price of the 1,412. A wrong bind is permanent (there is no merge or
    // split tool), so every fallback is logged at Warning with both sides' province and coordinates,
    // which is what makes that tail findable afterwards.
    //
    // What still guards this path: alreadyClaimed (two groups cannot take the same master),
    // compatibleTypes (no binding across collateral families) and the IsMaster test.

    /// <summary>
    /// Resolves the master from the appraisal's ancestor chain, used when the dedup key misses.
    /// Walks <see cref="AppraisalForCollateralResult.AncestorAppraisalIds"/> nearest-first and returns
    /// the first ancestor's master that passes every guard; null when none does.
    /// </summary>
    /// <remarks>
    /// The walk goes past the immediate PrevAppraisalId on purpose. Construction-inspection appraisals
    /// that recorded only a building — or no property at all — own no master, and they come in runs, so
    /// the nearest ancestor that owns one sits several hops up (34 at the worst on the U3 dataset).
    /// Looking at PrevAppraisalId alone resolved 46 of the 93 part-built chain tips; walking resolves
    /// all 93. This is a SEARCH for an existing master and never creates one.
    /// </remarks>
    /// <param name="alreadyClaimed">
    /// Masters already used while processing this appraisal — prevents two property groups being
    /// handed the same master, where the second would overwrite the first group's data.
    /// </param>
    private async Task<CollateralMaster?> FindMasterViaPreviousAppraisalAsync(
        AppraisalForCollateralResult appraisal,
        string[] compatibleTypes,
        string context,
        IReadOnlyCollection<Guid> alreadyClaimed,
        CancellationToken ct)
    {
        // Deliberately no AppraisalType test — see the block comment above this method.
        // Nearest-first; the query that builds this list carries its own Path-based cycle guard.
        for (int hop = 0; hop < appraisal.AncestorAppraisalIds.Count; hop++)
        {
            var ancestorId = appraisal.AncestorAppraisalIds[hop];
            var master = await repo.FindMasterByAppraisalIdAsync(ancestorId, ct);
            if (master is null)
                continue;   // that ancestor owns no master — keep walking up

            // Aliases cannot own engagements anyway (AppendEngagement guards it) — belt and braces.
            if (!master.IsMaster)
                continue;

            // Prevent two property groups from being handed the same master.
            if (alreadyClaimed.Contains(master.Id))
            {
                logger.LogWarning(
                    "{Context}: master {MasterId} from ancestor appraisal {AncestorId} was already "
                    + "claimed by another property group in the same appraisal; not reusing it "
                    + "(AppraisalId={AppraisalId})",
                    context, master.Id, ancestorId, appraisal.AppraisalId);
                return null;
            }

            // Guard against binding across types, e.g. the ancestor was a condo, this is land.
            // Stop rather than keep walking: the nearest master IS this chain's collateral, so an
            // incompatible one means the chain link itself is wrong, not that we looked too low.
            if (!compatibleTypes.Contains(master.CollateralType, StringComparer.Ordinal))
            {
                logger.LogWarning(
                    "{Context}: ancestor appraisal {AncestorId} is bound to master {MasterId} of type "
                    + "{ActualType}, which is incompatible with the type being upserted ({ExpectedTypes}); "
                    + "not using it as a fallback (AppraisalId={AppraisalId})",
                    context, ancestorId, master.Id, master.CollateralType,
                    string.Join("/", compatibleTypes), appraisal.AppraisalId);
                return null;
            }

            // Warning level on purpose: it means the two appraisals' identifying data disagree, which
            // someone should investigate and correct at source.
            logger.LogWarning(
                "{Context}: dedup key found no master, falling back to master {MasterId} from ancestor "
                + "appraisal {AncestorId} ({Hops} hop(s) up) (AppraisalId={AppraisalId}) — the two "
                + "appraisals' identifying data likely disagree; investigate and fix at source",
                context, master.Id, ancestorId, hop + 1, appraisal.AppraisalId);

            return master;
        }

        // Never silent: an exhausted walk is the difference between "no history" and "history we
        // failed to find", and only the log can tell them apart afterwards.
        if (appraisal.AncestorAppraisalIds.Count > 0)
            logger.LogWarning(
                "{Context}: walked all {Count} ancestor appraisal(s) and none owns a usable master "
                + "(AppraisalId={AppraisalId}); no fallback available",
                context, appraisal.AncestorAppraisalIds.Count, appraisal.AppraisalId);

        return null;
    }

    private async Task<(CollateralMaster IsMaster, List<CollateralMaster> Aliases, string CollateralType)> UpsertLandGroupAsync(
        IReadOnlyList<AppraisalPropertyForCollateral> landPropertiesInGroup,
        AppraisalForCollateralResult appraisal,
        List<AppraisalPropertyForCollateral> allBuildingProperties,
        IReadOnlyCollection<Guid> claimedMasterIds,
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
                land.Province!, land.District!, land.SubDistrict!, title.TitleNumber, ct);

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
                {
                    // Orphaned alias: its parent is soft-deleted (FindByIdWithEngagementsAsync filters
                    // on IsDeleted) or was removed outright. Historically this threw, which dead-lettered
                    // AppraisalCompletedConsumer for the whole appraisal — one stale row taking down an
                    // unrelated valuation. Skip the title instead: it then falls through to the chain
                    // fallback or mints a fresh master, and the warning names the row to clean up.
                    logger.LogWarning(
                        "[CollateralUpsert] Title {TitleNumber} matched alias {AliasId} whose "
                        + "ParentMasterId={ParentMasterId} is missing or soft-deleted. Skipping the alias "
                        + "and treating the title as unmatched.",
                        title.TitleNumber, hit.Id, hit.ParentMasterId);
                    continue;
                }
                masterRow = parent;
            }

            matchedMasterIds.Add(masterRow.Id);
            resolvedMasters[masterRow.Id.ToString()] = masterRow;
        }

        // Fallback: no title matched a dedup key, but for a reappraisal we can reuse the previous
        // appraisal's master. Feeding it into matchedMasterIds lets it flow through the normal
        // "exactly one group matched" branch below, which creates aliases for the incoming titles.
        if (matchedMasterIds.Count == 0)
        {
            var viaChain = await FindMasterViaPreviousAppraisalAsync(
                appraisal,
                [CollateralTypes.Land, CollateralTypes.LandWithBuilding],
                nameof(UpsertLandGroupAsync),
                claimedMasterIds,
                ct);

            // There is deliberately NO location check here any more — see the block comment above
            // FindMasterViaPreviousAppraisalAsync. The deed address follows the land office's own
            // division of the country, which is re-cut over time, so the same parcel reads
            // differently across two appraisals and the check was splitting 1,475 chains on U3.
            //
            // Trace instead of refuse: log both sides' location and coordinates so a bind that later
            // turns out to be wrong can be found. Warning level because the two appraisals' data
            // genuinely disagree and someone should correct it at source.
            if (viaChain is not null)
            {
                var d = viaChain.LandDetail;
                logger.LogWarning(
                    "UpsertLandGroupAsync: binding to master {MasterId} from the appraisal chain even "
                    + "though the deed location differs (was {OldLoc} @ {OldLat},{OldLon} / now "
                    + "{NewLoc} @ {NewLat},{NewLon}) — PrevAppraisalId is the user's own assertion, so "
                    + "it wins. Verify if these two look like different parcels "
                    + "(AppraisalId={AppraisalId})",
                    viaChain.Id,
                    $"{d?.Province}/{d?.District}/{d?.SubDistrict}",
                    d?.Coordinates?.Latitude, d?.Coordinates?.Longitude,
                    $"{land.Province}/{land.District}/{land.SubDistrict}", land.Latitude, land.Longitude,
                    appraisal.AppraisalId);

                matchedMasterIds.Add(viaChain.Id);
                resolvedMasters[viaChain.Id.ToString()] = viaChain;
            }
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
                landOfficeCode: firstLand.LandOffice,
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
                    landOfficeCode: lpLand.LandOffice,
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
            // LAND aliases only. FindAliasesByParentMasterIdAsync returns every alias of this
            // master, and when land is the primary group a condo / machine / leasehold group is
            // ALSO an alias of it. Those typed aliases must not flow into newAliases: it drives
            // UpdateCollateralType below, which would stamp the land type onto a machine alias.
            // The row then no longer matches FindMachineForUpsert's CollateralType == "MAC" filter,
            // so the next run mints a second machine master and trips
            // UX_MachineDetails_RegistrationNo_Active. LandDetail is Included by the repository, so
            // its presence is what distinguishes a land alias from a typed one.
            var existingAliases = (await repo.FindAliasesByParentMasterIdAsync(master.Id, ct))
                .Where(a => a.LandDetail is not null)
                .ToList();
            var existingTitleKeys = BuildExistingGroupTitleKeys(master, existingAliases);

            foreach (var (lp, t) in allTitlesWithOwner)
            {
                var lpLand = lp.LandIdentity!;
                var tKey = BuildTitleKey(lpLand.Province!, lpLand.District!, lpLand.SubDistrict!, t.TitleNumber);
                if (!existingTitleKeys.Contains(tKey))
                {
                    // New title not yet in this group — create alias
                    var alias = CollateralMaster.CreateLandAlias(
                        parentMasterId: master.Id,
                        landOfficeCode: lpLand.LandOffice,
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
        // Step 4: Update IsMaster with the last-known descriptive fields.
        //
        // Money and construction status are NOT written here any more — they live on the
        // engagement, frozen per appraisal. UnitPrice is not stored at all: nothing read it, and
        // the snapshot takes it straight from the appraisal contract, so there is also nothing to
        // propagate onto alias rows (the old Step 5).
        // -----------------------------------------------------------------------
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
            AppraisalDate: appraisal.AppraisalDate ?? dateTimeProvider.ApplicationNow
        );

        master.UpsertFromLandAppraisal(upsertData);

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
        IReadOnlyCollection<Guid> claimedMasterIds,
        CancellationToken ct)
    {
        var condo = p.CondoIdentity!;

        var master = await repo.FindCondoByDedupKey(
            condo.CondoRegistrationNumber!, condo.BuildingNumber!,
            condo.FloorNumber!, condo.RoomNumber!,
            condo.Province!, condo.District!, condo.SubDistrict!, ct);

        // Fallback when the dedup key misses — see FindMasterViaPreviousAppraisalAsync.
        master ??= await FindMasterViaPreviousAppraisalAsync(
            appraisal, [CollateralTypes.Condo], nameof(UpsertCondoAsync), claimedMasterIds, ct);

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
            AppraisalDate: appraisal.AppraisalDate ?? dateTimeProvider.ApplicationNow
        );

        master.UpsertFromCondoAppraisal(upsertData);
        return master;
    }

    private async Task<CollateralMaster> UpsertMachineAsync(
        AppraisalPropertyForCollateral p,
        AppraisalForCollateralResult appraisal,
        bool isPrimary,
        Guid? primaryMasterId,
        IReadOnlyCollection<Guid> claimedMasterIds,
        CancellationToken ct)
    {
        var m = p.MachineryIdentity!;

        var master = await repo.FindMachineForUpsert(
            m.RegistrationNumber, m.SerialNo, m.Brand, m.Model, m.Manufacturer, ct);


        // Fallback when the dedup key misses — see FindMasterViaPreviousAppraisalAsync.
        master ??= await FindMasterViaPreviousAppraisalAsync(
            appraisal, [CollateralTypes.Machine], nameof(UpsertMachineAsync), claimedMasterIds, ct);

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
            AppraisalDate: appraisal.AppraisalDate ?? dateTimeProvider.ApplicationNow,
            LifeYear: m.LifeYear
        );

        master.UpsertFromMachineAppraisal(upsertData);
        return master;
    }

    /// <summary>
    /// A land identity usable as a dedup key: every one of the four key columns
    /// (Province + District + SubDistrict + TitleNumber) is present. The corresponding
    /// collateral.LandDetails columns are NOT NULL, so a partial key cannot mint a master — it must
    /// fall through to the next source rather than blow up inside SaveChangesAsync.
    /// </summary>
    private static LandTitleForCollateral? FirstUsableTitle(LandIdentityForCollateral? land)
    {
        if (land is null
            || string.IsNullOrWhiteSpace(land.Province)
            || string.IsNullOrWhiteSpace(land.District)
            || string.IsNullOrWhiteSpace(land.SubDistrict))
            return null;

        return land.Titles.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.TitleNumber));
    }

    /// <summary>Same idea as <see cref="FirstUsableTitle"/> for the seven-column condo dedup key.</summary>
    private static bool IsUsableCondoKey(CondoIdentityForCollateral? condo) =>
        condo is not null
        && !string.IsNullOrWhiteSpace(condo.CondoRegistrationNumber)
        && !string.IsNullOrWhiteSpace(condo.BuildingNumber)
        && !string.IsNullOrWhiteSpace(condo.FloorNumber)
        && !string.IsNullOrWhiteSpace(condo.RoomNumber)
        && !string.IsNullOrWhiteSpace(condo.Province)
        && !string.IsNullOrWhiteSpace(condo.District)
        && !string.IsNullOrWhiteSpace(condo.SubDistrict);

    /// <summary>
    /// Resolves the land / condo master that a leasehold hangs off (LeaseholdDetail.UnderlyingMasterId).
    ///
    /// The leasehold property carries its OWN land or condo detail — PropertyType.HasLandDetail
    /// covers LSL / LS and HasCondoDetail covers LSU — because the UI puts the deed fields and the
    /// lease-contract fields on the same property. Users therefore never key a separate Land row,
    /// which is why scanning siblings alone used to fail with "no Land or Condo sibling found".
    ///
    /// Order: the property's own land AND condo detail first (it describes the very thing being
    /// leased — an LSU in an appraisal that also holds land must hang off its condo, not off the
    /// neighbouring parcel), then Land/LB and Condo siblings, then any other leasehold in the
    /// appraisal. That last step is what gives LSB an underlying, since a building-only property
    /// has no address of its own at all.
    /// Returns null when nothing yielded a complete dedup key.
    /// </summary>
    private async Task<CollateralMaster?> ResolveUnderlyingMasterAsync(
        AppraisalPropertyForCollateral p,
        AppraisalForCollateralResult appraisal,
        List<AppraisalPropertyForCollateral> landProperties,
        List<AppraisalPropertyForCollateral> condoProperties,
        List<AppraisalPropertyForCollateral> leaseholdProperties,
        Dictionary<Guid, CollateralMaster> landMasterByPropertyId,
        IReadOnlyList<CollateralMaster> pass1LandRows,
        CancellationToken ct)
    {
        // The property's OWN detail comes before every sibling, and land / condo are tried on the
        // property itself before falling back: an LSU sitting in an appraisal that also holds land
        // must hang off ITS condo, not off the neighbouring parcel.
        var resolved = await TryResolveFromLandAsync(p) ?? await TryResolveFromCondoAsync(p);
        if (resolved is not null)
            return resolved;

        // Siblings next — the classic shape, one Land/LB (or Condo) property next to the leasehold.
        foreach (var sibling in landProperties)
        {
            resolved = await TryResolveFromLandAsync(sibling);
            if (resolved is not null)
                return resolved;
        }

        foreach (var sibling in condoProperties)
        {
            resolved = await TryResolveFromCondoAsync(sibling);
            if (resolved is not null)
                return resolved;
        }

        // Last resort: another leasehold in the same appraisal. This is the only route open to LSB,
        // whose BuildingAppraisalDetail carries no address of its own at all.
        foreach (var other in leaseholdProperties.Where(x => x.PropertyId != p.PropertyId))
        {
            resolved = await TryResolveFromLandAsync(other) ?? await TryResolveFromCondoAsync(other);
            if (resolved is not null)
                return resolved;
        }

        return null;

        async Task<CollateralMaster?> TryResolveFromLandAsync(AppraisalPropertyForCollateral candidate)
        {
            if (FirstUsableTitle(candidate.LandIdentity) is not { } title)
                return null;

            var landId = candidate.LandIdentity!;

            // Pass 1 already built the master for a Land/LB sibling — reuse that instance so the
            // leasehold points at the same row rather than racing a second insert on the dedup key.
            if (landMasterByPropertyId.TryGetValue(candidate.PropertyId, out var fromPass1))
                return fromPass1;

            // Same guard by dedup key rather than by property: the leasehold's OWN title is usually
            // the very parcel a Land/LB sibling already claimed in pass 1. Those rows are still
            // Added-but-unsaved, and EF Core queries do not see them — going straight to the DB would
            // miss them and mint a duplicate that trips UX_LandDetails_DedupKey_Active on save.
            var inMemoryHit = pass1LandRows.FirstOrDefault(m =>
                m.LandDetail is { } d
                && string.Equals(d.Province, landId.Province, StringComparison.OrdinalIgnoreCase)
                && string.Equals(d.District, landId.District, StringComparison.OrdinalIgnoreCase)
                && string.Equals(d.SubDistrict, landId.SubDistrict, StringComparison.OrdinalIgnoreCase)
                && string.Equals(d.TitleNumber, title.TitleNumber, StringComparison.OrdinalIgnoreCase));

            if (inMemoryHit is not null)
            {
                // An alias can never be an underlying — hand back the group's IsMaster. Pass 1
                // collapses every land title in the appraisal under a single master, so it is in
                // this same list.
                if (inMemoryHit.IsMaster)
                    return inMemoryHit;

                var parent = pass1LandRows.FirstOrDefault(m => m.Id == inMemoryHit.ParentMasterId)
                             ?? await repo.FindByIdWithEngagementsAsync(inMemoryHit.ParentMasterId!.Value, ct);
                if (parent is not null)
                    return parent;
            }

            // The RE row mirrors what the leasehold property actually describes: a leasehold that
            // covers a building (LS / LSB) sits on land-with-building. Same rule as
            // UpsertLandGroupAsync's resolvedCollateralType, just sourced per property.
            var reCollateralType = candidate.BuildingIdentity is not null
                ? CollateralTypes.LandWithBuilding
                : CollateralTypes.Land;

            var landHit = await repo.FindLandByDedupKeyIncludingAliases(
                landId.Province!, landId.District!, landId.SubDistrict!, title.TitleNumber, ct);

            if (landHit is not null)
            {
                // An alias is never a valid underlying: resolve up to its IsMaster. A missing parent
                // (soft-deleted) falls through to minting a fresh master below.
                var hit = landHit.IsMaster
                    ? landHit
                    : await repo.FindByIdWithEngagementsAsync(landHit.ParentMasterId!.Value, ct);

                if (hit is not null)
                {
                    // Never downgrade LB → L: the parcel may carry a building this appraisal does not
                    // describe. Only the upgrade direction is evidence.
                    if (reCollateralType == CollateralTypes.LandWithBuilding)
                        hit.UpdateCollateralType(reCollateralType);

                    UpsertLandDetailFromCandidate(hit, landId, appraisal);
                    return hit;
                }
            }

            var created = CollateralMaster.CreateLand(
                ownerName: landId.OwnerName ?? string.Empty,
                landOfficeCode: landId.LandOffice,
                province: landId.Province!,
                district: landId.District!,
                subDistrict: landId.SubDistrict!,
                titleType: title.TitleType,
                titleNumber: title.TitleNumber,
                surveyNumber: title.SurveyNumber,
                landParcelNumber: title.LandParcelNumber,
                rawang: title.Rawang,
                street: landId.Street,
                village: landId.Village,
                latitude: landId.Latitude,
                longitude: landId.Longitude,
                collateralType: reCollateralType);
            repo.Add(created);
            UpsertLandDetailFromCandidate(created, landId, appraisal);

            logger.LogInformation(
                "ResolveUnderlyingMasterAsync: created underlying {Type} master {MasterId} from PropertyId="
                + "{SourcePropertyId} for leasehold PropertyId={PropertyId} (AppraisalId={AppraisalId})",
                reCollateralType, created.Id, candidate.PropertyId, p.PropertyId, appraisal.AppraisalId);

            return created;
        }

        async Task<CollateralMaster?> TryResolveFromCondoAsync(AppraisalPropertyForCollateral candidate)
        {
            if (!IsUsableCondoKey(candidate.CondoIdentity))
                return null;

            var condoId = candidate.CondoIdentity!;

            var condoHit = await repo.FindCondoByDedupKey(
                condoId.CondoRegistrationNumber!, condoId.BuildingNumber!,
                condoId.FloorNumber!, condoId.RoomNumber!,
                condoId.Province!, condoId.District!, condoId.SubDistrict!, ct);

            if (condoHit is not null)
            {
                UpsertCondoDetailFromCandidate(condoHit, condoId, appraisal);
                return condoHit;
            }

            var created = CollateralMaster.CreateCondo(
                ownerName: condoId.OwnerName ?? string.Empty,
                landOfficeCode: condoId.LandOffice,
                condoRegistrationNumber: condoId.CondoRegistrationNumber!,
                buildingNumber: condoId.BuildingNumber!,
                floorNumber: condoId.FloorNumber!,
                roomNumber: condoId.RoomNumber!,
                province: condoId.Province!,
                district: condoId.District!,
                subDistrict: condoId.SubDistrict!,
                condoName: condoId.CondoName);
            repo.Add(created);
            UpsertCondoDetailFromCandidate(created, condoId, appraisal);

            logger.LogInformation(
                "ResolveUnderlyingMasterAsync: created underlying condo master {MasterId} from PropertyId="
                + "{SourcePropertyId} for leasehold PropertyId={PropertyId} (AppraisalId={AppraisalId})",
                created.Id, candidate.PropertyId, p.PropertyId, appraisal.AppraisalId);

            return created;
        }
    }

    /// <summary>
    /// Fills the underlying RE row with the same last-known data a freehold appraisal of that parcel
    /// would write — owner, area, road and zoning context, coordinates. Without this the row exists
    /// with nothing but its dedup key, so the leasehold's RE half carries no area and no owner.
    ///
    /// <see cref="CollateralMaster.UpsertFromLandAppraisal"/> touches last-known fields only; it does
    /// NOT append an engagement, which is what keeps this row out of the AS400 and regulatory
    /// exports (both start from CollateralEngagements). It also rejects alias rows, hence the guard.
    /// </summary>
    private void UpsertLandDetailFromCandidate(
        CollateralMaster master,
        LandIdentityForCollateral land,
        AppraisalForCollateralResult appraisal)
    {
        if (!master.IsMaster || master.LandDetail is null)
            return;

        master.UpsertFromLandAppraisal(new LandUpsertData(
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
            AppraisalDate: appraisal.AppraisalDate ?? dateTimeProvider.ApplicationNow));
    }

    /// <summary>Condo counterpart of <see cref="UpsertLandDetailFromCandidate"/>.</summary>
    private void UpsertCondoDetailFromCandidate(
        CollateralMaster master,
        CondoIdentityForCollateral condo,
        AppraisalForCollateralResult appraisal)
    {
        if (!master.IsMaster || master.CondoDetail is null)
            return;

        master.UpsertFromCondoAppraisal(new CondoUpsertData(
            OwnerName: condo.OwnerName,
            CondoName: condo.CondoName,
            UsableArea: condo.UsableArea,
            LocationType: condo.LocationType,
            BuildingAge: condo.BuildingAge,
            ConstructionYear: condo.ConstructionYear,
            ModelName: condo.ModelName,
            Latitude: condo.Latitude,
            Longitude: condo.Longitude,
            AppraisalId: appraisal.AppraisalId,
            AppraisalNumber: appraisal.AppraisalNumber ?? string.Empty,
            AppraisalDate: appraisal.AppraisalDate ?? dateTimeProvider.ApplicationNow));
    }

    /// <summary>
    /// The lease-contract fields that make up the leasehold dedup key and are NOT NULL on
    /// collateral.LeaseholdDetails. Returns the ones this property is missing.
    /// </summary>
    private static List<string> MissingLeaseContractFields(LeaseholdIdentityForCollateral? lh)
    {
        var missing = new List<string>();
        if (lh is null)
        {
            missing.Add("LeaseAgreementDetail");
            return missing;
        }

        if (string.IsNullOrWhiteSpace(lh.ContractNo)) missing.Add("ContractNo");
        if (string.IsNullOrWhiteSpace(lh.LessorName)) missing.Add("Lessor");
        if (string.IsNullOrWhiteSpace(lh.LesseeName)) missing.Add("Lessee");
        if (lh.LeaseStartDate is null) missing.Add("LeaseTermStart");
        return missing;
    }

    /// <summary>
    /// Upserts the leasehold master for one leasehold property.
    /// Returns null when the lease contract is incomplete, or when no underlying land / condo master
    /// could be resolved (see <see cref="ResolveUnderlyingMasterAsync"/>). The caller skips the group
    /// in that case; nothing here throws, because throwing dead-letters the whole appraisal.
    /// </summary>
    private async Task<CollateralMaster?> UpsertLeaseholdAsync(
        AppraisalPropertyForCollateral p,
        AppraisalForCollateralResult appraisal,
        List<AppraisalPropertyForCollateral> landProperties,
        List<AppraisalPropertyForCollateral> condoProperties,
        List<AppraisalPropertyForCollateral> leaseholdProperties,
        Dictionary<Guid, CollateralMaster> landMasterByPropertyId,
        IReadOnlyList<CollateralMaster> pass1LandRows,
        bool isPrimary,
        Guid? primaryMasterId,
        IReadOnlyCollection<Guid> claimedMasterIds,
        CancellationToken ct)
    {
        var missingContractFields = MissingLeaseContractFields(p.LeaseholdIdentity);
        if (missingContractFields.Count > 0)
        {
            logger.LogWarning(
                "UpsertLeaseholdAsync: PropertyId={PropertyId} (type={PropertyType}) in "
                + "AppraisalId={AppraisalId} is missing lease contract fields: {MissingFields}. The "
                + "leasehold is skipped and gets no CollateralMaster; the rest of the appraisal still "
                + "processes. Fix the appraisal data and replay.",
                p.PropertyId, p.PropertyTypeCode, appraisal.AppraisalId,
                string.Join(", ", missingContractFields));
            return null;
        }

        var lh = p.LeaseholdIdentity!;
        var leaseTermStart = DateOnly.FromDateTime(lh.LeaseStartDate!.Value);

        // ---- Resolve or create the underlying master ----
        var underlyingMaster = await ResolveUnderlyingMasterAsync(
            p, appraisal, landProperties, condoProperties, leaseholdProperties,
            landMasterByPropertyId, pass1LandRows, ct);

        if (underlyingMaster is null)
        {
            // Every source came up empty (or held an incomplete dedup key). Historically this threw
            // MissingIdentityKeyException, which dead-lettered the AppraisalCompleted message for the
            // WHOLE appraisal — the land and machinery of unrelated groups lost their masters too.
            // Skip just this property instead; the warning names the row to fix at source.
            logger.LogWarning(
                "UpsertLeaseholdAsync: no underlying land/condo could be resolved for PropertyId={PropertyId} "
                + "(type={PropertyType}) in AppraisalId={AppraisalId} — neither the property's own land/condo "
                + "detail, nor a sibling property, carried a complete dedup key. The leasehold is skipped and "
                + "gets no CollateralMaster; fix the appraisal data and replay.",
                p.PropertyId, p.PropertyTypeCode, appraisal.AppraisalId);
            return null;
        }

        // ---- Find or create the leasehold master itself ----
        var leaseMaster = await repo.FindLeaseholdByDedupKey(
            lh.ContractNo!, underlyingMaster.Id, lh.LessorName!, lh.LesseeName!, leaseTermStart, ct);

        // Fallback when the dedup key misses — see FindMasterViaPreviousAppraisalAsync.
        // The leasehold dedup key already contains free-text lessor/lessee names, so it drifts more
        // easily than the other types.
        leaseMaster ??= await FindMasterViaPreviousAppraisalAsync(
            appraisal,
            CollateralTypes.LeaseholdFamily,
            nameof(UpsertLeaseholdAsync),
            claimedMasterIds,
            ct);

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
            AppraisalDate: appraisal.AppraisalDate ?? dateTimeProvider.ApplicationNow
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
                    // Straight from the contract, not via the master row we just wrote. The
                    // round-trip added nothing and forced AppendEngagement to run after the detail
                    // update; reading the source also avoids the alias staleness fixed below.
                    var isMasterUnitPrice = primaryLandProp.PricingInfo?.UnitPrice;

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
                    // From the contract, NOT aliasRow.LandDetail.UnitPrice. The alias copy is only
                    // written when the incoming value is non-null (see the propagation block below),
                    // while the IsMaster row is overwritten unconditionally — so an appraisal that
                    // moved off the cost approach left the alias holding the previous round's rate,
                    // and that stale rate was then baked into THIS appraisal's snapshot.
                    var aliasUnitPrice = aliasProp.PricingInfo?.UnitPrice;

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
                            unitPrice: prop.PricingInfo?.UnitPrice));
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

            // Group-level values straight from the appraisal contract. These used to be read back
            // off the master detail rows we had just written; those columns are gone now, and the
            // group's own PricingInfo is the source they were populated from anyway.
            var groupPricing = group.Properties
                .FirstOrDefault(p => p.PricingInfo is not null)?.PricingInfo;
            decimal? buildingCost = groupPricing?.BuildingValue;
            decimal? groupAppraisalValue = appraisal.AppraisedValue ?? groupPricing?.AppraisalValue;

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
            keys.Add(BuildTitleKey(ld.Province, ld.District, ld.SubDistrict, ld.TitleNumber));
        foreach (var a in aliases)
        {
            if (a.LandDetail is { } ald)
                keys.Add(BuildTitleKey(ald.Province, ald.District, ald.SubDistrict, ald.TitleNumber));
        }
        return keys;
    }

    // In-memory dedup key — MUST mirror the DB dedup key / UX_LandDetails_DedupKey_Active
    // (Province + District + SubDistrict + TitleNumber). LandOfficeCode is NOT part of the key, and
    // neither are TitleType / SurveyNumber / LandParcelNumber / Rawang since the 2026-08-09 narrowing —
    // see CollateralMasterRepository.LandKeyMatches.
    private static string BuildTitleKey(
        string province, string amphur, string tambon, string titleNo)
        => $"{province}|{amphur}|{tambon}|{titleNo}";

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
                    sellingPrice: dto.SellingPrice,
                    unitNumber: dto.UnitNumber)
                : ProjectUnit.CreateLandAndBuilding(
                    collateralMasterId: collateralMasterId,
                    sequenceNumber: dto.SequenceNumber,
                    plotNumber: dto.PlotNumber,
                    houseNumber: dto.HouseNumber,
                    modelType: dto.ModelType,
                    numberOfFloors: dto.NumberOfFloors,
                    landArea: dto.LandArea,
                    usableArea: dto.UsableArea,
                    sellingPrice: dto.SellingPrice,
                    unitNumber: dto.UnitNumber);

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

    /// <summary>
    /// Copies each existing unit's <c>HostCollateralId</c> onto the matching incoming unit, so the
    /// AS400 collateral ids survive <c>ProjectDetail.ReplaceUnits</c>.
    ///
    /// A unit is "the same unit" when its sequence number and its identity field agree — RoomNumber
    /// for condo projects, PlotNumber for land-and-building ones. That is the same identity AS400 uses
    /// to ask for a single unit's value (see <c>GetAppraisalResultQueryHandler.ResolveBlockUnitAsync</c>),
    /// so the system has one definition of unit identity rather than two.
    ///
    /// An existing id with no match is dropped and logged: the unit it belonged to is no longer in the
    /// project, which either means the Excel upload disagrees with the previous one or a genuinely
    /// different unit now sits at that sequence. Both warrant a look rather than a silent guess.
    /// </summary>
    private void CarryOverHostCollateralIds(
        IReadOnlyList<CollateralMasters.Models.ProjectUnit>? existingUnits,
        IReadOnlyList<CollateralMasters.Models.ProjectUnit> incomingUnits,
        Guid masterId)
    {
        var result = CarryHostCollateralIds(existingUnits, incomingUnits);

        foreach (var dropped in result.Dropped)
            logger.LogWarning(
                "CarryOverHostCollateralIds: dropping HostCollateralId {HostCollateralId} on PRJ master {MasterId} — "
                + "no incoming unit matches Sequence={Sequence} Room={RoomNumber} Plot={PlotNumber}.",
                dropped.HostCollateralId, masterId, dropped.SequenceNumber, dropped.RoomNumber, dropped.PlotNumber);

        if (result.Carried > 0)
            logger.LogInformation(
                "CarryOverHostCollateralIds: carried {Carried} AS400 collateral id(s) on PRJ master {MasterId}, dropped {Dropped}.",
                result.Carried, masterId, result.Dropped.Count);
    }

    /// <summary>
    /// The matching itself, with no logging or EF involvement. Public so it can be unit-tested
    /// directly, following <c>HostCollateralLinkIngestor.PickWinningRecord</c>.
    /// Mutates the matched incoming units and reports the existing units left without a match.
    /// </summary>
    public static HostIdCarryOverResult CarryHostCollateralIds(
        IReadOnlyList<CollateralMasters.Models.ProjectUnit>? existingUnits,
        IReadOnlyList<CollateralMasters.Models.ProjectUnit> incomingUnits)
    {
        var linkedUnits = existingUnits?.Where(u => u.HostCollateralId is not null).ToList() ?? [];
        if (linkedUnits.Count == 0)
            return new HostIdCarryOverResult(0, []);

        var carried = 0;
        var dropped = new List<CollateralMasters.Models.ProjectUnit>();

        foreach (var existing in linkedUnits)
        {
            var match = incomingUnits.FirstOrDefault(incoming =>
                incoming.SequenceNumber == existing.SequenceNumber &&
                SameUnitIdentity(existing, incoming));

            if (match is null)
            {
                dropped.Add(existing);
                continue;
            }

            match.SetHostCollateralId(existing.HostCollateralId);
            carried++;
        }

        return new HostIdCarryOverResult(carried, dropped);
    }

    /// <summary>
    /// True when both units name the same physical unit. Absent identity fields compare equal, which
    /// makes the sequence number the sole key for projects whose upload carried neither room nor plot.
    /// </summary>
    private static bool SameUnitIdentity(
        CollateralMasters.Models.ProjectUnit left,
        CollateralMasters.Models.ProjectUnit right)
        => IdentityEquals(left.RoomNumber, right.RoomNumber)
           && IdentityEquals(left.PlotNumber, right.PlotNumber);

    private static bool IdentityEquals(string? left, string? right)
        => string.Equals(
            (left ?? string.Empty).Trim(),
            (right ?? string.Empty).Trim(),
            StringComparison.OrdinalIgnoreCase);

    private void AppendEngagement(
        CollateralMaster primaryMaster,
        AppraisalForCollateralResult appraisal,
        string snapshot,
        string? appraisedCollateralType = null,
        decimal? landAreaInSqWa = null,
        decimal? appraisalValue = null,
        // Cost-approach per-sq.wa rate straight from the appraisal contract (PricingInfo.UnitPrice).
        // Taken as a parameter rather than read back off primaryMaster.LandDetail so the engagement
        // does not depend on the master having been written first.
        decimal? unitPrice = null,
        // Cost-approach building value from the contract (PricingInfo.BuildingValue), for the same
        // reason as unitPrice: the master detail column it used to be read from no longer exists.
        decimal? buildingCost = null)
    {
        Guid? companyId = appraisal.CompanyId.HasValue() && Guid.TryParse(appraisal.CompanyId, out var parsedCompanyId)
            ? parsedCompanyId
            : (Guid?)null;

        // Freeze the cost-approach Land/Building split from the just-upserted primary master, so the
        // outbound Collateral Result interface never recomputes from later-overwritten master state.
        decimal? landValue = null;
        decimal? buildingValue = null;
        var ld = primaryMaster.LandDetail;
        if (ld is not null && unitPrice is not null
            && primaryMaster.CollateralType is CollateralTypes.Land or CollateralTypes.LandWithBuilding)
        {
            if (landAreaInSqWa is not null)
                landValue = unitPrice.Value * landAreaInSqWa.Value;
            // BuildingValue intentionally stays null for bare Land — only L&B carries a building cost.
            if (primaryMaster.CollateralType == CollateralTypes.LandWithBuilding)
                buildingValue = buildingCost;
        }

        primaryMaster.AppendEngagement(
            appraisalId: appraisal.AppraisalId,
            appraisalNumber: appraisal.AppraisalNumber ?? string.Empty,
            requestId: appraisal.RequestId,
            requestNumber: appraisal.RequestNumber ?? string.Empty,
            appraisalType: appraisal.AppraisalType,
            appraisalDate: appraisal.AppraisalDate ?? dateTimeProvider.ApplicationNow,
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
            appraisalCompanyCode: appraisal.CompanyCode,
            // Part-built value, already computed by the Appraisal module's
            // IConstructionCurrentValueService. NULL when nothing on the appraisal is under
            // construction — the regulatory export then falls back to the appraised value.
            currentValue: appraisal.CurrentValue,
            // Same breakdown as CurrentValue — value-weighted over every inspected building.
            isUnderConstruction: appraisal.IsUnderConstruction,
            constructionProgressPercent: appraisal.ConstructionProgressPercent);
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
    private async Task UpsertProjectAsync(
        AppraisalForCollateralResult appraisal, bool engagementExists, CancellationToken ct)
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
            master = await repo.FindProjectMasterByAppraisalIdAsync(appraisal.PrevAppraisalId.Value, ct);
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
        // clears + re-adds). FindProjectMasterByAppraisalIdAsync eagerly loads Units, so EF deletes the
        // orphaned old rows and inserts the new ones in the SAME SaveChanges — atomic for both first-appraisal
        // (empty collection) and reappraisal (full replace). No eager ExecuteDeleteAsync (would commit before
        // the insert, risking unit loss if the later save throws). ---

        // --- Carry AS400 collateral ids over the replace ---
        // Must run BEFORE UpsertFromProjectAppraisal: that call invokes ProjectDetail.ReplaceUnits, which
        // discards the old rows outright. The appraisal snapshot carries no host id, so without this every
        // reappraisal of the project would erase the ids AS400 issued for its financed units.
        CarryOverHostCollateralIds(master.ProjectDetail?.Units, collateralUnits, master.Id);

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
            AppraisalDate: appraisal.AppraisalDate ?? dateTimeProvider.ApplicationNow
        );

        master.UpsertFromProjectAppraisal(upsertData);

        // --- Single engagement per appraisal (UX_CollateralEngagements_Appraisal) ---
        // Skipped on replay: the project structure above is still refreshed, only the append stops.
        if (!engagementExists)
        {
            // Σ per-unit APPRAISED value (ProjectUnitPrices.TotalAppraisalValueRounded), not
            // ProjectSellingPrice. The engagement's AppraisalValue means the same thing for every
            // collateral type — what the appraiser valued it at — and the selling price is the
            // developer's list price, a different figure that happened to be wired here.
            // Observed on dev: one project shipped 29,650,000 (list) against 3,000,000 appraised,
            // another 24,400,000 against 55,154,250 — wrong in both directions.
            // NULL when no unit has been priced yet, matching every other type's "no pricing" case.
            var projectAppraisedValue = proj.Units.Any(u => u.AppraisedValue.HasValue)
                ? proj.Units.Sum(u => u.AppraisedValue ?? 0m)
                : (decimal?)null;

            AppendEngagement(
                master,
                appraisal,
                snapshot: structureJson,
                appraisedCollateralType: CollateralTypes.Project,
                landAreaInSqWa: null,
                appraisalValue: projectAppraisedValue);
        }
    }
}

/// <param name="Carried">Existing ids successfully copied onto an incoming unit.</param>
/// <param name="Dropped">
/// Units that held an id but matched nothing incoming — the unit has left the project, or a different
/// unit now occupies its sequence number. Reported rather than force-fitted onto a neighbour.
/// </param>
public record HostIdCarryOverResult(
    int Carried,
    IReadOnlyList<CollateralMasters.Models.ProjectUnit> Dropped);

file static class StringExtensions
{
    internal static bool HasValue(this string? s) =>
        !string.IsNullOrWhiteSpace(s);
}
