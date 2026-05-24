using Appraisal.Domain.MarketComparables;

namespace Appraisal.Tests.Domain.MarketComparables;

/// <summary>
/// Unit tests for the geo-location additions to MarketComparable.
/// Covers Task #1 of the History Search feature:
///   - Create factory accepts valid lat/lon and CreatedByCompanyId
///   - Save method updates lat/lon
///   - Null lat/lon are preserved as null
/// </summary>
public class MarketComparableGeoTests
{
    // ── Create factory ────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidLatLon_StampsCoordinates()
    {
        // Arrange
        const decimal lat = 13.756331m;
        const decimal lon = 100.501765m;

        // Act
        var mc = MarketComparable.Create(
            propertyType: "Land",
            surveyName: "Test Survey",
            infoDateTime: null,
            sourceInfo: null,
            latitude: lat,
            longitude: lon);

        // Assert
        Assert.Equal(lat, mc.Latitude);
        Assert.Equal(lon, mc.Longitude);
    }

    [Fact]
    public void Create_WithNullLatLon_LeavesCoordinatesNull()
    {
        // Act
        var mc = MarketComparable.Create(
            propertyType: "Land",
            surveyName: "Test Survey",
            infoDateTime: null,
            sourceInfo: null,
            latitude: null,
            longitude: null);

        // Assert
        Assert.Null(mc.Latitude);
        Assert.Null(mc.Longitude);
    }

    [Fact]
    public void Create_WithCreatedByCompanyId_StampsCompany()
    {
        // Arrange
        var companyId = Guid.NewGuid();

        // Act
        var mc = MarketComparable.Create(
            propertyType: "Land",
            surveyName: "Test Survey",
            infoDateTime: null,
            sourceInfo: null,
            createdByCompanyId: companyId);

        // Assert
        Assert.Equal(companyId, mc.CreatedByCompanyId);
    }

    [Fact]
    public void Create_ForInternalUser_LeavesCreatedByCompanyIdNull()
    {
        // Internal bank users have no company — CompanyId claim is null.
        var mc = MarketComparable.Create(
            propertyType: "Land",
            surveyName: "Test Survey",
            infoDateTime: null,
            sourceInfo: null,
            createdByCompanyId: null);

        Assert.Null(mc.CreatedByCompanyId);
    }

    [Fact]
    public void Create_GeneratesNewGuid_NotEmpty()
    {
        var mc = MarketComparable.Create("Land", "S", null, null);
        Assert.NotEqual(Guid.Empty, mc.Id);
    }

    // ── Save method ──────────────────────────────────────────────────────────

    [Fact]
    public void Save_UpdatesLatLon()
    {
        // Arrange
        var mc = MarketComparable.Create("Land", "Survey", null, null, latitude: 13m, longitude: 100m);
        const decimal newLat = 14.0m;
        const decimal newLon = 101.0m;

        // Act
        mc.Save(new MarketComparable.MarketComparableUpdateData(
            SurveyName: "Updated Survey",
            InfoDateTime: null,
            SourceInfo: null,
            TemplateId: null,
            Notes: null,
            Latitude: newLat,
            Longitude: newLon));

        // Assert
        Assert.Equal(newLat, mc.Latitude);
        Assert.Equal(newLon, mc.Longitude);
    }

    [Fact]
    public void Save_NullLatLon_ClearsCoordinates()
    {
        // Arrange — start with coords set
        var mc = MarketComparable.Create("Land", "Survey", null, null, latitude: 13m, longitude: 100m);

        // Act — save without coords
        mc.Save(new MarketComparable.MarketComparableUpdateData(
            SurveyName: "Survey",
            InfoDateTime: null,
            SourceInfo: null,
            TemplateId: null,
            Notes: null,
            Latitude: null,
            Longitude: null));

        // Assert
        Assert.Null(mc.Latitude);
        Assert.Null(mc.Longitude);
    }

    // ── Range validation ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(-90.1)]
    [InlineData(90.1)]
    [InlineData(180)]   // way out of range
    public void Create_WithLatitudeOutOfRange_Throws(double badLat)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            MarketComparable.Create(
                propertyType: "Land",
                surveyName: "Test",
                infoDateTime: null,
                sourceInfo: null,
                latitude: (decimal)badLat,
                longitude: 100m));
        Assert.Equal("latitude", ex.ParamName);
    }

    [Theory]
    [InlineData(-180.1)]
    [InlineData(180.1)]
    [InlineData(360)]
    public void Create_WithLongitudeOutOfRange_Throws(double badLon)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            MarketComparable.Create(
                propertyType: "Land",
                surveyName: "Test",
                infoDateTime: null,
                sourceInfo: null,
                latitude: 13m,
                longitude: (decimal)badLon));
        Assert.Equal("longitude", ex.ParamName);
    }

    [Fact]
    public void Save_WithLatitudeOutOfRange_Throws()
    {
        var mc = MarketComparable.Create("Land", "Survey", null, null);
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            mc.Save(new MarketComparable.MarketComparableUpdateData(
                SurveyName: "Survey",
                InfoDateTime: null,
                SourceInfo: null,
                TemplateId: null,
                Notes: null,
                Latitude: 91m,
                Longitude: 100m)));
        Assert.Equal("latitude", ex.ParamName);
    }

    [Theory]
    [InlineData(-90)]    // boundary
    [InlineData(90)]     // boundary
    [InlineData(0)]
    public void Create_WithBoundaryLatitude_Succeeds(double goodLat)
    {
        var mc = MarketComparable.Create(
            propertyType: "Land",
            surveyName: "Test",
            infoDateTime: null,
            sourceInfo: null,
            latitude: (decimal)goodLat,
            longitude: 0m);
        Assert.Equal((decimal)goodLat, mc.Latitude);
    }

    [Fact]
    public void Save_DoesNotModifyCreatedByCompanyId()
    {
        // CreatedByCompanyId is stamped once on creation and never changed by Save.
        var companyId = Guid.NewGuid();
        var mc = MarketComparable.Create("Land", "Survey", null, null, createdByCompanyId: companyId);

        mc.Save(new MarketComparable.MarketComparableUpdateData(
            SurveyName: "Updated",
            InfoDateTime: null,
            SourceInfo: null,
            TemplateId: null,
            Notes: null));

        Assert.Equal(companyId, mc.CreatedByCompanyId);
    }
}
