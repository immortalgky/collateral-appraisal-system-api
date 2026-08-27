namespace Appraisal.Application.Features.Appraisals.CorrectPropertyData;

/// <summary>
/// Wire contract for a data correction. Every section is optional and every field inside it is
/// nullable: omit what you are not changing.
///
/// The client is expected to send only the fields the user actually touched (react-hook-form's
/// dirtyFields), which is what makes null-means-unchanged safe end to end.
/// </summary>
public record CorrectPropertyDataRequest(
    string Reason,
    string? Description = null,
    LandCorrection? Land = null,
    IReadOnlyList<LandTitleCorrection>? LandTitles = null,
    BuildingCorrection? Building = null,
    CondoCorrection? Condo = null,
    VehicleCorrection? Vehicle = null,
    VesselCorrection? Vessel = null,
    MachineryCorrection? Machinery = null,
    LeaseAgreementCorrection? LeaseAgreement = null)
{
    public PropertyCorrectionData ToCorrectionData() => new(
        Description,
        Land,
        LandTitles,
        Building,
        Condo,
        Vehicle,
        Vessel,
        Machinery,
        LeaseAgreement);
}
