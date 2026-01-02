using Request.Domain.Requests;
using Request.Domain.RequestTitles;
using Request.Domain.RequestTitles.TitleTypes;
using Request.Tests.TestData;
using Shared.Exceptions;

namespace Request.Tests.Request.Requests.Models;

public class RequestTitleTests
{
    [Fact]
    public void TitleLand_Create_MapsExpectedFields()
    {
        // Arrange
        var data = new RequestTitleDataBuilder()
            .WithCollateralType("L")
            .Build();

        // Act
        var result = TitleLand.Create(data);

        // Assert
        var title = Assert.IsType<TitleLand>(result);
        Assert.Equal(data.CollateralType, title.CollateralType);
        Assert.Equal(data.CollateralStatus, title.CollateralStatus);
        Assert.Equal(data.TitleDeedInfo, title.TitleDeedInfo);
        Assert.Equal(data.LandLocationInfo, title.LandLocationInfo);
        Assert.Equal(data.LandArea, title.LandArea);
        Assert.Equal(data.TitleAddress, title.TitleAddress);
        Assert.Equal(data.DopaAddress, title.DopaAddress);
        Assert.Equal(data.Notes, title.Notes);
        Assert.Equal(data.OwnerName, title.OwnerName);
    }
}

public class RequestTitleDataBuilder
{
    public Guid _requestId = Guid.NewGuid();
    public string _collateralType = "L";
    public bool _collateralStatus = true;
    public TitleDeedInfo _titleDeedInfo = TitleDeedInfo.Create("123", "Chanote");
    public LandLocationInfo _landLocationInfo = LandLocationInfo.Create("rawang", "landNo", "surveyNo");
    public LandArea _landArea = LandArea.Of(1, 2, 3);
    public string _ownerName = "Owner";
    public VehicleInfo _vehicleInfo = VehicleInfo.Create("Car", "Location", "VIN123", "ABC-1234");
    public VesselInfo _vesselInfo = VesselInfo.Create("Boat", "Marina", "HIN123", "VES-001");
    public MachineInfo _machineInfo = MachineInfo.Create(true, "REG123", "Type", "Installed", "INV123", 1);
    public BuildingInfo _buildingInfo = BuildingInfo.Create("Type", 100, 1);
    public CondoInfo _condoInfo = CondoInfo.Create("CondoName", "B1", "101", "10");

    public Address _titleAddress = Address.Create(new AddressData(
        "house", "proj", "moo", "soi", "road", "sub", "dist", "prov", "50000"));

    public Address _dopaAddress = Address.Create(new AddressData(
        "house", "proj", "moo", "soi", "road", "sub", "dist", "prov", "50000"));

    public string _notes = "notes";

    public RequestTitleData Build()
    {
        return new RequestTitleData
        {
            RequestId = _requestId,
            CollateralType = _collateralType,
            CollateralStatus = _collateralStatus,
            TitleDeedInfo = _titleDeedInfo,
            LandLocationInfo = _landLocationInfo,
            LandArea = _landArea,
            OwnerName = _ownerName,
            VehicleInfo = _vehicleInfo,
            VesselInfo = _vesselInfo,
            MachineInfo = _machineInfo,
            BuildingInfo = _buildingInfo,
            CondoInfo = _condoInfo,
            TitleAddress = _titleAddress,
            DopaAddress = _dopaAddress,
            Notes = _notes
        };
    }

    public RequestTitleDataBuilder WithCollateralType(string type)
    {
        _collateralType = type;
        return this;
    }

    // add WithXxx(...) methods as needed for tests
}