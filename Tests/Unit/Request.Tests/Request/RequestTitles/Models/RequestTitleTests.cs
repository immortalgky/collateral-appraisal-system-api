using Request.Requests.ValueObjects;
using Request.RequestTitles.Models;
using Request.RequestTitles.ValueObjects;
using Request.Tests.TestData;
using Shared.Exceptions;

namespace Request.Tests.Request.RequestTitles.Models;

public class RequestTitleTests
{
    
}

public class RequestTitleDataBuilder
{
  public Guid _requestId = Guid.NewGuid();
  public string _collateralType = "Land";
  public bool _collateralStatus = true;
  public TitleDeedInfo _titleDeedInfo = new("123", "Chanote", "detail");
  public SurveyInfo _surveyInfo = new("rawang", "landNo", "surveyNo");
  public LandArea _landArea = new(1, 2, 3);
  public string _ownerName = "Owner";
  public string _registrationNo = "REG123";
  public VehicleInfo _vehicleInfo = new("Car", "Location", "CHASIS");
  public MachineInfo _machineInfo = new("Status", "Type", "Installed", "INV123", 1);
  public BuildingInfo _buildingInfo = new("Type", 100, 1);
  public CondoInfo _condoInfo = new("CondoName", "B1", "101", "10");
  public Address _titleAddress = new("house", "proj", "moo", "soi", "road", "sub", "dist", "prov", "50000");
  public Address _dopaAddress = new("house", "proj", "moo", "soi", "road", "sub", "dist", "prov", "50000");
  public string _notes = "notes";

  public RequestTitleData Build() => new()
  {
    RequestId = _requestId,
    CollateralType = _collateralType,
    CollateralStatus = _collateralStatus,
    TitleDeedInfo = _titleDeedInfo,
    SurveyInfo = _surveyInfo,
    LandArea = _landArea,
    OwnerName = _ownerName,
    RegistrationNo = _registrationNo,
    VehicleInfo = _vehicleInfo,
    MachineInfo = _machineInfo,
    BuildingInfo = _buildingInfo,
    CondoInfo = _condoInfo,
    TitleAddress = _titleAddress,
    DopaAddress = _dopaAddress,
    Notes = _notes
  };

  public RequestTitleDataBuilder WithCollateralType(string type)
  {
    _collateralType = type;
    return this;
  }

  // add WithXxx(...) methods as needed for tests
}