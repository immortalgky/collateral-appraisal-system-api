using Request.Contracts.Requests.Dtos;
using Shared.Identity;

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
    ILogger<AppraisalCreationService> logger) : IAppraisalCreationService
{
    public async Task<Guid> CreateAppraisalFromRequest(
        Guid requestId,
        List<RequestTitleDto> requestTitles,
        AppointmentDto? appointment = null,
        FeeDto? fee = null,
        ContactDto? contact = null,
        string? createdBy = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Creating appraisal from request {RequestId} with {TitleCount} titles",
            requestId, requestTitles.Count);

        // Step 1: Check idempotency - does an appraisal already exist for this request?
        var existingAppraisal = await appraisalRepository.GetByRequestIdAsync(requestId, cancellationToken);
        if (existingAppraisal.Any())
        {
            var existingId = existingAppraisal.First().Id;
            logger.LogInformation("Appraisal already exists for request {RequestId}: {AppraisalId}",
                requestId, existingId);
            return existingId;
        }

        // Step 2: Filter for Land type only (CollateralType == "L")
        var landTitles = requestTitles
            .Where(t => t.CollateralType is "L" or "LB" or "U")
            .ToList();

        if (!landTitles.Any())
        {
            logger.LogWarning("No land titles found for request {RequestId}. Skipping appraisal creation.",
                requestId);
            throw new InvalidOperationException($"No land titles found for request {requestId}");
        }

        logger.LogInformation("Processing {LandTitleCount} land titles for request {RequestId}",
            landTitles.Count, requestId);

        // Step 3: Create Appraisal aggregate
        var appraisal = Domain.Appraisals.Appraisal.Create(
            requestId,
            "Initial",
            "Normal",
            30); // Default SLA of 30 days

        // Set appraisal number: APP-{yyyyMMdd}-{GUID8}
        var appraisalNumber = $"APP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        appraisal.SetAppraisalNumber(appraisalNumber);

        logger.LogInformation("Created appraisal {AppraisalNumber} for request {RequestId}",
            appraisalNumber, requestId);

        // Step 4: For each land title, create AppraisalProperty + LandAppraisalDetail + LandTitle
        foreach (var landTitle in landTitles)
        {
            // Add land property with detail
            var property = appraisal.AddLandProperty();

            logger.LogInformation("Added land property {PropertyId} for title {TitleNumber}",
                property.Id, landTitle.TitleNumber);

            // Get the land detail (it was created by AddLandProperty)
            var landDetail = property.LandDetail;
            if (landDetail != null)
            {
                // Step 5: Create and add LandTitle with data from RequestTitleDto
                var title = LandTitle.Create(
                    landDetail.Id,
                    landTitle.TitleNumber ?? "N/A",
                    landTitle.TitleType ?? "Unknown");

                // Update with additional fields
                var landArea = LandArea.Create(
                    landTitle.AreaRai,
                    landTitle.AreaNgan,
                    landTitle.AreaSquareWa);

                title.Update(
                    landTitle.BookNumber,
                    landTitle.PageNumber,
                    landTitle.LandParcelNumber,
                    landTitle.SurveyNumber,
                    landTitle.MapSheetNumber,
                    landTitle.Rawang,
                    landTitle.AerialMapName,
                    landTitle.AerialMapNumber,
                    landArea,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);

                landDetail.AddTitle(title);

                // Populate LandAppraisalDetail with address/owner data from the request title
                var adminAddress = AdministrativeAddress.Create(
                    landTitle.TitleAddress?.SubDistrict,
                    landTitle.TitleAddress?.District,
                    landTitle.TitleAddress?.Province);

                landDetail.Update(
                    landTitle.TitleAddress?.ProjectName,
                    address: adminAddress,
                    ownerName: landTitle.OwnerName,
                    street: landTitle.TitleAddress?.Road,
                    soi: landTitle.TitleAddress?.Soi,
                    village: landTitle.TitleAddress?.Moo,
                    addressLocation: landTitle.TitleAddress?.HouseNumber);

                logger.LogInformation("Added land title {TitleDeedNumber} to property {PropertyId}",
                    title.TitleNumber, property.Id);
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

            // Phase 2: Create group + assignment, then save so the assignment row exists in DB
            // before we create entities (Appointment, AppraisalFee) that FK-reference it.
            var initialGroup = appraisal.CreateGroup("Initial Group", "Auto-generated group for all properties");

            foreach (var property in appraisal.Properties) initialGroup.AddProperty(property.Id);

            // var assignment = appraisal.Assign(
            //     "Internal",
            //     null,
            //     null,
            //     "Manual",
            //     assignedBy: createdBy ?? string.Empty);

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

            // Phase 3: Create fee + appointment (both FK to assignment, which now exists in DB)
            // Only auto-create the Appraisal Fee (code "01") with amount based on TotalSellingPrice tier.
            // Other fees (Travel, Urgent) are added manually via the AddFeeItem endpoint.
            var totalSellingPrice = fee?.TotalSellingPrice ?? 0m;

            var appraisalFeeStructure = await dbContext.FeeStructures
                .Where(fs => fs.IsActive && fs.FeeCode == "01")
                .ToListAsync(cancellationToken);

            var matchedTier = appraisalFeeStructure.FirstOrDefault(fs => fs.IsApplicableFor(totalSellingPrice));
            if (matchedTier is null)
            {
                // Fallback: use highest tier (open-ended MaxSellingPrice)
                matchedTier = appraisalFeeStructure
                    .OrderByDescending(fs => fs.MinSellingPrice)
                    .First();
                logger.LogWarning(
                    "No fee tier matched TotalSellingPrice {TotalSellingPrice}. Falling back to highest tier (BaseAmount={BaseAmount})",
                    totalSellingPrice, matchedTier.BaseAmount);
            }

            var appraisalFee = AppraisalFee.Create(
                assignment.Id,
                fee?.FeePaymentType,
                fee?.FeeNotes);
            appraisalFee.AddItem(matchedTier.FeeCode, matchedTier.FeeName, matchedTier.BaseAmount);

            if (fee?.AbsorbedAmount is > 0) appraisalFee.SetBankAbsorb(fee.AbsorbedAmount.Value);

            dbContext.AppraisalFees.Add(appraisalFee);

            logger.LogInformation(
                "Created fee {FeeId} with Appraisal Fee (BaseAmount={BaseAmount}) for assignment {AssignmentId} (TotalSellingPrice={TotalSellingPrice})",
                appraisalFee.Id, matchedTier.BaseAmount, assignment.Id, totalSellingPrice);

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
            appraisal.Id, appraisalNumber, appraisal.Properties.Count, requestId);

        return appraisal.Id;
    }
}