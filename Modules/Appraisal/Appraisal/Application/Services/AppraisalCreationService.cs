using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Projects;
using Request.Contracts.Requests.Dtos;
using Shared.Identity;
using Shared.Time;
using Workflow;

namespace Appraisal.Application.Services;

/// <summary>
/// Service implementation for creating appraisals from request submissions.
/// Creates the appraisal aggregate with properties, groups, an initial assignment, fee, and appointment.
/// </summary>
public class AppraisalCreationService(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork,
    AppraisalDbContext dbContext,
    ICurrentUserService currentUserService,
    ILogger<AppraisalCreationService> logger,
    ISlaCalculatorClient slaCalculatorClient,
    IDateTimeProvider dateTimeProvider) : IAppraisalCreationService
{
    public async Task<Guid> CreateAppraisalFromRequest(
        Guid requestId,
        List<RequestTitleDto> requestTitles,
        AppointmentDto? appointment = null,
        FeeDto? fee = null,
        ContactDto? contact = null,
        string? createdBy = null,
        string? priority = null,
        bool isPma = false,
        string? purpose = null,
        string? channel = null,
        string? bankingSegment = null,
        decimal? facilityLimit = null,
        bool hasAppraisalBook = false,
        string? requestedBy = null,
        DateTime? requestedAt = null,
        Guid? prevAppraisalId = null,
        string? appraisalType = null,
        Guid? workflowDefinitionId = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Creating appraisal from request {RequestId} with {TitleCount} titles",
            requestId, requestTitles.Count);

        // Step 1: Check idempotency - does an appraisal already exist for this request?
        var existingAppraisal = await appraisalRepository.GetByRequestIdAsync(requestId, cancellationToken);
        if (existingAppraisal is not null)
        {
            var existingId = existingAppraisal.Id;
            logger.LogInformation("Appraisal already exists for request {RequestId}: {AppraisalId}",
                requestId, existingId);
            return existingId;
        }

        // Step 2: Filter for supported collateral types via new 33-code parameter table
        var titlesToProcess = requestTitles
            .Where(t => CodeToAppraisalFamily.ContainsKey(t.CollateralType ?? ""))
            .ToList();

        var skipped = requestTitles.Except(titlesToProcess).ToList();
        foreach (var s in skipped)
            logger.LogWarning(
                "Skipping title {TitleNumber} with unsupported collateral type {Code} on request {RequestId}",
                s.TitleNumber, s.CollateralType, requestId);

        if (!titlesToProcess.Any())
            logger.LogWarning(
                "No supported titles found for request {RequestId}. Creating appraisal with no properties.",
                requestId);

        logger.LogInformation("Processing {TitleCount} titles for request {RequestId}",
            titlesToProcess.Count, requestId);

        var isProgressive = appraisalType == AppraisalTypes.Progressive
                            && prevAppraisalId.HasValue;
        var isBlock = titlesToProcess.Any(t => ProjectCodes.Contains(t.CollateralType ?? ""));

        // Step 3: Resolve workflow-level SLA budget. workflowDefinitionId is optional because the caller
        // (AppraisalCreationRequestedIntegrationEventHandler) may not always know the definition ID at
        // creation time. When null, the appraisal SLA hours remain null instead of using a hardcoded fallback.
        int? appraisalSlaHours = null;
        if (workflowDefinitionId.HasValue)
        {
            var slaSnapshot = await slaCalculatorClient.GetWorkflowSlaAsync(
                workflowDefinitionId.Value, loanType: null, startedAt: DateTime.Now, cancellationToken);
            appraisalSlaHours = slaSnapshot?.DurationHours;
        }

        // Step 4: Create Appraisal aggregate
        var resolvedAppraisalType =
            isProgressive ? AppraisalTypes.Progressive
            : isBlock ? AppraisalTypes.PreAppraisal
            : AppraisalTypes.New;
        var appraisal = Domain.Appraisals.Appraisal.Create(
            requestId,
            resolvedAppraisalType,
            priority ?? "Normal",
            dateTimeProvider.ApplicationNow,
            appraisalSlaHours,
            requestedBy ?? createdBy,
            isPma,
            purpose,
            channel,
            bankingSegment,
            facilityLimit,
            hasAppraisalBook,
            requestedAt,
            isProgressive ? prevAppraisalId : null);

        // Track prior→new property mapping so we can duplicate PropertyPhotoMapping rows
        // AFTER Phase 1 SaveChanges (which is where DB-generated IDs land on the new properties).
        List<(Guid PriorPropertyId, AppraisalProperty NewProperty)> priorToNewProperties = [];

        if (isProgressive)
        {
            // Step 4 (Progressive path): Deep-copy properties from the prior appraisal.
            // ConstructionInspection (per-property) detail is intentionally excluded — it stays empty for fresh tracking.
            priorToNewProperties = await CopyPropertiesFromPriorAppraisalAsync(
                appraisal, prevAppraisalId!.Value, cancellationToken);
        }
        else
        {
            // Step 4 (normal path): Partition titles and create properties.
            //   - Land family (L, LB): collapse into ONE property; promote to LB if any LB present.
            //     All land titles merge into the single LandDetail.
            //   - Lease land family (LSL, LS): same rule, independent from non-lease family.
            //   - B, LSB: not auto-created (appraiser adds manually).
            //   - U, VEH, VES, MAC: one property per title.
            var landFamily = titlesToProcess.Where(t => GetAppraisalFamily(t) is "L" or "LB").ToList();
            var leaseLandFamily = titlesToProcess.Where(t => GetAppraisalFamily(t) is "LSL" or "LS").ToList();
            var notAutoCreated = titlesToProcess.Where(t => GetAppraisalFamily(t) is "B" or "LSB").ToList();

            foreach (var t in notAutoCreated)
                logger.LogInformation(
                    "Title {TitleNumber} ({Code}) will not be auto-created as a property; appraiser will add manually.",
                    t.TitleNumber, t.CollateralType);

            if (landFamily.Any())
                CreateLandFamilyProperty(appraisal, landFamily);

            if (leaseLandFamily.Any())
                CreateLeaseLandFamilyProperty(appraisal, leaseLandFamily);

            foreach (var title in titlesToProcess)
                switch (GetAppraisalFamily(title))
                {
                    case "U": CreateCondoProperty(appraisal, title); break;
                    case "LSU": CreateLeaseAgreementCondoProperty(appraisal, title); break;
                    case "VEH": CreateVehicleProperty(appraisal, title); break;
                    case "VES": CreateVesselProperty(appraisal, title); break;
                    case "MAC": CreateMachineryProperty(appraisal, title); break;
                }
        }

        // Wrap all saves in an explicit transaction to prevent partial success.
        // Three phases: (1) appraisal + properties, (2) group + assignment, (3) fee + appointment.
        // Owned entities need a save to get DB-generated IDs; the assignment must exist in DB
        // before entities with FK references to it (Appointment, AppraisalFee) can be inserted.
        await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Phase 1: Save appraisal + properties to get real IDs from NEWSEQUENTIALID()
            await appraisalRepository.AddAsync(appraisal, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Saved appraisal {AppraisalId} with {PropertyCount} properties (IDs assigned by DB)",
                appraisal.Id, appraisal.Properties.Count);

            // CI: duplicate PropertyPhotoMapping rows from prior properties onto new properties.
            // Reuses the same GalleryPhotoId (mapping is a join row, not a blob copy).
            // Done after Phase 1 SaveChanges so newProperty.Id is populated.
            if (priorToNewProperties.Count > 0)
                await CopyPhotoMappingsFromPriorAsync(priorToNewProperties, cancellationToken);

            // Pre-generate appendices from active AppendixType configuration
            var activeAppendixTypes = await dbContext.AppendixTypes
                .Where(t => t.IsActive)
                .OrderBy(t => t.SortOrder)
                .ToListAsync(cancellationToken);

            foreach (var appendixType in activeAppendixTypes)
            {
                var appendix = AppraisalAppendix.Create(
                    appraisal.Id, appendixType.Id, appendixType.SortOrder);
                dbContext.AppraisalAppendices.Add(appendix);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Pre-generated {Count} appendices for appraisal {AppraisalId}",
                activeAppendixTypes.Count, appraisal.Id);

            // Initialize Project for BlockLand (32) and BlockCondo (33). These external
            // CollateralType codes signal a block/project deal — we only create the Project
            // aggregate header, not individual property rows. ProjectType.Land has no
            // external code: it is reachable only via the in-app ChangeProjectType flow.
            var projectTitles = titlesToProcess.Where(t => ProjectCodes.Contains(t.CollateralType ?? "")).ToList();
            if (projectTitles.Any())
            {
                var projectCode = projectTitles.First().CollateralType;
                var projectType = projectCode == "32" ? ProjectType.LandAndBuilding : ProjectType.Condo;
                var project = Project.Create(appraisal.Id, projectType);
                dbContext.Projects.Add(project);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Initialized Project {ProjectId} (type={ProjectType}) for appraisal {AppraisalId}",
                    project.Id, projectType, appraisal.Id);
            }

            // Phase 2: Create group(s) + assignment, then save so the assignment row exists in DB
            // before we create entities (Appointment, AppraisalFee) that FK-reference it.
            //
            // For CI we mirror the prior appraisal's group structure so PricingAnalysis can be
            // cloned per-group with stable PropertyGroupId mapping. Otherwise default to "Group 1".
            Dictionary<Guid, Guid> priorToNewGroupIds = new();
            if (isProgressive && priorToNewProperties.Count > 0)
            {
                priorToNewGroupIds = await MirrorPriorGroupsFromPriorAsync(
                    appraisal, prevAppraisalId!.Value, priorToNewProperties, cancellationToken);
            }
            else
            {
                var initialGroup = appraisal.CreateGroup("Group 1", "Auto-generated group for all properties");
                foreach (var property in appraisal.Properties) initialGroup.AddProperty(property.Id);
            }

            var assignment = appraisal.AssignAdmin();

            // Explicitly add assignment via DbSet so EF Core traverses the entity graph
            // (including OwnsOne value objects like AssignmentType/AssignmentStatus) and marks
            // everything as Added. Without this, DetectChanges discovers the assignment through
            // the aggregate's HasMany collection with a non-sentinel key + ValueGeneratedOnAdd,
            // causing EF Core to assume it already exists (UPDATE instead of INSERT).
            dbContext.AppraisalAssignments.Add(assignment);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Saved group and assignment {AssignmentId} for appraisal {AppraisalId}",
                assignment.Id, appraisal.Id);

            // CI: clone pricing analysis from prior appraisal — per-group PA + Approaches/Methods
            // chain + 1:1 method analyses + AppraisalComparable rows. Status reset to "Draft".
            // Property-id map carries through for MachineCostItems whose AppraisalPropertyId must
            // be remapped to the new appraisal's property ids.
            if (isProgressive && priorToNewGroupIds.Count > 0)
            {
                var propertyIdMap = priorToNewProperties.ToDictionary(t => t.PriorPropertyId, t => t.NewProperty.Id);

                await ClonePricingFromPriorAsync(
                    prevAppraisalId!.Value, priorToNewGroupIds, propertyIdMap, cancellationToken);

                await CloneAppraisalComparablesFromPriorAsync(
                    prevAppraisalId!.Value, appraisal.Id, cancellationToken);

                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            // Phase 3: Create fee shell + appointment (both FK to assignment, which now exists in DB)
            // The shell captures the 4 fee-context fields from the request.
            // Fee items (the calculated amount) are added later by IAssignmentFeeService
            // when the real assignee is known (internal/external/quotation event handler).
            if (fee is not null)
            {
                var appraisalFee = AppraisalFee.Create(
                    assignment.Id,
                    fee.FeePaymentType,
                    fee.FeeNotes,
                    fee.TotalSellingPrice);
                if (fee.AbsorbedAmount is > 0m)
                    appraisalFee.SetBankAbsorb(fee.AbsorbedAmount.Value);
                dbContext.AppraisalFees.Add(appraisalFee);

                logger.LogInformation(
                    "Appraisal fee created: shell fee {FeeId} for assignment {AssignmentId} (TotalSellingPrice={TotalSellingPrice})",
                    appraisalFee.Id, assignment.Id, fee.TotalSellingPrice);
            }

            if (appointment?.AppointmentDateTime.HasValue == true)
            {
                var appt = Appointment.Create(
                    assignment.Id,
                    appointment.AppointmentDateTime.Value,
                    createdBy ?? "",
                    appointment.AppointmentLocation,
                    contact?.ContactPersonName,
                    contact?.ContactPersonPhone);

                dbContext.Appointments.Add(appt);

                logger.LogInformation("Created appointment {AppointmentId} for assignment {AssignmentId}",
                    appt.Id, assignment.Id);
            }

            // Commit — saves fee + appointment then commits the entire transaction
            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        logger.LogInformation(
            "Successfully created appraisal {AppraisalId} ({AppraisalNumber}) with {PropertyCount} properties for request {RequestId}",
            appraisal.Id, appraisal.AppraisalNumber, appraisal.Properties.Count, requestId);

        return appraisal.Id;
    }

    // Maps new 33-code parameter table codes to the appraisal family they belong to.
    // Family codes mirror the old letter codes used internally in the Appraisal domain.
    private static readonly Dictionary<string, string> CodeToAppraisalFamily = new()
    {
        ["01"] = "L", ["13"] = "L", ["14"] = "L", ["17"] = "L",
        ["19"] = "L", ["21"] = "L", ["26"] = "L", ["27"] = "L",
        ["02"] = "LB", ["03"] = "LB", ["04"] = "LB", ["23"] = "LB",
        ["24"] = "LB", ["32"] = "LB",
        ["05"] = "B", ["06"] = "B", ["07"] = "B", ["15"] = "B",
        ["16"] = "B", ["18"] = "B", ["20"] = "B", ["22"] = "B",
        ["08"] = "U", ["33"] = "U",
        ["09"] = "LS", ["25"] = "LS", ["30"] = "LS", ["31"] = "LS",
        ["10"] = "VEH",
        ["11"] = "MAC",
        ["12"] = "VES",
        ["28"] = "LSU",
        ["29"] = "LSL"
    };

    // Codes that require Project aggregate initialization alongside the Appraisal
    private static readonly HashSet<string> ProjectCodes = ["32", "33"];

    private static string GetAppraisalFamily(RequestTitleDto t)
    {
        return CodeToAppraisalFamily.TryGetValue(t.CollateralType ?? "", out var family) ? family : "";
    }

    private void CreateLandFamilyProperty(Domain.Appraisals.Appraisal appraisal, List<RequestTitleDto> titles)
    {
        // Promote to LB family if any LB-mapped code exists; otherwise plain L. All land titles merge into one LandDetail.
        var hasLandAndBuilding = titles.Any(t => GetAppraisalFamily(t) == "LB");
        var property = hasLandAndBuilding
            ? appraisal.AddLandAndBuildingProperty()
            : appraisal.AddLandProperty();

        logger.LogInformation(
            "Added {PropertyKind} property {PropertyId} grouping {TitleCount} title(s)",
            hasLandAndBuilding ? "land+building" : "land", property.Id, titles.Count);

        var landDetail = property.LandDetail;
        if (landDetail == null) return;

        // Primary title supplies property-level fields (project/owner/address); prefer an LB-family title if any.
        var primary = titles.FirstOrDefault(t => GetAppraisalFamily(t) == "LB") ?? titles.First();
        UpdateLandDetailTopFields(landDetail, primary);

        foreach (var t in titles) AddLandTitleFromRequest(landDetail, t);
    }

    private void CreateLeaseLandFamilyProperty(Domain.Appraisals.Appraisal appraisal, List<RequestTitleDto> titles)
    {
        // Promote to LS if any LS-mapped code exists; otherwise plain LSL. Mirrors the non-lease rule.
        var hasLandAndBuilding = titles.Any(t => GetAppraisalFamily(t) == "LS");
        var property = hasLandAndBuilding
            ? appraisal.AddLeaseAgreementLandAndBuildingProperty()
            : appraisal.AddLeaseAgreementLandProperty();

        logger.LogInformation(
            "Added lease-agreement {PropertyKind} property {PropertyId} grouping {TitleCount} title(s)",
            hasLandAndBuilding ? "land+building" : "land", property.Id, titles.Count);

        var landDetail = property.LandDetail;
        if (landDetail == null) return;

        var primary = titles.FirstOrDefault(t => GetAppraisalFamily(t) == "LS") ?? titles.First();
        UpdateLandDetailTopFields(landDetail, primary);

        foreach (var t in titles) AddLandTitleFromRequest(landDetail, t);
        // BuildingDetail / LeaseAgreementDetail / RentalInfo stay empty — populated later by appraiser
    }

    private void CreateCondoProperty(Domain.Appraisals.Appraisal appraisal, RequestTitleDto requestTitle)
    {
        var property = appraisal.AddCondoProperty();

        logger.LogInformation("Added condo property {PropertyId} for title {TitleNumber}",
            property.Id, requestTitle.TitleNumber);

        var condoDetail = property.CondoDetail;
        if (condoDetail == null) return;

        var adminAddress = AdministrativeAddress.Create(
            requestTitle.TitleAddress?.SubDistrict,
            requestTitle.TitleAddress?.District,
            requestTitle.TitleAddress?.Province);

        condoDetail.Update(
            requestTitle.TitleAddress?.ProjectName,
            requestTitle.CondoName,
            requestTitle.BuildingNumber,
            roomNumber: requestTitle.RoomNumber,
            floorNumber: requestTitle.FloorNumber,
            usableArea: requestTitle.UsableArea,
            address: adminAddress,
            ownerName: requestTitle.OwnerName,
            street: requestTitle.TitleAddress?.Road,
            soi: requestTitle.TitleAddress?.Soi);
    }

    private void CreateLeaseAgreementCondoProperty(Domain.Appraisals.Appraisal appraisal, RequestTitleDto requestTitle)
    {
        var property = appraisal.AddLeaseAgreementCondoProperty();

        logger.LogInformation("Added lease agreement condo property {PropertyId} for title {RoomNumber}",
            property.Id, requestTitle.RoomNumber);

        var condoDetail = property.CondoDetail;
        if (condoDetail == null) return;

        var adminAddress = AdministrativeAddress.Create(
            requestTitle.TitleAddress?.SubDistrict,
            requestTitle.TitleAddress?.District,
            requestTitle.TitleAddress?.Province);

        condoDetail.Update(
            requestTitle.TitleAddress?.ProjectName,
            requestTitle.CondoName,
            requestTitle.BuildingNumber,
            roomNumber: requestTitle.RoomNumber,
            floorNumber: requestTitle.FloorNumber,
            usableArea: requestTitle.UsableArea,
            address: adminAddress,
            ownerName: requestTitle.OwnerName,
            street: requestTitle.TitleAddress?.Road,
            soi: requestTitle.TitleAddress?.Soi);
    }

    private void AddLandTitleFromRequest(LandAppraisalDetail landDetail, RequestTitleDto requestTitle)
    {
        var title = LandTitle.Create(
            landDetail.Id,
            requestTitle.TitleNumber ?? "N/A",
            requestTitle.TitleType ?? "Unknown");

        var landArea = LandArea.Create(
            requestTitle.AreaRai,
            requestTitle.AreaNgan,
            requestTitle.AreaSquareWa);

        title.Update(
            requestTitle.BookNumber,
            requestTitle.PageNumber,
            requestTitle.LandParcelNumber,
            requestTitle.SurveyNumber,
            requestTitle.MapSheetNumber,
            requestTitle.Rawang,
            requestTitle.AerialMapName,
            requestTitle.AerialMapNumber,
            landArea,
            null, null, null, null, null, null, null);

        landDetail.AddTitle(title);

        logger.LogInformation("Added land title {TitleDeedNumber} to land detail {LandDetailId}",
            title.TitleNumber, landDetail.Id);
    }

    private void UpdateLandDetailTopFields(LandAppraisalDetail landDetail, RequestTitleDto requestTitle)
    {
        var adminAddress = AdministrativeAddress.Create(
            requestTitle.TitleAddress?.SubDistrict,
            requestTitle.TitleAddress?.District,
            requestTitle.TitleAddress?.Province);

        landDetail.Update(
            requestTitle.TitleAddress?.ProjectName,
            address: adminAddress,
            ownerName: requestTitle.OwnerName,
            street: requestTitle.TitleAddress?.Road,
            soi: requestTitle.TitleAddress?.Soi,
            village: requestTitle.TitleAddress?.Moo,
            addressLocation: requestTitle.TitleAddress?.HouseNumber);
    }

    private void CreateVehicleProperty(Domain.Appraisals.Appraisal appraisal, RequestTitleDto requestTitle)
    {
        var property = appraisal.AddVehicleProperty();

        logger.LogInformation("Added vehicle property {PropertyId} for title {TitleNumber}",
            property.Id, requestTitle.TitleNumber);

        var vehicleDetail = property.VehicleDetail;
        if (vehicleDetail == null) return;

        vehicleDetail.Update(
            chassisNo: requestTitle.VIN,
            registrationNumber: requestTitle.LicensePlateNumber,
            location: requestTitle.VehicleLocation,
            ownerName: requestTitle.OwnerName,
            vehiclePart: requestTitle.VehicleType);
    }

    private void CreateVesselProperty(Domain.Appraisals.Appraisal appraisal, RequestTitleDto requestTitle)
    {
        var property = appraisal.AddVesselProperty();

        logger.LogInformation("Added vessel property {PropertyId} for title {TitleNumber}",
            property.Id, requestTitle.TitleNumber);

        var vesselDetail = property.VesselDetail;
        if (vesselDetail == null) return;

        vesselDetail.Update(
            registrationNumber: requestTitle.VesselRegistrationNumber,
            vesselType: requestTitle.VesselType,
            location: requestTitle.VesselLocation,
            ownerName: requestTitle.OwnerName,
            other: requestTitle.HIN is not null ? $"HIN={requestTitle.HIN}" : null);
    }

    private void CreateMachineryProperty(Domain.Appraisals.Appraisal appraisal, RequestTitleDto requestTitle)
    {
        var property = appraisal.AddMachineryProperty();

        logger.LogInformation("Added machinery property {PropertyId} for title {TitleNumber}",
            property.Id, requestTitle.TitleNumber);

        var machineryDetail = property.MachineryDetail;
        if (machineryDetail == null) return;

        var otherParts = new List<string>();
        otherParts.Add($"RegistrationStatus={requestTitle.RegistrationStatus}");
        if (requestTitle.InvoiceNumber is not null) otherParts.Add($"Invoice={requestTitle.InvoiceNumber}");
        if (requestTitle.InstallationStatus is not null)
            otherParts.Add($"InstallationStatus={requestTitle.InstallationStatus}");

        machineryDetail.Update(
            registrationNumber: requestTitle.RegistrationNumber,
            machineName: requestTitle.MachineType,
            quantity: requestTitle.NumberOfMachine,
            ownerName: requestTitle.OwnerName,
            other: string.Join("; ", otherParts));
    }

    /// <summary>
    /// Loads properties from the prior appraisal and deep-copies them into the new CI appraisal.
    /// Uses the same CopyFrom static factories as Appraisal.CopyProperty().
    /// ConstructionInspection detail is excluded — it starts empty for fresh CI tracking.
    /// </summary>
    private async Task<List<(Guid PriorPropertyId, AppraisalProperty NewProperty)>>
        CopyPropertiesFromPriorAppraisalAsync(
            Domain.Appraisals.Appraisal appraisal,
            Guid prevAppraisalId,
            CancellationToken cancellationToken)
    {
        var priorToNew = new List<(Guid, AppraisalProperty)>();
        var priorAppraisal = await dbContext.Appraisals
            .AsNoTracking()
            .AsSplitQuery()
            .Include(a => a.Properties)
            .ThenInclude(p => p.LandDetail)
            .ThenInclude(l => l!.Titles)
            .Include(a => a.Properties)
            .ThenInclude(p => p.BuildingDetail)
            .Include(a => a.Properties)
            .ThenInclude(p => p.CondoDetail)
            .Include(a => a.Properties)
            .ThenInclude(p => p.VehicleDetail)
            .Include(a => a.Properties)
            .ThenInclude(p => p.VesselDetail)
            .Include(a => a.Properties)
            .ThenInclude(p => p.MachineryDetail)
            .Include(a => a.Properties)
            .ThenInclude(p => p.LeaseAgreementDetail)
            .Include(a => a.Properties)
            .ThenInclude(p => p.RentalInfo)
            .Include(a => a.Properties)
            .ThenInclude(p => p.ConstructionInspection)
            .ThenInclude(ci => ci!.WorkDetails)
            .FirstOrDefaultAsync(a => a.Id == prevAppraisalId, cancellationToken);

        if (priorAppraisal is null)
        {
            logger.LogWarning(
                "CI copy: prior appraisal {PrevAppraisalId} not found. Appraisal will have no properties.",
                prevAppraisalId);
            return priorToNew;
        }

        foreach (var prior in priorAppraisal.Properties)
        {
            var pt = prior.PropertyType;
            AppraisalProperty? newProp = null;

            if (pt == PropertyType.Land)
            {
                newProp = appraisal.AddLandProperty();
                if (prior.LandDetail is not null)
                    newProp.SetLandDetail(LandAppraisalDetail.CopyFrom(prior.LandDetail, newProp.Id));
            }
            else if (pt == PropertyType.Building)
            {
                newProp = appraisal.AddBuildingProperty();
                if (prior.BuildingDetail is not null)
                    newProp.SetBuildingDetail(BuildingAppraisalDetail.CopyFrom(prior.BuildingDetail, newProp.Id));
            }
            else if (pt == PropertyType.LandAndBuilding)
            {
                newProp = appraisal.AddLandAndBuildingProperty();
                if (prior.LandDetail is not null && prior.BuildingDetail is not null)
                    newProp.SetLandAndBuildingDetails(
                        LandAppraisalDetail.CopyFrom(prior.LandDetail, newProp.Id),
                        BuildingAppraisalDetail.CopyFrom(prior.BuildingDetail, newProp.Id));
                else if (prior.LandDetail is not null)
                    newProp.SetLandDetail(LandAppraisalDetail.CopyFrom(prior.LandDetail, newProp.Id));
            }
            else if (pt == PropertyType.Condo)
            {
                newProp = appraisal.AddCondoProperty();
                if (prior.CondoDetail is not null)
                    newProp.SetCondoDetail(CondoAppraisalDetail.CopyFrom(prior.CondoDetail, newProp.Id));
            }
            else if (pt == PropertyType.Vehicle)
            {
                newProp = appraisal.AddVehicleProperty();
                if (prior.VehicleDetail is not null)
                    newProp.SetVehicleDetail(VehicleAppraisalDetail.CopyFrom(prior.VehicleDetail, newProp.Id));
            }
            else if (pt == PropertyType.Vessel)
            {
                newProp = appraisal.AddVesselProperty();
                if (prior.VesselDetail is not null)
                    newProp.SetVesselDetail(VesselAppraisalDetail.CopyFrom(prior.VesselDetail, newProp.Id));
            }
            else if (pt == PropertyType.Machinery)
            {
                newProp = appraisal.AddMachineryProperty();
                if (prior.MachineryDetail is not null)
                    newProp.SetMachineryDetail(MachineryAppraisalDetail.CopyFrom(prior.MachineryDetail, newProp.Id));
            }
            else if (pt == PropertyType.LeaseAgreementLand)
            {
                newProp = appraisal.AddLeaseAgreementLandProperty();
                if (prior.LandDetail is not null)
                    newProp.SetLandDetail(LandAppraisalDetail.CopyFrom(prior.LandDetail, newProp.Id));
                if (prior.LeaseAgreementDetail is not null)
                    newProp.SetLeaseAgreementDetail(LeaseAgreementDetail.CopyFrom(prior.LeaseAgreementDetail,
                        newProp.Id));
                if (prior.RentalInfo is not null)
                    newProp.SetRentalInfo(RentalInfo.CopyFrom(prior.RentalInfo, newProp.Id));
            }
            else if (pt == PropertyType.LeaseAgreementBuilding)
            {
                newProp = appraisal.AddLeaseAgreementBuildingProperty();
                if (prior.BuildingDetail is not null)
                    newProp.SetBuildingDetail(BuildingAppraisalDetail.CopyFrom(prior.BuildingDetail, newProp.Id));
                if (prior.LeaseAgreementDetail is not null)
                    newProp.SetLeaseAgreementDetail(LeaseAgreementDetail.CopyFrom(prior.LeaseAgreementDetail,
                        newProp.Id));
                if (prior.RentalInfo is not null)
                    newProp.SetRentalInfo(RentalInfo.CopyFrom(prior.RentalInfo, newProp.Id));
            }
            else if (pt == PropertyType.LeaseAgreementLandAndBuilding)
            {
                newProp = appraisal.AddLeaseAgreementLandAndBuildingProperty();
                if (prior.LandDetail is not null && prior.BuildingDetail is not null)
                    newProp.SetLandAndBuildingDetails(
                        LandAppraisalDetail.CopyFrom(prior.LandDetail, newProp.Id),
                        BuildingAppraisalDetail.CopyFrom(prior.BuildingDetail, newProp.Id));
                if (prior.LeaseAgreementDetail is not null)
                    newProp.SetLeaseAgreementDetail(LeaseAgreementDetail.CopyFrom(prior.LeaseAgreementDetail,
                        newProp.Id));
                if (prior.RentalInfo is not null)
                    newProp.SetRentalInfo(RentalInfo.CopyFrom(prior.RentalInfo, newProp.Id));
            }
            else
            {
                logger.LogWarning(
                    "CI copy: skipping property {PropertyId} with unsupported type {PropertyType} from prior appraisal {PrevAppraisalId}",
                    prior.Id, pt.Code, prevAppraisalId);
                continue;
            }

            // Shift prior current progress → new previous progress; reset current for inspector to fill.
            if (prior.ConstructionInspection is not null)
                newProp.SetConstructionInspection(
                    ConstructionInspection.CopyForNextInspection(prior.ConstructionInspection, newProp.Id));

            priorToNew.Add((prior.Id, newProp));

            logger.LogInformation(
                "CI copy: copied property {SourcePropertyId} ({PropertyType}) from prior appraisal {PrevAppraisalId}",
                prior.Id, pt.Code, prevAppraisalId);
        }

        return priorToNew;
    }

    /// <summary>
    /// Duplicates PropertyPhotoMapping rows from prior properties onto their new copies so the
    /// new appraisal carries forward the source's photos. Reuses the same GalleryPhotoId — the
    /// mapping is a join row pointing at the same gallery photo, not a blob copy.
    /// Must be called AFTER Phase 1 SaveChanges so newProperty.Id is populated.
    /// </summary>
    private async Task CopyPhotoMappingsFromPriorAsync(
        List<(Guid PriorPropertyId, AppraisalProperty NewProperty)> priorToNew,
        CancellationToken cancellationToken)
    {
        var priorPropertyIds = priorToNew.Select(t => t.PriorPropertyId).ToList();

        var sourceMappings = await dbContext.PropertyPhotoMappings
            .AsNoTracking()
            .Where(m => priorPropertyIds.Contains(m.AppraisalPropertyId))
            .ToListAsync(cancellationToken);

        if (sourceMappings.Count == 0)
            return;

        var newPropertyByPriorId = priorToNew.ToDictionary(t => t.PriorPropertyId, t => t.NewProperty);
        var linkedBy = currentUserService.Username
                       ?? currentUserService.UserId?.ToString()
                       ?? "System";

        foreach (var src in sourceMappings)
        {
            if (!newPropertyByPriorId.TryGetValue(src.AppraisalPropertyId, out var newProp))
                continue;

            var copy = PropertyPhotoMapping.Create(
                src.GalleryPhotoId,
                newProp.Id,
                src.PhotoPurpose,
                linkedBy);
            copy.SetSection(src.SectionReference);
            copy.SetSequence(src.SequenceNumber);
            if (src.IsThumbnail)
                copy.SetAsThumbnail();
            dbContext.PropertyPhotoMappings.Add(copy);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "CI copy: duplicated {Count} PropertyPhotoMapping rows from prior properties.",
            sourceMappings.Count);
    }

    /// <summary>
    /// Mirrors the prior appraisal's PropertyGroups (including names, descriptions, group numbers,
    /// and property→group mapping) onto the new CI appraisal. Replaces the default "Group 1" path
    /// so each cloned PricingAnalysis can attach to a corresponding new group via FK.
    /// Returns prior PropertyGroupId → new PropertyGroupId map for the pricing-clone phase.
    /// </summary>
    private async Task<Dictionary<Guid, Guid>> MirrorPriorGroupsFromPriorAsync(
        Domain.Appraisals.Appraisal appraisal,
        Guid prevAppraisalId,
        List<(Guid PriorPropertyId, AppraisalProperty NewProperty)> priorToNewProperties,
        CancellationToken cancellationToken)
    {
        var priorAppraisal = await dbContext.Appraisals
            .AsNoTracking()
            .Include(a => a.Groups)
            .FirstOrDefaultAsync(a => a.Id == prevAppraisalId, cancellationToken);

        var map = new Dictionary<Guid, Guid>();
        if (priorAppraisal is null)
        {
            logger.LogWarning(
                "CI mirror groups: prior appraisal {PrevAppraisalId} not found; falling back to default Group 1.",
                prevAppraisalId);
            var initialGroup = appraisal.CreateGroup("Group 1", "Auto-generated group for all properties");
            foreach (var property in appraisal.Properties) initialGroup.AddProperty(property.Id);
            return map;
        }

        var newPropertyByPriorId = priorToNewProperties.ToDictionary(t => t.PriorPropertyId, t => t.NewProperty);

        foreach (var priorGroup in priorAppraisal.Groups.OrderBy(g => g.GroupNumber))
        {
            var newGroup = appraisal.CreateGroup(priorGroup.GroupName, priorGroup.Description);
            map[priorGroup.Id] = newGroup.Id;

            foreach (var item in priorGroup.Items.OrderBy(i => i.SequenceInGroup))
            {
                if (!newPropertyByPriorId.TryGetValue(item.AppraisalPropertyId, out var newProp))
                    continue; // prior property not copied (unsupported type) — skip
                appraisal.AddPropertyToGroup(newGroup.Id, newProp.Id);
            }
        }

        // Safety net — if no groups mirrored (e.g. prior appraisal had none), still attach properties
        // to a default group so downstream pricing/valuation flows have somewhere to live.
        if (map.Count == 0)
        {
            var fallback = appraisal.CreateGroup("Group 1", "Auto-generated group for all properties");
            foreach (var property in appraisal.Properties) fallback.AddProperty(property.Id);
        }

        logger.LogInformation(
            "CI mirror groups: created {Count} group(s) on appraisal {AppraisalId} from prior {PrevAppraisalId}.",
            map.Count, appraisal.Id, prevAppraisalId);

        return map;
    }

    /// <summary>
    /// Clones every PricingAnalysis from the prior appraisal onto the new CI appraisal.
    /// Loads the full Approaches → Methods → children chain (incl. 1:1 method analyses with their
    /// nested collections) AsNoTracking, then constructs new aggregates via the domain Clone* factories.
    /// Status is reset to "Draft"; FinalAppraisedValue carries forward (and re-derives ValuationAnalyses
    /// via AppraisalFinalValuesChangedEvent).
    /// </summary>
    private async Task ClonePricingFromPriorAsync(
        Guid prevAppraisalId,
        IReadOnlyDictionary<Guid, Guid> priorToNewGroupIds,
        IReadOnlyDictionary<Guid, Guid> priorToNewPropertyIds,
        CancellationToken cancellationToken)
    {
        var priorGroupIds = priorToNewGroupIds.Keys.ToList();
        if (priorGroupIds.Count == 0) return;

        var priorPricingAnalyses = await dbContext.PricingAnalyses
            .AsNoTracking()
            .AsSplitQuery()
            .Include(p => p.Approaches)
            .ThenInclude(a => a.Methods)
            .ThenInclude(m => m.ComparableLinks)
            .Include(p => p.Approaches)
            .ThenInclude(a => a.Methods)
            .ThenInclude(m => m.Calculations)
            .Include(p => p.Approaches)
            .ThenInclude(a => a.Methods)
            .ThenInclude(m => m.ComparativeFactors)
            .Include(p => p.Approaches)
            .ThenInclude(a => a.Methods)
            .ThenInclude(m => m.FactorScores)
            .Include(p => p.Approaches)
            .ThenInclude(a => a.Methods)
            .ThenInclude(m => m.MachineCostItems)
            .Include(p => p.Approaches)
            .ThenInclude(a => a.Methods)
            .ThenInclude(m => m.FinalValue)
            .Include(p => p.Approaches)
            .ThenInclude(a => a.Methods)
            .ThenInclude(m => m.RsqResult)
            .Include(p => p.Approaches)
            .ThenInclude(a => a.Methods)
            .ThenInclude(m => m.LeaseholdAnalysis!)
            .ThenInclude(l => l.LandGrowthPeriods)
            .Include(p => p.Approaches)
            .ThenInclude(a => a.Methods)
            .ThenInclude(m => m.LeaseholdAnalysis!)
            .ThenInclude(l => l.TableRows)
            .Include(p => p.Approaches)
            .ThenInclude(a => a.Methods)
            .ThenInclude(m => m.ProfitRentAnalysis!)
            .ThenInclude(pr => pr.GrowthPeriods)
            .Include(p => p.Approaches)
            .ThenInclude(a => a.Methods)
            .ThenInclude(m => m.ProfitRentAnalysis!)
            .ThenInclude(pr => pr.TableRows)
            .Include(p => p.Approaches)
            .ThenInclude(a => a.Methods)
            .ThenInclude(m => m.IncomeAnalysis!)
            .ThenInclude(i => i.Sections)
            .ThenInclude(s => s.Categories)
            .ThenInclude(c => c.Assumptions)
            .Include(p => p.Approaches)
            .ThenInclude(a => a.Methods)
            .ThenInclude(m => m.HypothesisAnalysis!)
            .ThenInclude(h => h.Uploads)
            .Include(p => p.Approaches)
            .ThenInclude(a => a.Methods)
            .ThenInclude(m => m.HypothesisAnalysis!)
            .ThenInclude(h => h.LandBuildingUnitRows)
            .Include(p => p.Approaches)
            .ThenInclude(a => a.Methods)
            .ThenInclude(m => m.HypothesisAnalysis!)
            .ThenInclude(h => h.CondominiumUnitRows)
            .Include(p => p.Approaches)
            .ThenInclude(a => a.Methods)
            .ThenInclude(m => m.HypothesisAnalysis!)
            .ThenInclude(h => h.CostItems)
            .ThenInclude(ci => ci.DepreciationPeriods)
            .Where(p => p.PropertyGroupId != null && priorGroupIds.Contains(p.PropertyGroupId.Value))
            .ToListAsync(cancellationToken);

        foreach (var prior in priorPricingAnalyses)
        {
            if (!priorToNewGroupIds.TryGetValue(prior.PropertyGroupId!.Value, out var newGroupId))
                continue;

            var clone = PricingAnalysis.CloneForGroup(prior, newGroupId, priorToNewPropertyIds);
            dbContext.PricingAnalyses.Add(clone);
        }

        logger.LogInformation(
            "CI pricing clone: cloned {Count} PricingAnalysis row(s) from prior {PrevAppraisalId}.",
            priorPricingAnalyses.Count, prevAppraisalId);
    }

    /// <summary>
    /// Clones the prior appraisal's per-appraisal AppraisalComparable rows (and their
    /// ComparableAdjustment children) onto the new CI appraisal. MarketComparableId references
    /// carry forward unchanged — comparables are global.
    /// </summary>
    private async Task CloneAppraisalComparablesFromPriorAsync(
        Guid prevAppraisalId,
        Guid newAppraisalId,
        CancellationToken cancellationToken)
    {
        var priorComparables = await dbContext.AppraisalComparables
            .AsNoTracking()
            .Include(c => c.Adjustments)
            .Where(c => c.AppraisalId == prevAppraisalId)
            .ToListAsync(cancellationToken);

        foreach (var prior in priorComparables)
        {
            var clone = AppraisalComparable.CloneForAppraisal(prior, newAppraisalId);
            dbContext.AppraisalComparables.Add(clone);
        }

        logger.LogInformation(
            "CI comparables clone: cloned {Count} AppraisalComparable row(s) from prior {PrevAppraisalId}.",
            priorComparables.Count, prevAppraisalId);
    }
}