using Appraisal.Application.Features.Appraisals.GetAppraisalForCollateral;
using Collateral.CollateralMasters.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Collateral.CollateralMasters.Services;

/// <summary>
/// Implements the core write path: given a completed appraisal, finds or creates a
/// CollateralMaster for each in-scope property, upserts last-known data, and appends
/// an engagement row with a JSON snapshot. Idempotent via the unique (AppraisalId, PropertyId)
/// index on CollateralEngagements.
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
        // Pass 1: Land + Condo + Machine (no dependency on other masters)
        // -----------------------------------------------------------------------

        // Pass-1 cache for Leasehold's underlying-resolution. Keyed by source PropertyId so
        // same-appraisal Leasehold-over-newly-created-Land works without a DB query (the
        // master was added to EF tracker but not yet committed). PropertyId is stable and
        // matches what the Leasehold sibling resolution uses.
        var landMasterByPropertyId = new Dictionary<Guid, CollateralMaster>();

        foreach (var p in landOrLbProperties)
        {
            var master = await UpsertLandAsync(p, appraisal, buildingProperties, ct);
            landMasterByPropertyId[p.PropertyId] = master;
        }

        foreach (var p in condoProperties)
        {
            await UpsertCondoAsync(p, appraisal, ct);
        }

        foreach (var p in machineryProperties)
        {
            await UpsertMachineAsync(p, appraisal, ct);
        }

        // -----------------------------------------------------------------------
        // Pass 2: Leasehold (depends on underlying master already existing or created)
        // -----------------------------------------------------------------------
        foreach (var p in leaseholdProperties)
        {
            await UpsertLeaseholdAsync(p, appraisal, landOrLbProperties, condoProperties, landMasterByPropertyId, ct);
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
            // Idempotency: a concurrent consumer already inserted the engagement rows
            // for this (AppraisalId, PropertyId). Treat as success.
            logger.LogWarning(
                "ProcessAppraisalAsync: duplicate engagement detected for AppraisalId={AppraisalId} — treated as idempotent no-op",
                appraisalId);
        }
        catch (DbUpdateException dbEx) when (IsUniqueConstraintViolation(dbEx))
        {
            // Different unique-index violation (e.g. concurrent master/alias creation
            // colliding on LandDetails dedup key). This is NOT idempotent — the loser
            // lost its engagement and last-known updates. Surface for retry.
            var indexName = ExtractViolatedIndexName(dbEx);
            logger.LogError(dbEx,
                "ProcessAppraisalAsync: non-engagement unique-constraint violation for AppraisalId={AppraisalId}, Index={IndexName} — surfacing for retry",
                appraisalId, indexName ?? "<unknown>");
            throw;
        }
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
                    missing.Add("TitleDeedNo");
                if (land is null || !land.Titles.Any(t => !string.IsNullOrWhiteSpace(t.TitleType)))
                    missing.Add("TitleDeedType");
                if (string.IsNullOrWhiteSpace(land?.LandOffice)) missing.Add("LandOfficeCode");
                if (string.IsNullOrWhiteSpace(land?.Province)) missing.Add("Province");
                if (string.IsNullOrWhiteSpace(land?.District)) missing.Add("Amphur");
                if (string.IsNullOrWhiteSpace(land?.SubDistrict)) missing.Add("Tambon");
                break;
            }
            case "U":
            {
                var condo = p.CondoIdentity;
                if (string.IsNullOrWhiteSpace(condo?.LandOffice)) missing.Add("LandOfficeCode");
                if (string.IsNullOrWhiteSpace(condo?.CondoRegistrationNumber)) missing.Add("CondoRegistrationNumber");
                if (string.IsNullOrWhiteSpace(condo?.BuildingNumber)) missing.Add("BuildingNumber");
                if (string.IsNullOrWhiteSpace(condo?.FloorNumber)) missing.Add("FloorNumber");
                if (string.IsNullOrWhiteSpace(condo?.RoomNumber)) missing.Add("UnitNumber");
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

    private async Task<CollateralMaster> UpsertLandAsync(
        AppraisalPropertyForCollateral p,
        AppraisalForCollateralResult appraisal,
        List<AppraisalPropertyForCollateral> buildingProperties,
        CancellationToken ct)
    {
        var land = p.LandIdentity!;
        var allValidTitles = land.Titles
            .Where(t => !string.IsNullOrWhiteSpace(t.TitleNumber))
            .ToList();

        // -----------------------------------------------------------------------
        // Step 1: For each title, look up ANY existing row (master or alias).
        // Resolve hits to their IsMaster row and collect distinct master IDs.
        // -----------------------------------------------------------------------
        var matchedMasterIds = new HashSet<Guid>();
        // In-memory map: title key → resolved IsMaster row (tracked entities)
        var resolvedMasters = new Dictionary<string, CollateralMaster>();

        foreach (var t in allValidTitles)
        {
            var hit = await repo.FindLandByDedupKeyIncludingAliases(
                land.LandOffice!, land.Province!, land.District!, land.SubDistrict!,
                t.TitleType, t.TitleNumber, null, ct);

            if (hit is null) continue;

            // Navigate to IsMaster if alias
            CollateralMaster masterRow;
            if (hit.IsMaster)
            {
                masterRow = hit;
            }
            else
            {
                // hit is an alias — load its parent
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
        // -----------------------------------------------------------------------
        CollateralMaster master;

        if (matchedMasterIds.Count == 0)
        {
            // No existing group — create new IsMaster row with the FIRST title
            var firstTitle = allValidTitles.First();
            master = CollateralMaster.CreateLand(
                ownerName: string.Empty,
                landOfficeCode: land.LandOffice!,
                province: land.Province!,
                amphur: land.District!,
                tambon: land.SubDistrict!,
                titleDeedType: firstTitle.TitleType,
                titleDeedNo: firstTitle.TitleNumber,
                surveyOrParcelNo: null,
                street: null, village: null, postalCode: null,
                latitude: null, longitude: null);
            repo.Add(master);

            // Create alias rows for the remaining titles (skip the first one used for the master)
            foreach (var t in allValidTitles.Skip(1))
            {
                var alias = CollateralMaster.CreateLandAlias(
                    parentMasterId: master.Id,
                    landOfficeCode: land.LandOffice!,
                    province: land.Province!,
                    amphur: land.District!,
                    tambon: land.SubDistrict!,
                    titleDeedType: t.TitleType,
                    titleDeedNo: t.TitleNumber,
                    surveyOrParcelNo: null);
                repo.Add(alias);
            }
        }
        else if (matchedMasterIds.Count == 1)
        {
            // Existing group found — reuse master
            master = resolvedMasters[matchedMasterIds.First().ToString()];

            // Ensure all current appraisal titles have alias rows in this group
            var existingAliases = await repo.FindAliasesByParentMasterIdAsync(master.Id, ct);
            var existingTitleKeys = BuildExistingGroupTitleKeys(master, existingAliases);

            foreach (var t in allValidTitles)
            {
                var tKey = BuildTitleKey(land.LandOffice!, land.Province!, land.District!, land.SubDistrict!, t.TitleType, t.TitleNumber);
                if (!existingTitleKeys.Contains(tKey))
                {
                    // New title — create alias
                    var alias = CollateralMaster.CreateLandAlias(
                        parentMasterId: master.Id,
                        landOfficeCode: land.LandOffice!,
                        province: land.Province!,
                        amphur: land.District!,
                        tambon: land.SubDistrict!,
                        titleDeedType: t.TitleType,
                        titleDeedNo: t.TitleNumber,
                        surveyOrParcelNo: null);
                    repo.Add(alias);
                }
            }
        }
        else
        {
            // More than 1 distinct master matched → conflict — admin must resolve (v2)
            var idList = string.Join(", ", matchedMasterIds);
            throw new ConflictException(
                $"The titles in this appraisal span multiple existing CollateralMaster groups: [{idList}]. " +
                "Admin merge is required before this appraisal can be processed.");
        }

        // -----------------------------------------------------------------------
        // Step 3: Update IsMaster with last-known + construction + appraisal data
        // -----------------------------------------------------------------------
        // Buildings whose BuiltOnTitleNumber matches any of this property's titles
        var titleNumbers = allValidTitles.Select(t => t.TitleNumber).ToHashSet();
        var buildingsForThisLand = buildingProperties
            .Where(b => b.BuildingIdentity?.BuiltOnTitleNumber is { } btn && titleNumbers.Contains(btn))
            .ToList();

        var buildingValueSum = buildingsForThisLand.Sum(b => b.AppraisedValue ?? 0m);
        var landAppraisedValue = p.AppraisedValue ?? 0m;
        var totalAppraisedValue = landAppraisedValue + buildingValueSum;

        var ci = p.ConstructionInspection;
        bool isUnderConstruction = ci is not null && ci.OverallCurrentProgressPercent < 100m;
        decimal? overallPct = ci?.OverallCurrentProgressPercent;
        Guid? lastInspectionId = ci?.InspectionId;

        var upsertData = new LandUpsertData(
            LandShapeType: null,
            LandZoneType: null,
            UrbanPlanningType: null,
            AccessRoadWidth: null,
            RoadFrontage: null,
            LandArea: null,
            Street: null,
            Village: null,
            PostalCode: null,
            Latitude: null,
            Longitude: null,
            AppraisalId: appraisal.AppraisalId,
            AppraisalNumber: appraisal.AppraisalNumber ?? string.Empty,
            AppraisalDate: appraisal.CompletedAt ?? DateTime.UtcNow,
            AppraisedValue: landAppraisedValue,
            TotalAppraisedValue: totalAppraisedValue,
            IsUnderConstruction: isUnderConstruction,
            OverallConstructionProgressPercent: overallPct,
            LastConstructionInspectionId: lastInspectionId
        );

        master.UpsertFromLandAppraisal(upsertData);

        var snapshot = SnapshotBuilder.BuildLand(p, buildingsForThisLand, totalAppraisedValue);
        AppendEngagement(master, p, appraisal, snapshot);

        return master;
    }

    private static HashSet<string> BuildExistingGroupTitleKeys(
        CollateralMaster master,
        List<CollateralMaster> aliases)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (master.LandDetail is { } ld)
            keys.Add(BuildTitleKey(ld.LandOfficeCode, ld.Province, ld.Amphur, ld.Tambon, ld.TitleDeedType, ld.TitleDeedNo));
        foreach (var a in aliases)
        {
            if (a.LandDetail is { } ald)
                keys.Add(BuildTitleKey(ald.LandOfficeCode, ald.Province, ald.Amphur, ald.Tambon, ald.TitleDeedType, ald.TitleDeedNo));
        }
        return keys;
    }

    private static string BuildTitleKey(
        string landOffice, string province, string amphur, string tambon,
        string titleType, string titleNo)
        => $"{landOffice}|{province}|{amphur}|{tambon}|{titleType}|{titleNo}";

    private async Task UpsertCondoAsync(
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
                ownerName: string.Empty,
                landOfficeCode: condo.LandOffice!,
                condoRegistrationNumber: condo.CondoRegistrationNumber!,
                buildingNumber: condo.BuildingNumber!,
                floorNumber: condo.FloorNumber!,
                unitNumber: condo.RoomNumber!,
                titleNumber: condo.TitleNumber!,
                titleType: condo.TitleType!,
                condoName: null,
                province: condo.Province);
            repo.Add(master);
        }

        var upsertData = new CondoUpsertData(
            CondoName: null,
            Province: condo.Province,
            UsableArea: null,
            LocationType: null,
            BuildingAge: null,
            ConstructionYear: null,
            ModelName: null,
            AppraisalId: appraisal.AppraisalId,
            AppraisalNumber: appraisal.AppraisalNumber ?? string.Empty,
            AppraisalDate: appraisal.CompletedAt ?? DateTime.UtcNow,
            AppraisedValue: p.AppraisedValue
        );

        master.UpsertFromCondoAppraisal(upsertData);

        var snapshot = SnapshotBuilder.BuildCondo(p);
        AppendEngagement(master, p, appraisal, snapshot);
    }

    private async Task UpsertMachineAsync(
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
            EngineNo: null,
            ChassisNo: null,
            YearOfManufacture: null,
            MachineCondition: null,
            MachineAge: null,
            IncomingRegistrationNo: m.RegistrationNo,
            AppraisalId: appraisal.AppraisalId,
            AppraisalNumber: appraisal.AppraisalNumber ?? string.Empty,
            AppraisalDate: appraisal.CompletedAt ?? DateTime.UtcNow,
            AppraisedValue: p.AppraisedValue
        );

        master.UpsertFromMachineAppraisal(upsertData);

        var snapshot = SnapshotBuilder.BuildMachine(p);
        AppendEngagement(master, p, appraisal, snapshot);
    }

    private async Task UpsertLeaseholdAsync(
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
        // Try to find a sibling Land property from the same appraisal.
        // For LSL / LS types the land identity is on the property itself.
        CollateralMaster? underlyingMaster = null;
        string underlyingType = "Land";

        // Attempt: find a Land sibling property to use as underlying
        var landSibling = landProperties.FirstOrDefault();
        if (landSibling?.LandIdentity is { } landId && landId.Titles.Any(t => !string.IsNullOrWhiteSpace(t.TitleNumber)))
        {
            var title = landId.Titles.First(t => !string.IsNullOrWhiteSpace(t.TitleNumber));

            // Check Pass-1 in-memory cache first (master may have been created this same call
            // but not yet committed, so a DB query would return null). Keyed by sibling's PropertyId.
            landMasterByPropertyId.TryGetValue(landSibling.PropertyId, out underlyingMaster);

            if (underlyingMaster is null)
            {
                // Use IncludingAliases so we can find the master even when the title matches an alias row.
                var landHit = await repo.FindLandByDedupKeyIncludingAliases(
                    landId.LandOffice!, landId.Province!, landId.District!, landId.SubDistrict!,
                    title.TitleType, title.TitleNumber, null, ct);

                if (landHit is not null && !landHit.IsMaster)
                {
                    // Alias — resolve to parent
                    underlyingMaster = await repo.FindByIdWithEngagementsAsync(landHit.ParentMasterId!.Value, ct);
                }
                else
                {
                    underlyingMaster = landHit;
                }
            }

            if (underlyingMaster is null)
            {
                // Auto-create the underlying land master
                underlyingMaster = CollateralMaster.CreateLand(
                    ownerName: string.Empty,
                    landOfficeCode: landId.LandOffice!,
                    province: landId.Province!,
                    amphur: landId.District!,
                    tambon: landId.SubDistrict!,
                    titleDeedType: title.TitleType,
                    titleDeedNo: title.TitleNumber,
                    surveyOrParcelNo: null,
                    street: null, village: null, postalCode: null,
                    latitude: null, longitude: null);
                repo.Add(underlyingMaster);

                // Also append engagement for the underlying master
                var underlyingSnapshot = SnapshotBuilder.BuildLand(landSibling, [], 0m);
                AppendEngagement(underlyingMaster, landSibling, appraisal, underlyingSnapshot);
            }

            underlyingType = "Land";
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
                    unitNumber: condoId.RoomNumber!,
                    titleNumber: condoId.TitleNumber!,
                    titleType: condoId.TitleType!,
                    condoName: null,
                    province: condoId.Province);
                repo.Add(underlyingMaster);

                var underlyingSnapshot = SnapshotBuilder.BuildCondo(condoSibling);
                AppendEngagement(underlyingMaster, condoSibling, appraisal, underlyingSnapshot);
            }

            underlyingType = "Condo";
        }
        else
        {
            // Cannot resolve underlying — dead-letter
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

        var leaseholdUpsertData = new LeaseholdUpsertData(
            LeaseTermEnd: leaseTermEnd,
            LeaseTermMonths: null,
            AnnualRent: null,
            LeasePurpose: null,
            AppraisalId: appraisal.AppraisalId,
            AppraisalNumber: appraisal.AppraisalNumber ?? string.Empty,
            AppraisalDate: appraisal.CompletedAt ?? DateTime.UtcNow,
            AppraisedValue: p.AppraisedValue
        );

        leaseMaster.UpsertFromLeaseholdAppraisal(leaseholdUpsertData);

        var snapshot = SnapshotBuilder.BuildLeasehold(p, underlyingMaster.Id, underlyingType);
        AppendEngagement(leaseMaster, p, appraisal, snapshot);
    }

    // -------------------------------------------------------------------------
    // Shared helpers
    // -------------------------------------------------------------------------

    private static void AppendEngagement(
        CollateralMaster master,
        AppraisalPropertyForCollateral p,
        AppraisalForCollateralResult appraisal,
        string snapshot)
    {
        Guid? companyId = appraisal.CompanyId.HasValue() ? Guid.Parse(appraisal.CompanyId!) : null;

        master.AppendEngagement(
            appraisalId: appraisal.AppraisalId,
            appraisalNumber: appraisal.AppraisalNumber ?? string.Empty,
            requestId: appraisal.RequestId,
            requestNumber: appraisal.RequestNumber ?? string.Empty,
            propertyId: p.PropertyId,
            appraisalType: appraisal.AppraisalType,
            appraisalDate: appraisal.CompletedAt ?? DateTime.UtcNow,
            appraisedValue: appraisal.AppraisedValue,
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
        // Inspect the entries that failed — they're CollateralEngagement when the
        // (AppraisalId, PropertyId) idempotency index trips.
        return ex.Entries.Any(e => e.Entity is CollateralEngagement);
    }

    private static string? ExtractViolatedIndexName(DbUpdateException ex)
    {
        if (ex.InnerException is not SqlException sqlEx) return null;
        // SQL Server error message contains the index name in single quotes:
        // "Cannot insert duplicate key row in object '...' with unique index 'IX_NAME'."
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
