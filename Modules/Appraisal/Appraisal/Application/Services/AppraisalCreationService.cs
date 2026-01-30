using Appraisal.Domain.Appraisals;
using Microsoft.Extensions.Logging;
using Request.Contracts.Requests.Dtos;

namespace Appraisal.Application.Services;

/// <summary>
/// Service implementation for creating appraisals from request submissions.
/// </summary>
public class AppraisalCreationService(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork,
    ILogger<AppraisalCreationService> logger) : IAppraisalCreationService
{
    public async Task<Guid> CreateAppraisalFromRequest(
        Guid requestId,
        List<RequestTitleDto> requestTitles,
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
            .Where(t => t.CollateralType == "L")
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
            requestId: requestId,
            appraisalType: "Initial",
            priority: "Normal",
            slaDays: 30); // Default SLA of 30 days

        // Set appraisal number: APP-{yyyyMMdd}-{GUID8}
        var appraisalNumber = $"APP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
        appraisal.SetAppraisalNumber(appraisalNumber);

        logger.LogInformation("Created appraisal {AppraisalNumber} for request {RequestId}",
            appraisalNumber, requestId);

        // Step 4: For each land title, create AppraisalProperty + LandAppraisalDetail + LandTitle
        foreach (var landTitle in landTitles)
        {
            // Add land property with detail
            var property = appraisal.AddLandProperty(
                owner: landTitle.OwnerName ?? "Unknown",
                description: $"Title: {landTitle.TitleNo ?? "N/A"}");

            logger.LogInformation("Added land property {PropertyId} for title {TitleNo}",
                property.Id, landTitle.TitleNo);

            // Get the land detail (it was created by AddLandProperty)
            var landDetail = property.LandDetail;
            if (landDetail != null)
            {
                // Step 5: Create and add LandTitle with data from RequestTitleDto
                var title = LandTitle.Create(
                    landAppraisalDetailId: landDetail.Id,
                    titleDeedNumber: landTitle.TitleNo ?? "N/A",
                    titleDeedType: landTitle.DeedType ?? "Unknown");

                // Update with additional fields
                var landArea = LandArea.Create(
                    rai: landTitle.AreaRai,
                    ngan: landTitle.AreaNgan,
                    squareWa: landTitle.AreaSquareWa);

                title.Update(
                    bookNumber: null, // Not provided in RequestTitleDto
                    pageNumber: landTitle.LandNo, // Using LandNo as PageNumber
                    landParcelNumber: null,
                    surveyNumber: landTitle.SurveyNo,
                    mapSheetNumber: null,
                    rawang: landTitle.Rawang,
                    aerialMapName: null,
                    aerialMapNumber: null,
                    area: landArea,
                    hasBoundaryMarker: null,
                    boundaryMarkerRemark: null,
                    isDocumentValidated: null,
                    isMissingFromSurvey: null,
                    governmentPricePerSqWa: null,
                    governmentPrice: null,
                    remark: landTitle.Notes);

                landDetail.AddTitle(title);

                logger.LogInformation("Added land title {TitleDeedNumber} to property {PropertyId}",
                    title.TitleDeedNumber, property.Id);
            }
        }

        // Step 6: Create initial PropertyGroup and add all properties to it
        var initialGroup = appraisal.CreateGroup("Initial Group", "Auto-generated group for all properties");

        logger.LogInformation("Created initial property group {GroupId} with name '{GroupName}'",
            initialGroup.Id, initialGroup.GroupName);

        // Step 7: Add all properties to the initial group
        foreach (var property in appraisal.Properties)
        {
            appraisal.AddPropertyToGroup(initialGroup.Id, property.Id);
            logger.LogInformation("Added property {PropertyId} to group {GroupId}",
                property.Id, initialGroup.Id);
        }

        // Step 8: Save via repository
        await appraisalRepository.AddAsync(appraisal, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Successfully created appraisal {AppraisalId} ({AppraisalNumber}) with {PropertyCount} properties for request {RequestId}",
            appraisal.Id, appraisalNumber, appraisal.Properties.Count, requestId);

        return appraisal.Id;
    }
}
