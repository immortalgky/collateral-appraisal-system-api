using System.Reflection;
using System.Text.Json;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Appraisals.Events;
using Shared.Exceptions;

namespace Appraisal.Tests.Domain;

/// <summary>
/// Covers <see cref="Appraisal.Domain.Appraisals.Appraisal.CorrectPropertyData"/> — the admin
/// data-correction path for closed appraisals.
///
/// The contract these tests lock in:
///   * null means "unchanged", "" means "clear", equal means "no-op"
///   * a partial correction NEVER wipes the fields it did not mention (the trap that
///     LandAppraisalDetail.Update would fall into)
///   * every change is recorded as { from, to } under a dotted key
///   * land titles are corrected in place, keeping their ids
/// </summary>
public class PropertyDataCorrectionTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static Appraisal.Domain.Appraisals.Appraisal CreateAppraisal() =>
        Appraisal.Domain.Appraisals.Appraisal.Create(
            requestId: Guid.NewGuid(),
            appraisalType: "New",
            priority: "Normal",
            now: new DateTime(2026, 1, 1));

    /// <summary>
    /// Entity ids are database-assigned, so in unit tests they stay Guid.Empty unless set. Assign
    /// them explicitly so property/title lookups are unambiguous.
    /// </summary>
    private static AppraisalProperty AddLandPropertyWithId(
        Appraisal.Domain.Appraisals.Appraisal appraisal)
    {
        var property = appraisal.AddLandProperty();
        property.Id = Guid.NewGuid();
        return property;
    }

    private static LandTitle AddTitle(LandAppraisalDetail landDetail, string titleNumber)
    {
        var title = LandTitle.Create(landDetail.Id, titleNumber, "โฉนดที่ดิน");
        title.Id = Guid.NewGuid();
        landDetail.AddTitle(title);
        return title;
    }

    private static PropertyCorrectionData LandOnly(LandCorrection land) =>
        new(null, land, null, null, null, null, null, null, null);

    private static LandCorrection EmptyLand() => new();

    private static LandTitleCorrection EmptyTitle(Guid titleId) => new(titleId);

    private static (string? From, string? To) ReadChange(string changedFields, string key)
    {
        using var document = JsonDocument.Parse(changedFields);
        var change = document.RootElement.GetProperty(key);
        return (
            change.GetProperty("from").ValueKind == JsonValueKind.Null
                ? null : change.GetProperty("from").ToString(),
            change.GetProperty("to").ValueKind == JsonValueKind.Null
                ? null : change.GetProperty("to").ToString());
    }

    // ---------------------------------------------------------------------------
    // No-op behaviour
    // ---------------------------------------------------------------------------

    [Fact]
    public void CorrectPropertyData_WithAllNulls_ChangesNothingAndRaisesNoEvent()
    {
        var appraisal = CreateAppraisal();
        var property = AddLandPropertyWithId(appraisal);
        property.LandDetail!.Update(propertyName: "Original", ownerName: "Owner A");
        appraisal.ClearDomainEvents();

        var outcome = appraisal.CorrectPropertyData(
            property.Id, LandOnly(EmptyLand()), "no change", "EMP001");

        Assert.Equal(0, outcome.ChangedFieldCount);
        Assert.Equal("{}", outcome.ChangedFields);
        Assert.Empty(appraisal.DomainEvents);
        Assert.Equal("Original", property.LandDetail.PropertyName);
        Assert.Equal("Owner A", property.LandDetail.OwnerName);
    }

    [Fact]
    public void CorrectPropertyData_WithSameValue_IsNotRecordedAsAChange()
    {
        var appraisal = CreateAppraisal();
        var property = AddLandPropertyWithId(appraisal);
        property.LandDetail!.Update(ownerName: "Owner A");
        appraisal.ClearDomainEvents();

        var outcome = appraisal.CorrectPropertyData(
            property.Id,
            LandOnly(EmptyLand() with { OwnerName = "Owner A" }),
            "same value",
            "EMP001");

        Assert.Equal(0, outcome.ChangedFieldCount);
        Assert.Empty(appraisal.DomainEvents);
    }

    // ---------------------------------------------------------------------------
    // The core guarantee: a partial correction must not wipe anything else
    // ---------------------------------------------------------------------------

    [Fact]
    public void CorrectPropertyData_CorrectingOneField_LeavesEveryOtherFieldIntact()
    {
        var appraisal = CreateAppraisal();
        var property = AddLandPropertyWithId(appraisal);

        // A realistic spread of fields, including ones outside the correctable set. If
        // ApplyCorrection ever delegates to LandAppraisalDetail.Update, these all become null and
        // this test fails — which is exactly the regression it exists to catch.
        property.LandDetail!.Update(
            propertyName: "Plot A",
            landDescription: "Vacant land",
            address: Address.Create("ท่าทราย", "เมืองสมุทรสาคร", "สมุทรสาคร"),
            ownerName: "Owner A",
            isOwnerVerified: true,
            street: "พระราม 2",
            soi: "44",
            village: "หมู่บ้าน ก",
            landOffice: "สำนักงานที่ดินสมุทรสาคร",
            landShapeType: "สี่เหลี่ยม",
            urbanPlanningType: "สีเหลือง");
        appraisal.ClearDomainEvents();

        var outcome = appraisal.CorrectPropertyData(
            property.Id,
            LandOnly(EmptyLand() with { OwnerName = "Owner B" }),
            "wrong owner keyed",
            "EMP001");

        Assert.Equal(1, outcome.ChangedFieldCount);

        var detail = property.LandDetail;
        Assert.Equal("Owner B", detail.OwnerName);
        Assert.Equal("Plot A", detail.PropertyName);
        Assert.Equal("Vacant land", detail.LandDescription);
        Assert.Equal("พระราม 2", detail.Street);
        Assert.Equal("44", detail.Soi);
        Assert.Equal("หมู่บ้าน ก", detail.Village);
        Assert.Equal("สำนักงานที่ดินสมุทรสาคร", detail.LandOffice);
        Assert.True(detail.IsOwnerVerified);
        // Outside the correctable set entirely — must survive untouched.
        Assert.Equal("สี่เหลี่ยม", detail.LandShapeType);
        Assert.Equal("สีเหลือง", detail.UrbanPlanningType);
        Assert.Equal("ท่าทราย", detail.Address!.SubDistrict);
    }

    [Fact]
    public void CorrectPropertyData_CorrectingOneAddressComponent_KeepsTheOthers()
    {
        var appraisal = CreateAppraisal();
        var property = AddLandPropertyWithId(appraisal);
        property.LandDetail!.Update(address: Address.Create("ท่าทราย", "เมืองสมุทรสาคร", "สมุทรสาคร"));
        appraisal.ClearDomainEvents();

        var outcome = appraisal.CorrectPropertyData(
            property.Id,
            LandOnly(EmptyLand() with { Province = "สมุทรปราการ" }),
            "wrong province",
            "EMP001");

        Assert.Equal(1, outcome.ChangedFieldCount);
        Assert.Equal("สมุทรปราการ", property.LandDetail.Address!.Province);
        Assert.Equal("ท่าทราย", property.LandDetail.Address.SubDistrict);
        Assert.Equal("เมืองสมุทรสาคร", property.LandDetail.Address.District);
    }

    // ---------------------------------------------------------------------------
    // Diff shape and the raised event
    // ---------------------------------------------------------------------------

    [Fact]
    public void CorrectPropertyData_RecordsFromAndToUnderADottedKey()
    {
        var appraisal = CreateAppraisal();
        var property = AddLandPropertyWithId(appraisal);
        property.LandDetail!.Update(ownerName: "Owner A");
        appraisal.ClearDomainEvents();

        var outcome = appraisal.CorrectPropertyData(
            property.Id,
            LandOnly(EmptyLand() with { OwnerName = "Owner B" }),
            "wrong owner",
            "EMP001");

        var (from, to) = ReadChange(outcome.ChangedFields, "Land.OwnerName");
        Assert.Equal("Owner A", from);
        Assert.Equal("Owner B", to);
    }

    [Fact]
    public void CorrectPropertyData_RaisesEventCarryingReasonActorAndDiff()
    {
        var appraisal = CreateAppraisal();
        var property = AddLandPropertyWithId(appraisal);
        property.LandDetail!.Update(ownerName: "Owner A");
        appraisal.ClearDomainEvents();

        var outcome = appraisal.CorrectPropertyData(
            property.Id,
            LandOnly(EmptyLand() with { OwnerName = "Owner B" }),
            "owner keyed from the contact person",
            "EMP001");

        var raised = Assert.Single(appraisal.DomainEvents);
        var corrected = Assert.IsType<AppraisalPropertyCorrectedEvent>(raised);

        Assert.Equal(appraisal.Id, corrected.AppraisalId);
        Assert.Equal(property.Id, corrected.PropertyId);
        Assert.Equal(PropertyType.Land.Code, corrected.PropertyType);
        Assert.Equal("owner keyed from the contact person", corrected.Reason);
        Assert.Equal("EMP001", corrected.By);
        Assert.Equal(outcome.ChangedFields, corrected.ChangedFields);
    }

    // ---------------------------------------------------------------------------
    // Clearing semantics
    // ---------------------------------------------------------------------------

    [Fact]
    public void CorrectPropertyData_EmptyStringClearsTheFieldAndIsRecorded()
    {
        var appraisal = CreateAppraisal();
        var property = AddLandPropertyWithId(appraisal);
        property.LandDetail!.Update(remark: "typed into the wrong box");
        appraisal.ClearDomainEvents();

        var outcome = appraisal.CorrectPropertyData(
            property.Id,
            LandOnly(EmptyLand() with { Remark = "" }),
            "remark belongs elsewhere",
            "EMP001");

        Assert.Equal(1, outcome.ChangedFieldCount);
        Assert.Null(property.LandDetail.Remark);

        var (from, to) = ReadChange(outcome.ChangedFields, "Land.Remark");
        Assert.Equal("typed into the wrong box", from);
        Assert.Null(to);
    }

    // ---------------------------------------------------------------------------
    // Non-nullable bool (machinery / vehicle / vessel)
    // ---------------------------------------------------------------------------

    [Fact]
    public void CorrectPropertyData_NonNullableBoolIsCorrectableAndRecorded()
    {
        var appraisal = CreateAppraisal();
        var property = appraisal.AddProperty(PropertyType.Machinery);
        property.Id = Guid.NewGuid();

        var machinery = MachineryAppraisalDetail.Create(property.Id);
        machinery.Update(machineName: "Injection moulder", isOwnerVerified: true);
        property.SetMachineryDetail(machinery);
        appraisal.ClearDomainEvents();

        var correction = new PropertyCorrectionData(
            null, null, null, null, null, null, null,
            new MachineryCorrection(
                PropertyName: null, MachineName: null, EngineNo: null, ChassisNo: null,
                RegistrationNumber: null, SerialNo: null, Brand: null, Model: null, Series: null,
                Manufacturer: null, OwnerName: null, IsOwnerVerified: false, Location: null,
                Other: null, Remark: null),
            null);

        var outcome = appraisal.CorrectPropertyData(
            property.Id, correction, "owner was never actually verified", "EMP001");

        Assert.Equal(1, outcome.ChangedFieldCount);
        Assert.False(property.MachineryDetail!.IsOwnerVerified);

        var (from, to) = ReadChange(outcome.ChangedFields, "Machinery.IsOwnerVerified");
        Assert.Equal("True", from);
        Assert.Equal("False", to);
    }

    // ---------------------------------------------------------------------------
    // Land titles
    // ---------------------------------------------------------------------------

    [Fact]
    public void CorrectPropertyData_CorrectsTitleNumberInPlaceKeepingTheTitleId()
    {
        var appraisal = CreateAppraisal();
        var property = AddLandPropertyWithId(appraisal);
        var title = AddTitle(property.LandDetail!, "44821");
        var originalId = title.Id;
        appraisal.ClearDomainEvents();

        var correction = new PropertyCorrectionData(
            null, null,
            [EmptyTitle(title.Id) with { TitleNumber = "44812" }],
            null, null, null, null, null, null);

        var outcome = appraisal.CorrectPropertyData(
            property.Id, correction, "digits transposed", "EMP001");

        Assert.Equal(1, outcome.ChangedFieldCount);
        Assert.Equal("44812", title.TitleNumber);
        // The audit trail references titles by id — a correction must never re-create the row.
        Assert.Equal(originalId, title.Id);
        Assert.Single(property.LandDetail!.Titles);

        var (from, to) = ReadChange(outcome.ChangedFields, $"Land.Title[{originalId}].TitleNumber");
        Assert.Equal("44821", from);
        Assert.Equal("44812", to);
    }

    [Fact]
    public void CorrectPropertyData_BlankTitleNumberIsIgnoredRatherThanClearingIt()
    {
        var appraisal = CreateAppraisal();
        var property = AddLandPropertyWithId(appraisal);
        var title = AddTitle(property.LandDetail!, "44821");
        appraisal.ClearDomainEvents();

        var correction = new PropertyCorrectionData(
            null, null,
            [EmptyTitle(title.Id) with { TitleNumber = "" }],
            null, null, null, null, null, null);

        var outcome = appraisal.CorrectPropertyData(
            property.Id, correction, "attempted clear", "EMP001");

        // TitleNumber is non-nullable in the model; clearing it would break the aggregate.
        Assert.Equal(0, outcome.ChangedFieldCount);
        Assert.Equal("44821", title.TitleNumber);
    }

    [Fact]
    public void CorrectPropertyData_UnknownTitleId_Throws()
    {
        var appraisal = CreateAppraisal();
        var property = AddLandPropertyWithId(appraisal);
        AddTitle(property.LandDetail!, "44821");
        appraisal.ClearDomainEvents();

        var correction = new PropertyCorrectionData(
            null, null,
            [EmptyTitle(Guid.NewGuid()) with { TitleNumber = "99999" }],
            null, null, null, null, null, null);

        // Silently ignoring this would leave the admin believing the edit was saved.
        Assert.Throws<NotFoundException>(() =>
            appraisal.CorrectPropertyData(property.Id, correction, "wrong payload", "EMP001"));
    }

    [Fact]
    public void CorrectPropertyData_CorrectsOnlyTheTargetedTitle()
    {
        var appraisal = CreateAppraisal();
        var property = AddLandPropertyWithId(appraisal);
        var first = AddTitle(property.LandDetail!, "44821");
        var second = AddTitle(property.LandDetail!, "44822");
        appraisal.ClearDomainEvents();

        var correction = new PropertyCorrectionData(
            null, null,
            [EmptyTitle(second.Id) with { BookNumber = "450" }],
            null, null, null, null, null, null);

        appraisal.CorrectPropertyData(property.Id, correction, "wrong book number", "EMP001");

        Assert.Equal("450", second.BookNumber);
        Assert.Null(first.BookNumber);
        Assert.Equal("44821", first.TitleNumber);
    }

    // ---------------------------------------------------------------------------
    // Guard rails
    // ---------------------------------------------------------------------------

    [Fact]
    public void CorrectPropertyData_UnknownPropertyId_Throws()
    {
        var appraisal = CreateAppraisal();
        AddLandPropertyWithId(appraisal);

        Assert.Throws<Appraisal.Domain.Appraisals.Exceptions.PropertyNotFoundException>(() =>
            appraisal.CorrectPropertyData(
                Guid.NewGuid(), LandOnly(EmptyLand()), "reason", "EMP001"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CorrectPropertyData_BlankReason_Throws(string reason)
    {
        var appraisal = CreateAppraisal();
        var property = AddLandPropertyWithId(appraisal);

        Assert.Throws<ArgumentException>(() =>
            appraisal.CorrectPropertyData(
                property.Id, LandOnly(EmptyLand() with { OwnerName = "X" }), reason, "EMP001"));
    }

    [Fact]
    public void CorrectPropertyData_SectionForAMissingDetail_IsIgnored()
    {
        var appraisal = CreateAppraisal();
        var property = AddLandPropertyWithId(appraisal);
        appraisal.ClearDomainEvents();

        // A condo correction sent against a land property: the client may post a generic payload.
        var correction = new PropertyCorrectionData(
            null, null, null, null,
            new CondoCorrection(
                PropertyName: null, CondoName: "Should be ignored", BuildingNumber: null,
                ModelName: null, BuiltOnTitleNumber: null, CondoRegistrationNumber: null,
                RoomNumber: null, FloorNumber: null, PhysicalFloorNumber: null, TitleNumber: null,
                TitleType: null, Latitude: null, Longitude: null, SubDistrict: null, District: null,
                Province: null, LandOffice: null, DopaSubDistrict: null, DopaDistrict: null,
                DopaProvince: null, OwnerName: null, IsOwnerVerified: null, HasObligation: null,
                ObligationDetails: null, DocumentValidationResultType: null, Street: null,
                Soi: null, Remark: null),
            null, null, null, null);

        var outcome = appraisal.CorrectPropertyData(
            property.Id, correction, "generic payload", "EMP001");

        Assert.Equal(0, outcome.ChangedFieldCount);
        Assert.Null(property.CondoDetail);
    }

    // ---------------------------------------------------------------------------
    // Coverage lock: the DTO must keep pace with the value objects
    // ---------------------------------------------------------------------------

    public static TheoryData<Type, Type> DetailAndCorrectionPairs() => new()
    {
        { typeof(LandAppraisalDetail), typeof(LandCorrection) },
        { typeof(LandTitle), typeof(LandTitleCorrection) },
        { typeof(BuildingAppraisalDetail), typeof(BuildingCorrection) },
        { typeof(CondoAppraisalDetail), typeof(CondoCorrection) },
        { typeof(VehicleAppraisalDetail), typeof(VehicleCorrection) },
        { typeof(VesselAppraisalDetail), typeof(VesselCorrection) },
        { typeof(MachineryAppraisalDetail), typeof(MachineryCorrection) },
        { typeof(LeaseAgreementDetail), typeof(LeaseAgreementCorrection) },
    };

    /// <summary>
    /// Every appraiser-authored field on a detail VO must be correctable. A property that exists on
    /// the VO but not on its correction record is silently unfixable through this feature — which is
    /// exactly the gap that prompted widening it in the first place.
    ///
    /// The immutable value objects are excluded because the DTO carries their components instead
    /// (Address → SubDistrict/District/Province, GpsCoordinate → Latitude/Longitude), and so are
    /// the child collections, which have their own add/remove APIs and are not part of a
    /// field-level correction. <c>Area</c> is excluded for a different reason — see
    /// <see cref="CorrectionDto_DoesNotExposeArea"/>.
    /// </summary>
    [Theory]
    [MemberData(nameof(DetailAndCorrectionPairs))]
    public void CorrectionDto_CoversEveryCorrectableProperty(Type detailType, Type correctionType)
    {
        var infrastructure = new[]
        {
            "Id", "AppraisalPropertyId", "LandAppraisalDetailId",
            "CreatedAt", "CreatedBy", "CreatedWorkstation",
            "UpdatedAt", "UpdatedBy", "UpdatedWorkstation",
        };
        var valueObjects = new[] { "Coordinates", "Address", "DopaAddress", "Area" };
        // Area is intentionally uncorrectable — see CorrectionDto_DoesNotExposeArea, which fails
        // if any of these ever reappears on a correction record.
        var notCorrectable = new[] { "TotalBuildingArea", "UsableArea" };

        var settable = detailType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is not null)
            .Where(p => !infrastructure.Contains(p.Name))
            .Where(p => !valueObjects.Contains(p.Name))
            .Where(p => !notCorrectable.Contains(p.Name))
            .Where(p => !typeof(System.Collections.IEnumerable).IsAssignableFrom(p.PropertyType)
                        || p.PropertyType == typeof(string)
                        || p.PropertyType == typeof(List<string>))
            .Select(p => p.Name)
            .ToList();

        var covered = correctionType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet();

        var missing = settable.Where(name => !covered.Contains(name)).ToList();

        Assert.Empty(missing);
    }

    /// <summary>
    /// Area drives every valuation on the appraisal — a title's rai/ngan/sq.wa feed the land
    /// value, the building's total area feeds the RCN and depreciation figures, and a condo's
    /// usable area feeds its government price. This feature
    /// corrects descriptive data only: it does not recompute prices and does not send the
    /// appraisal back through the workflow, so a correctable area would leave the recorded values
    /// disagreeing with their own inputs. Both were removed at the user's instruction once the
    /// field list was widened; if an area really is wrong, that is a re-appraisal.
    ///
    /// </summary>
    [Theory]
    [InlineData(typeof(LandTitleCorrection), "Rai", "Ngan", "SquareWa", "TotalSquareWa", "Area")]
    [InlineData(typeof(BuildingCorrection), "TotalBuildingArea")]
    [InlineData(typeof(CondoCorrection), "UsableArea", "TotalBuildingArea")]
    public void CorrectionDto_DoesNotExposeArea(Type correctionType, params string[] forbidden)
    {
        var exposed = correctionType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Intersect(forbidden)
            .ToList();

        Assert.Empty(exposed);
    }
}
