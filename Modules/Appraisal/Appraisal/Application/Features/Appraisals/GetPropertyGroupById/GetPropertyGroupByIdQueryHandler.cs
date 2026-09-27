using Dapper;

namespace Appraisal.Application.Features.Appraisals.GetPropertyGroupById;

/// <summary>
/// Handler for getting a property group by ID
/// </summary>
public class GetPropertyGroupByIdQueryHandler(
    ISqlConnectionFactory sqlConnectionFactory
) : IQueryHandler<GetPropertyGroupByIdQuery, GetPropertyGroupByIdResult>
{
    public async Task<GetPropertyGroupByIdResult> Handle(
        GetPropertyGroupByIdQuery query,
        CancellationToken cancellationToken)
    {
        var sql = """
                    SELECT * 
                    FROM appraisal.vw_PropertyGroupDetail 
                    WHERE AppraisalId = @AppraisalId AND PropertyGroupId = @PropertyGroupId
                  """;

        var connection = sqlConnectionFactory.GetOpenConnection();

        var lookup = new Dictionary<Guid, GetPropertyGroupByIdResult>();

        var result = await connection.QueryAsync<PropertyGroupDto, PropertyGroupItemDto, GetPropertyGroupByIdResult>(
            sql,
            (group, item) =>
            {
                if (!lookup.TryGetValue(group.PropertyGroupId, out var result))
                {
                    result = new GetPropertyGroupByIdResult(
                        group.PropertyGroupId,
                        group.GroupNumber ?? 0,
                        group.GroupName ?? string.Empty,
                        group.Description,
                        group.PricingAnalysisId,
                        new List<PropertyGroupItemDto>()
                    );
                    lookup.Add(group.PropertyGroupId, result);
                }

                if (item is not null && item.PropertyId is not null)
                    result.Properties?.Add(item);

                return result;
            },
            new
            {
                query.AppraisalId,
                PropertyGroupId = query.GroupId
            },
            splitOn: "PropertyGroupItemId"
        );

        if (result is null || !lookup.Any())
            throw new InvalidOperationException($"Property group {query.GroupId} not found");

        var propertyGroup = lookup.First().Value;

        // Secondary query: fetch photo DocumentIds per property
        var propertyIds = propertyGroup.Properties?
            .Where(p => p.PropertyId is not null)
            .Select(p => p.PropertyId!.Value)
            .ToList();

        if (propertyIds is { Count: > 0 })
        {
            var photoSql = """
                           SELECT PPM.Id AS MappingId, PPM.AppraisalPropertyId, AG.DocumentId, PPM.IsThumbnail
                           FROM appraisal.PropertyPhotoMappings PPM
                           INNER JOIN appraisal.AppraisalGallery AG ON AG.Id = PPM.GalleryPhotoId
                           WHERE PPM.AppraisalPropertyId IN @PropertyIds
                           """;

            var photos = await connection.QueryAsync<PropertyPhotoRow>(
                photoSql,
                new { PropertyIds = propertyIds });

            var photosByProperty = photos
                .GroupBy(p => p.AppraisalPropertyId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var property in propertyGroup.Properties!)
            {
                if (property.PropertyId is not null &&
                    photosByProperty.TryGetValue(property.PropertyId.Value, out var propertyPhotos))
                {
                    property.Photos = propertyPhotos
                        .Select(p => new PropertyPhotoDto(p.MappingId, p.DocumentId, p.IsThumbnail))
                        .ToList();
                }
            }

            // Secondary query: fetch the full land titles per land property.
            // Only land/land-and-building properties have rows (joined via LandAppraisalDetails).
            var titleSql = """
                           SELECT lad.AppraisalPropertyId,
                                  lt.Id,
                                  lt.TitleNumber,
                                  lt.TitleType,
                                  lt.BookNumber,
                                  lt.PageNumber,
                                  lt.LandParcelNumber,
                                  lt.SurveyNumber,
                                  lt.MapSheetNumber,
                                  lt.Rawang,
                                  lt.AerialMapName,
                                  lt.AerialMapNumber,
                                  lt.AreaRai      AS Rai,
                                  lt.AreaNgan     AS Ngan,
                                  lt.AreaSquareWa AS SquareWa,
                                  lt.BoundaryMarkerType,
                                  lt.DocumentValidationResultType,
                                  lt.GovernmentPricePerSqWa,
                                  lt.GovernmentPrice,
                                  lt.Remark
                           FROM appraisal.LandTitles lt
                           INNER JOIN appraisal.LandAppraisalDetails lad ON lad.Id = lt.LandAppraisalDetailId
                           WHERE lad.AppraisalPropertyId IN @PropertyIds
                           """;

            var titles = await connection.QueryAsync<LandTitleDto>(
                titleSql,
                new { PropertyIds = propertyIds });

            var titlesByProperty = titles
                .GroupBy(tt => tt.AppraisalPropertyId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var property in propertyGroup.Properties!)
            {
                if (property.PropertyId is not null &&
                    titlesByProperty.TryGetValue(property.PropertyId.Value, out var propertyTitles))
                {
                    property.Titles = propertyTitles;
                }
            }

            // Secondary query: the per-type facts the list shows under each property's name — a
            // building's age, area and construction progress, a condo's project / floor / room, a
            // machine's year and age, a lease's remaining term and rent. One row per property; the
            // detail tables that do not apply to its type join as nulls.
            // Progress mirrors ConstructionInspection.OverallCurrentProgressPercent: the sum of the
            // work items' weighted shares in full-detail mode, the single summary figure otherwise.
            var factsSql = """
                           SELECT ap.Id AS AppraisalPropertyId,
                                  COALESCE(bad.BuildingAge, cad.BuildingAge)                 AS BuildingAge,
                                  COALESCE(bad.IsUnderConstruction, cad.IsUnderConstruction) AS IsUnderConstruction,
                                  bad.TotalBuildingArea                                      AS BuildingArea,
                                  CASE
                                      WHEN ci.Id IS NULL THEN NULL
                                      WHEN ci.IsFullDetail = 1 THEN ISNULL(
                                          (SELECT SUM(wd.CurrentProportionPct)
                                           FROM appraisal.ConstructionWorkDetails wd
                                           WHERE wd.ConstructionInspectionId = ci.Id), 0)
                                      ELSE ISNULL(ci.SummaryCurrentProgressPct, 0)
                                  END AS ConstructionProgressPct,
                                  cad.CondoName,
                                  cad.FloorNumber,
                                  cad.RoomNumber,
                                  cad.RoomLayoutType,
                                  cad.RoomLayoutTypeOther,
                                  mad.YearOfManufacture,
                                  mad.MachineAge,
                                  lsd.RemainingLeaseAsAppraisalDate AS RemainingLeaseYears,
                                  lsd.LeaseEndDate,
                                  lsd.LeaseRentFee
                           FROM appraisal.AppraisalProperties ap
                           LEFT JOIN appraisal.BuildingAppraisalDetails bad ON bad.AppraisalPropertyId = ap.Id
                           LEFT JOIN appraisal.CondoAppraisalDetails cad ON cad.AppraisalPropertyId = ap.Id
                           LEFT JOIN appraisal.ConstructionInspections ci ON ci.AppraisalPropertyId = ap.Id
                           LEFT JOIN appraisal.MachineryAppraisalDetails mad ON mad.AppraisalPropertyId = ap.Id
                           LEFT JOIN appraisal.LeaseAgreementDetails lsd ON lsd.AppraisalPropertyId = ap.Id
                           WHERE ap.Id IN @PropertyIds
                           """;

            var facts = (await connection.QueryAsync<PropertyFactsRow>(
                    factsSql,
                    new { PropertyIds = propertyIds }))
                .GroupBy(f => f.AppraisalPropertyId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var property in propertyGroup.Properties!)
            {
                if (property.PropertyId is null ||
                    !facts.TryGetValue(property.PropertyId.Value, out var f))
                    continue;

                property.BuildingAge = f.BuildingAge;
                property.IsUnderConstruction = f.IsUnderConstruction;
                // Only meaningful while the building is unfinished.
                property.ConstructionProgressPct = f.IsUnderConstruction == true ? f.ConstructionProgressPct : null;
                property.BuildingArea = f.BuildingArea;
                property.CondoName = f.CondoName;
                property.FloorNumber = f.FloorNumber;
                property.RoomNumber = f.RoomNumber;
                property.RoomLayoutType = f.RoomLayoutType;
                property.RoomLayoutTypeOther = f.RoomLayoutTypeOther;
                property.YearOfManufacture = f.YearOfManufacture;
                property.MachineAge = f.MachineAge;
                property.RemainingLeaseYears = f.RemainingLeaseYears;
                property.LeaseEndDate = f.LeaseEndDate;
                property.LeaseRentFee = f.LeaseRentFee;
            }
        }

        return propertyGroup;
    }
}

public record PropertyGroupDto
{
    public Guid? AppraisalId { get; set; }
    public Guid PropertyGroupId { get; set; }
    public int? GroupNumber { get; set; }
    public string? GroupName { get; set; }
    public string? Description { get; set; }
    public Guid? PricingAnalysisId { get; set; }
}

public record PropertyGroupItemDto
{
    public Guid? PropertyId { get; set; }
    public int? SequenceInGroup { get; set; }
    public string? PropertyType { get; set; } = default!;
    public Guid? AppraisalDetailId { get; set; }
    public string? PropertyName { get; set; } = default!;
    public decimal? Area { get; set; }
    public decimal? latitude { get; set; }
    public decimal? longitude { get; set; }
    public string? MachineName { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? RegistrationNumber { get; set; }
    /// <summary>Machinery only: registered with the authorities. Null for other property types.</summary>
    public bool? RegistrationStatus { get; set; }
    /// <summary>Machinery only: appraiser certifies this machine's price. Null for other types.</summary>
    public bool? IsPriceCertified { get; set; }

    /// <summary>Machinery only: ConditionUse parameter code. Null for other property types.</summary>
    public string? ConditionUse { get; set; }
    public string? Dimension { get; set; }
    public string? Location { get; set; }

    /// <summary>Building only: BuildingType parameter code. Null for other property types.</summary>
    public string? BuildingType { get; set; }

    /// <summary>Building only: the free-text type entered when BuildingType is '99' (other).</summary>
    public string? BuildingTypeOther { get; set; }

    /// <summary>Building only: storeys. Decimal because a mezzanine is recorded as a half floor.</summary>
    public decimal? NumberOfFloors { get; set; }
    /// <summary>Building (B / LB) or condo: age in years as entered. Null for other types or when blank.</summary>
    public int? BuildingAge { get; set; }
    /// <summary>Building (B / LB) or condo: still under construction. Null for other property types.</summary>
    public bool? IsUnderConstruction { get; set; }
    /// <summary>
    /// Building or condo, and only while under construction: overall current progress, 0-100, from its
    /// construction inspection. Null when finished or when no inspection has been recorded yet.
    /// </summary>
    public decimal? ConstructionProgressPct { get; set; }
    /// <summary>Building / land-and-building: the building's total floor area (m²).</summary>
    public decimal? BuildingArea { get; set; }
    /// <summary>Condo only: project name.</summary>
    public string? CondoName { get; set; }
    /// <summary>Condo only: floor as printed on the unit deed (may be "12A").</summary>
    public string? FloorNumber { get; set; }
    /// <summary>Condo only: unit number.</summary>
    public string? RoomNumber { get; set; }
    /// <summary>Condo only: RoomLayout parameter code.</summary>
    public string? RoomLayoutType { get; set; }
    /// <summary>Condo only: free-text layout when RoomLayoutType is "other".</summary>
    public string? RoomLayoutTypeOther { get; set; }
    /// <summary>Machinery only: year of manufacture.</summary>
    public int? YearOfManufacture { get; set; }
    /// <summary>Machinery only: age in years as entered.</summary>
    public decimal? MachineAge { get; set; }
    /// <summary>Lease types only: remaining lease term in years at the appraisal date, as entered.</summary>
    public decimal? RemainingLeaseYears { get; set; }
    /// <summary>Lease types only: contract end date.</summary>
    public DateTime? LeaseEndDate { get; set; }
    /// <summary>Lease types only: contractual rent.</summary>
    public decimal? LeaseRentFee { get; set; }
    /// <summary>Title deed no(s): comma-joined LandTitles for land, unit deed for condo.</summary>
    public string? TitleNo { get; set; }
    /// <summary>True for plain land (L/LB) flagged "rented out to others"; null for non-land types.</summary>
    public bool? IsRentedOut { get; set; }
    public List<PropertyPhotoDto>? Photos { get; set; }
    /// <summary>Full land titles (land/land-and-building only); null/empty for other types.</summary>
    public List<LandTitleDto>? Titles { get; set; }
}

public record PropertyPhotoDto(Guid MappingId, Guid DocumentId, bool IsThumbnail);

internal record PropertyPhotoRow(Guid MappingId, Guid AppraisalPropertyId, Guid DocumentId, bool IsThumbnail);

internal record PropertyFactsRow
{
    public Guid AppraisalPropertyId { get; init; }
    public int? BuildingAge { get; init; }
    public bool? IsUnderConstruction { get; init; }
    public decimal? BuildingArea { get; init; }
    public decimal? ConstructionProgressPct { get; init; }
    public string? CondoName { get; init; }
    public string? FloorNumber { get; init; }
    public string? RoomNumber { get; init; }
    public string? RoomLayoutType { get; init; }
    public string? RoomLayoutTypeOther { get; init; }
    public int? YearOfManufacture { get; init; }
    public decimal? MachineAge { get; init; }
    public decimal? RemainingLeaseYears { get; init; }
    public DateTime? LeaseEndDate { get; init; }
    public decimal? LeaseRentFee { get; init; }
}

public record LandTitleDto
{
    /// <summary>Used only to group titles onto their property; not meaningful to clients.</summary>
    public Guid AppraisalPropertyId { get; set; }
    public Guid? Id { get; set; }
    public string? TitleNumber { get; set; }
    public string? TitleType { get; set; }
    public string? BookNumber { get; set; }
    public string? PageNumber { get; set; }
    public string? LandParcelNumber { get; set; }
    public string? SurveyNumber { get; set; }
    public string? MapSheetNumber { get; set; }
    public string? Rawang { get; set; }
    public string? AerialMapName { get; set; }
    public string? AerialMapNumber { get; set; }
    public decimal? Rai { get; set; }
    public decimal? Ngan { get; set; }
    public decimal? SquareWa { get; set; }
    public string? BoundaryMarkerType { get; set; }
    public string? DocumentValidationResultType { get; set; }
    public decimal? GovernmentPricePerSqWa { get; set; }
    public decimal? GovernmentPrice { get; set; }
    public string? Remark { get; set; }
}