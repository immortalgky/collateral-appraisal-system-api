using Appraisal.Application.Features.Appraisals.Shared;
using Appraisal.Application.Features.Appraisals.CreateLandProperty;

namespace Appraisal.Application.Features.Appraisals.CreateLandPMAProperty;

/// <summary>
/// Handler for updating a land property detail
/// </summary>
public class UpdateLandPMAPropertyCommandHandler(
    IAppraisalRepository appraisalRepository,
    IAppraisalUnitOfWork unitOfWork
) : ICommandHandler<CreateLandPMAPropertyCommand, CreateLandPMAPropertyResult>
{
    public async Task<CreateLandPMAPropertyResult> Handle(
        CreateLandPMAPropertyCommand command,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdWithPropertiesAsync(
                            command.AppraisalId, cancellationToken)
                        ?? throw new AppraisalNotFoundException(command.AppraisalId);

        var property = appraisal.AddLandAndBuildingProperty();

        property.UpdatePrice(
            sellingPrice: command.SellingPrice,
            forcedSalePrice: command.ForcedSalePrice,
            buildingInsurancePrice: command.BuildingInsurancePrice
        );

        Address? address = null;
        if (command.SubDistrict is not null || command.District is not null ||
            command.Province is not null)
            address = Address.Create(
                command.SubDistrict,
                command.District,
                command.Province
            );
        property.LandDetail!.UpdatePmaFields(
            ownerName: "",
            address: address
        );


        // Sync land titles (null = no-op, empty list = clear all)
        if (command.Titles is not null)
            SyncTitles(property.LandDetail!, command.Titles);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (command.GroupId.HasValue) appraisal.AddPropertyToGroup(command.GroupId.Value, property.Id);

        return new CreateLandPMAPropertyResult(property.Id, property.LandDetail.Id);
    }

    private static void SyncTitles(LandAppraisalDetail landDetail, List<LandTitleItemData> incomingTitles)
    {
        var incomingIds = incomingTitles
            .Where(t => t.Id.HasValue)
            .Select(t => t.Id!.Value)
            .ToHashSet();

        // Delete titles not in the incoming list
        var titlesToRemove = landDetail.Titles
            .Where(t => !incomingIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToList();
        foreach (var id in titlesToRemove)
            landDetail.RemoveTitle(id);

        // Add or update; the incoming list order wins, so every title is stamped with its 1-based position
        var sequence = 0;
        // Looked up once: Titles sorts and copies on every read.
        var existingById = landDetail.Titles.Where(t => t.Id != Guid.Empty).ToDictionary(t => t.Id);
        foreach (var titleData in incomingTitles)
        {
            sequence++;
            LandArea? area = null;
            if (titleData.Rai.HasValue || titleData.Ngan.HasValue || titleData.SquareWa.HasValue)
                area = LandArea.Create(titleData.Rai, titleData.Ngan, titleData.SquareWa);

            if (titleData.Id.HasValue)
            {
                // Update existing
                var existing = existingById.GetValueOrDefault(titleData.Id.Value);
                existing?.SetSequenceNumber(sequence);
                existing?.Update(
                    titleData.BookNumber, titleData.PageNumber,
                    titleData.LandParcelNumber, titleData.SurveyNumber,
                    titleData.MapSheetNumber, titleData.Rawang,
                    titleData.AerialMapName, titleData.AerialMapNumber,
                    area, titleData.BoundaryMarkerType, titleData.BoundaryMarkerRemark,
                    titleData.DocumentValidationResultType, titleData.IsMissingFromSurvey,
                    titleData.GovernmentPricePerSqWa, titleData.GovernmentPrice,
                    titleData.Remark);
            }
            else
            {
                // Create new
                var title = LandTitle.Create(landDetail.Id, titleData.TitleNumber, titleData.TitleType ?? "DEED");
                title.SetSequenceNumber(sequence);
                title.Update(
                    titleData.BookNumber, titleData.PageNumber,
                    titleData.LandParcelNumber, titleData.SurveyNumber,
                    titleData.MapSheetNumber, titleData.Rawang,
                    titleData.AerialMapName, titleData.AerialMapNumber,
                    area, titleData.BoundaryMarkerType, titleData.BoundaryMarkerRemark,
                    titleData.DocumentValidationResultType, titleData.IsMissingFromSurvey,
                    titleData.GovernmentPricePerSqWa, titleData.GovernmentPrice,
                    titleData.Remark);
                landDetail.AddTitle(title);
            }
        }
    }
}