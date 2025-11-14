using Shared.Messaging.Events;

namespace Request.RequestTitles.Models;

public class RequestTitle : Aggregate<Guid>
{
    public Guid RequestId { get; private set; }
    public string? CollateralType { get; private set; }
    public bool? CollateralStatus { get; private set; }

    public TitleDeedInfo TitleDeedInfo { get; private set; } = default!;
    
    public SurveyInfo SurveyInfo { get; private set; } = default!;

    public LandArea LandArea { get; private set; } = default!;

    public string? OwnerName { get; private set; }
    
    public string? RegistrationNo { get; private set; } 
    
    public Vehicle Vehicle { get; private set; } = default!;
    
    public Machinery Machinery { get; private set; } = default!;
    
    public BuildingInfo BuildingInfo { get; private set; } = default!;

    public CondoInfo CondoInfo { get; private set; } = default!;

    // Address
    public Address TitleAddress { get; private set; } = default!;
    
    // Dopa Address
    public Address DopaAddress { get; private set; } = default!;

    public string? Notes { get; private set; }

    private readonly List<RequestTitleDocument> _requestTitleDocuments = [];
    public IReadOnlyList<RequestTitleDocument> RequestTitleDocuments => _requestTitleDocuments.AsReadOnly();

    private RequestTitle()
    {
        // For EF Core
    }

    public static RequestTitle Create(Guid requestId, string? collateralType, bool? collateralStatus, TitleDeedInfo titleDeedInfo, SurveyInfo surveyInfo, LandArea landArea, string? ownerName, string? registrationNo, Vehicle vehicle, Machinery machinery, BuildingInfo buildingInfo, CondoInfo condoInfo, Address titleAddress, Address dopaAddress, string? notes, List<RequestTitleDocumentDto> requestTitleDocuments)
    {
        var requestTitle = collateralType switch
        {
            "L" => CreateLand( titleDeedInfo, surveyInfo, landArea, titleAddress, dopaAddress, notes),
            "B" => CreateBuilding( buildingInfo, titleAddress, dopaAddress, notes),
            "LB" => CreateLandBuilding( titleDeedInfo,  surveyInfo, ownerName, landArea, buildingInfo, titleAddress, dopaAddress, notes),
            "C" => CreateCondo( condoInfo, buildingInfo, ownerName, titleDeedInfo, titleAddress, dopaAddress, notes),
            "V" => CreateVechicle( registrationNo, vehicle, titleAddress, dopaAddress, notes),
            "M" => CreateMachine( registrationNo, machinery, titleAddress, dopaAddress, notes),
            _ => throw new NotFoundException("Collateral Type is not valid.")
        };

        requestTitle.Id = Guid.NewGuid();
        requestTitle.RequestId = requestId;
        requestTitle.CollateralType = collateralType;
        requestTitle.CollateralStatus = collateralStatus;

        requestTitle.AddDomainEvent(new RequestTitleAddedEvent(requestId, requestTitle));

        if (requestTitle is not null)
        {
            foreach (var requestTitleDocument in requestTitleDocuments)
            {
                requestTitle.AddDocument(requestTitle.Id, requestTitleDocument.DocumentId, requestTitleDocument.DocumentType, requestTitleDocument.DocumentDescription, requestTitleDocument.IsRequired, requestTitleDocument.UploadedBy, requestTitleDocument.UploadedByName);
            }
        }
        
        // requestTitle.AddDomainEvent(new DocumentLinkedIntegrationEvent("Title", requestTitle.Id, []));
        
        return requestTitle;
    }

    public static RequestTitle CreateDraft(Guid requestId, string? collateralType, bool? collateralStatus, TitleDeedInfo titleDeedInfo, SurveyInfo surveyInfo, LandArea landArea, string? ownerName, string? registrationNo, Vehicle vehicle, Machinery machinery, BuildingInfo buildingInfo, CondoInfo condoInfo, Address titleAddress, Address dopaAddress, string? notes)
    {
        var requestTitle = new RequestTitle()
        {
            Id = Guid.NewGuid(),
            RequestId = requestId,
            CollateralType = collateralType,
            CollateralStatus = collateralStatus,
            TitleDeedInfo = titleDeedInfo,
            SurveyInfo = surveyInfo,
            LandArea = landArea,
            OwnerName = ownerName,
            RegistrationNo = registrationNo,
            Vehicle = vehicle,
            Machinery = machinery,
            BuildingInfo = buildingInfo,
            CondoInfo = condoInfo,
            TitleAddress = titleAddress,
            DopaAddress = dopaAddress,
            Notes = notes
        };

        requestTitle.AddDomainEvent(new RequestTitleAddedEvent(requestId, requestTitle));

        return requestTitle;
    }

    public void UpdateDetails(string? collateralType, bool? collateralStatus, TitleDeedInfo titleDeedInfo, SurveyInfo surveyInfo, LandArea landArea, string? ownerName, string? registrationNo, Vehicle vehicle, Machinery machinery, BuildingInfo buildingInfo, CondoInfo condoInfo, Address titleAddress, Address dopaAddress, string? notes)
    {
        switch (collateralType)
        {
            case "L": 
                ValidateLand(titleDeedInfo, surveyInfo, "-", landArea);
                break;
            case "B":
                ValidateBuilding(buildingInfo);
                break;
            case "LB":
                ValidateLand(titleDeedInfo, surveyInfo, ownerName, landArea);
                ValidateBuilding(buildingInfo);
                break;
            case "C":
                ValidateCondo(condoInfo, buildingInfo, ownerName);
                break;
            case "M":
                ValidateMachine(registrationNo, machinery);
                break;
            case "V":
                ValidateVehicle(registrationNo, vehicle);
                break;
            default:
                break;
        }

        ValidateTitleDocAddr(titleAddress);
        ValidateDopaAddr(dopaAddress);

        CollateralType = collateralType;
        CollateralStatus = collateralStatus;
        TitleDeedInfo = titleDeedInfo;
        SurveyInfo = surveyInfo;
        LandArea = landArea;
        OwnerName = ownerName;
        RegistrationNo = registrationNo;
        Vehicle = vehicle;
        Machinery = machinery;
        BuildingInfo = buildingInfo;
        CondoInfo = condoInfo;
        TitleAddress = titleAddress;
        DopaAddress = dopaAddress;
        Notes = notes;
    }

    public void UpdateDraftDetails(string? collateralType, bool? collateralStatus, TitleDeedInfo titleDeedInfo, SurveyInfo surveyInfo, LandArea landArea, string? ownerName, string? registrationNo, Vehicle vehicle, Machinery machinery, BuildingInfo buildingInfo, CondoInfo condoInfo, Address titleAddress, Address dopaAddress, string? notes)
    {
        CollateralType = collateralType;
        CollateralStatus = collateralStatus;
        TitleDeedInfo = titleDeedInfo;
        SurveyInfo = surveyInfo;
        LandArea = landArea;
        OwnerName = ownerName;
        RegistrationNo = registrationNo;
        Vehicle = vehicle;
        Machinery = machinery;
        BuildingInfo = buildingInfo;
        CondoInfo = condoInfo;
        TitleAddress = titleAddress;
        DopaAddress = dopaAddress;
        Notes = notes;
    }

    public void AddDocument(Guid titleId, Guid documentId, string documentType, string documentDescription,
        bool isRequired, string uploadedBy, string uploadedByName)
    {
        var requestTitleDocument = RequestTitleDocument.Create(titleId, documentId, documentType, documentDescription,
            isRequired, uploadedBy, uploadedByName);
        
        _requestTitleDocuments.Add(requestTitleDocument);
    }
    
    public static void ValidateTitleDocAddr(Address titleAddress)
    {
        var ruleCheck = RuleCheck.Valid();
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.SubDistrict), "SubDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.District), "District is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.Province), "Province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.Postcode), "Postcode is null or contains only whitespace.");
        ruleCheck.ThrowIfInvalid();
    }

    public static void ValidateDopaAddr(Address dopaAddress)
    {
        var ruleCheck = RuleCheck.Valid();
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.SubDistrict), "SubDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.District), "District is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.Province), "Province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.Postcode), "Postcode is null or contains only whitespace.");
        ruleCheck.ThrowIfInvalid();
    }

    public static void ValidateTitleDeedInfo(TitleDeedInfo titleDeedInfo)
    {
        var ruleCheck = RuleCheck.Valid();
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleDeedInfo.TitleNo), "titleNo");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleDeedInfo.DeedType), "deedType");

        List<string> deedTypeList = new List<string>() { "Chanote", "NorSor3", "NorSor3Kor" };
        ruleCheck.AddErrorIf(!deedTypeList.Contains(titleDeedInfo.DeedType), "deedType");

        ruleCheck.ThrowIfInvalid();
    }

    public static void ValidateSurveyInfo(SurveyInfo surveyInfo)
    {
        var ruleCheck = RuleCheck.Valid();
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(surveyInfo.Rawang), "rawang");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(surveyInfo.LandNo), "landNo");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(surveyInfo.SurveyNo), "surveyNo");

        ruleCheck.ThrowIfInvalid();
    }

    public static void ValidateLand(TitleDeedInfo titleDeedInfo, SurveyInfo surveyInfo, string? ownerName, LandArea landArea)
    {
        ValidateTitleDeedInfo(titleDeedInfo);
        ValidateSurveyInfo(surveyInfo);

        var ruleCheck = RuleCheck.Valid();
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(ownerName), "ownerName");

        ruleCheck.AddErrorIf(landArea.AreaNgan is null || landArea.AreaNgan < 0, "areaNgan must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(landArea.AreaRai is null || landArea.AreaRai < 0, "areaRai must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(landArea.AreaSquareWa is null || landArea.AreaSquareWa < 0, "areaSquareWa must be greater than or equal to 0 or not null.");

        ruleCheck.ThrowIfInvalid();
    }

    public static void ValidateBuilding(BuildingInfo buildingInfo)
    {
        var ruleCheck = RuleCheck.Valid();

        ruleCheck.AddErrorIf(string.IsNullOrEmpty(buildingInfo.BuildingType), "buildingType");
        
        ruleCheck.AddErrorIf(buildingInfo.UsableArea is null || buildingInfo.UsableArea < 0, "usableArea must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(buildingInfo.NumberOfBuilding is null || buildingInfo.NumberOfBuilding < 0, "NumberOfBuilding must be greater than or equal to 0 or not null.");

        ruleCheck.ThrowIfInvalid();
    }

    public static void ValidateMachine(string registrationNo, Machinery machinery)
    {
        var ruleCheck = RuleCheck.Valid();

        ruleCheck.AddErrorIf(string.IsNullOrEmpty(registrationNo), "RegistrationNo");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(machinery.MachineryStatus), "MachineStatus");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(machinery.MachineryType), "MachineType");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(machinery.InstallationStatus), "InstallationStatus");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(machinery.InvoiceNumber), "InvoiceNumber");

        ruleCheck.AddErrorIf(machinery.NumberOfMachinery is null || machinery.NumberOfMachinery < 0, "NumberOfMachine must be greater than or equal to 0 or not null.");

        ruleCheck.ThrowIfInvalid();
    }

    public static void ValidateVehicle(string registrationNo, Vehicle vehicle)
    {
        var ruleCheck = RuleCheck.Valid();

        ruleCheck.AddErrorIf(string.IsNullOrEmpty(registrationNo), "registrationNo");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(vehicle.VehicleType), "vehicleType");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(vehicle.VehicleAppointmentLocation), "vehicleAppointmentLocation");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(vehicle.ChassisNumber), "chassisNumber");

        ruleCheck.ThrowIfInvalid();
    }

    public static void ValidateCondo(CondoInfo condoInfo, BuildingInfo buildingInfo, string ownerName)
    {
        var ruleCheck = RuleCheck.Valid();

        ruleCheck.AddErrorIf(string.IsNullOrEmpty(condoInfo.CondoName), "condoName");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(condoInfo.BuildingNo), "buildingNo");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(condoInfo.RoomNo), "roomNo");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(condoInfo.FloorNo), "floorNo");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(ownerName), "ownerName");

        ruleCheck.AddErrorIf(buildingInfo.UsableArea is null || buildingInfo.UsableArea < 0, "usableArea must be greater than or equal to 0 or not null.");

        ruleCheck.ThrowIfInvalid();
    }

    public static RequestTitle CreateLand(TitleDeedInfo titleDeedInfo, SurveyInfo surveyInfo, LandArea landArea, Address titleAddress, Address dopaAddress, string? notes)
    {
        ValidateLand(titleDeedInfo, surveyInfo, "-", landArea);
        ValidateTitleDocAddr(titleAddress);
        ValidateDopaAddr(dopaAddress);

        return new RequestTitle() { TitleDeedInfo = titleDeedInfo, SurveyInfo = surveyInfo, LandArea = landArea, TitleAddress = titleAddress, DopaAddress = dopaAddress, Notes = notes };
    }

    public static RequestTitle CreateBuilding(BuildingInfo buildingInfo, Address titleAddress, Address dopaAddress, string? notes)
    {
        ValidateBuilding(buildingInfo);
        ValidateTitleDocAddr(titleAddress);
        ValidateDopaAddr(dopaAddress);

        return new RequestTitle() { BuildingInfo = buildingInfo, TitleAddress = titleAddress, DopaAddress = dopaAddress, Notes = notes };
    }

    public static RequestTitle CreateCondo(CondoInfo condoInfo, BuildingInfo buildingInfo, string ownerName, TitleDeedInfo titleDeedInfo, Address titleAddress, Address dopaAddress, string? notes)
    { 
        ValidateCondo(condoInfo, buildingInfo, ownerName);
        ValidateTitleDocAddr(titleAddress);
        ValidateDopaAddr(dopaAddress);

        return new RequestTitle() { CondoInfo = condoInfo, BuildingInfo = buildingInfo, OwnerName = ownerName, TitleDeedInfo = titleDeedInfo, TitleAddress = titleAddress, DopaAddress = dopaAddress, Notes = notes };
    }

    public static RequestTitle CreateVechicle(string registrationNo, Vehicle vehicle, Address titleAddress, Address dopaAddress, string? notes)
    {
        ValidateVehicle(registrationNo, vehicle);
        ValidateTitleDocAddr(titleAddress);
        ValidateDopaAddr(dopaAddress);

        return new RequestTitle() { RegistrationNo = registrationNo, Vehicle = vehicle, TitleAddress = titleAddress, DopaAddress = dopaAddress, Notes = notes };
    }

    public static RequestTitle CreateMachine(string registrationNo, Machinery machinery, Address titleAddress, Address dopaAddress, string? notes)
    {
        ValidateMachine(registrationNo, machinery);
        ValidateTitleDocAddr(titleAddress);
        ValidateDopaAddr(dopaAddress);

        return new RequestTitle() { RegistrationNo = registrationNo, Machinery = machinery, TitleAddress = titleAddress, DopaAddress = dopaAddress, Notes = notes };
    }

    public static RequestTitle CreateLandBuilding(TitleDeedInfo titleDeedInfo, SurveyInfo surveyInfo, string ownerName, LandArea landArea, BuildingInfo buildingInfo, Address titleAddress, Address dopaAddress, string? notes)
    {
        ValidateLand(titleDeedInfo, surveyInfo, ownerName, landArea);
        ValidateBuilding(buildingInfo);
        ValidateTitleDocAddr(titleAddress);
        ValidateDopaAddr(dopaAddress);

        return new RequestTitle() { TitleDeedInfo = titleDeedInfo, SurveyInfo = surveyInfo, LandArea = landArea, BuildingInfo = buildingInfo, TitleAddress = titleAddress, DopaAddress = dopaAddress, Notes = notes };
    }
}