namespace Request.RequestTitles.Models;

public class RequestTitle : Aggregate<Guid>
{
    public Guid RequestId { get; private set; }
    public string? CollateralType { get; private set; }
    public bool? CollateralStatus { get; private set; }

    // == Title Deed Information ==
    public string? TitleNo { get; private set; }
    public string? DeedType { get; private set; }
    public string? TitleDetail { get; private set; }
    
    // == Survey Information ==
    public string? Rawang { get; private set; }
    public string? LandNo { get; private set; }
    public string? SurveyNo { get; private set; }

    // AreaRai, AreaNgan, AreaSquareWa
    public LandArea LandArea { get; private set; } = default!;

    // == Ownership ==
    public string? OwnerName { get; private set; }
    
    // == Vehicle/Vessel/ Machine ==
    public string? RegistrationNo { get; private set; } 
    // Vehicle Type
    public Vehicle Vehicle { get; private set; } = default!;
    // MachineStatus, MachineType, MachineRegistrationStatus, MachineInvoiceNo, NoOfMachine
    public Machine Machine { get; private set; } = default!;
    
    // == Building Information ==
    public string? BuildingType { get; private set; }
    public decimal? UsableArea { get; private set; }
    public int? NoOfBuilding { get; private set; }

    //  == Condo Information ==
    public Condo Condo { get; private set; } = default!;

    // Address
    public Address TitleAddress { get; private set; } = default!;
    
    // Dopa Address
    public Address DopaAddress { get; private set; } = default!;

    public string? Notes { get; private set; }

    public Requests.Models.Request Request { get; private set; } = default!;

    private RequestTitle()
    {
        // For EF Core
    }

    public static RequestTitle Create(Guid requestId, string? collateralType, bool? collateralStatus, string? titleNo, string? deedType, string? titleDetail, string? rawang, string? landNo, string? surveyNo, LandArea landArea, string? ownerName, string? registrationNo, Vehicle vehicle, Machine machine, string? buildingType, decimal? usableArea,  int? noOfBuilding, Condo condo, Address titleAddress, Address dopaAddress, string? notes)
    {
        var requestTitle = collateralType switch
        {
            "L" => CreateLand(titleNo, deedType, titleDetail, rawang, landNo, surveyNo, landArea, titleAddress, dopaAddress, notes),
            "B" => CreateBuilding(buildingType, usableArea, noOfBuilding, titleAddress, dopaAddress, notes),
            "LB" => CreateLandBuilding(  titleNo,  deedType,  titleDetail,  rawang,  landNo,  surveyNo, ownerName,landArea, buildingType, usableArea, noOfBuilding, titleAddress, dopaAddress, notes),
            "C" => CreateCondo(condo, usableArea, ownerName, titleDetail, titleAddress, dopaAddress, notes),
            "V" => CreateVechicle(registrationNo, vehicle, titleAddress, dopaAddress, notes),
            "M" => CreateMachine(registrationNo, machine, titleAddress, dopaAddress, notes),
            _ => throw new NotFoundException("Collateral Type is not valid.")
        };

        requestTitle.Id = Guid.NewGuid();
        requestTitle.RequestId = requestId;
        requestTitle.CollateralType = collateralType;
        requestTitle.CollateralStatus = collateralStatus;

        requestTitle.AddDomainEvent(new RequestTitleAddedEvent(requestId, requestTitle));

        return requestTitle;
    }

    public static RequestTitle CreateDraft(Guid requestId, string? collateralType, bool? collateralStatus, string? titleNo, string? deedType, string? titleDetail, string? rawang, string? landNo, string? surveyNo, LandArea landArea, string? ownerName, string? registrationNo, Vehicle vehicle, Machine machine, string? buildingType, decimal? usableArea,  int? noOfBuilding, Condo condo, Address titleAddress, Address dopaAddress, string? notes)
    {
        var requestTitle = new RequestTitle()
        {
            Id = Guid.NewGuid(),
            RequestId = requestId,
            CollateralType = collateralType,
            CollateralStatus = collateralStatus,
            TitleNo = titleNo,
            DeedType = deedType,
            TitleDetail = titleDetail,
            Rawang = rawang,
            LandNo = landNo,
            SurveyNo = surveyNo,
            LandArea = landArea,
            OwnerName = ownerName,
            RegistrationNo = registrationNo,
            Vehicle = vehicle,
            Machine = machine,
            BuildingType = buildingType,
            UsableArea = usableArea,
            NoOfBuilding = noOfBuilding,
            Condo = condo,
            TitleAddress = titleAddress,
            DopaAddress = dopaAddress,
            Notes = notes
        };

        requestTitle.AddDomainEvent(new RequestTitleAddedEvent(requestId, requestTitle));

        return requestTitle;
    }

    public void UpdateDetails(string? collateralType, bool? collateralStatus, string? titleNo, string? deedType, string? titleDetail, string? rawang, string? landNo, string? surveyNo, LandArea landArea, string? ownerName, string? registrationNo, Vehicle vehicle, Machine machine, string? buildingType, decimal? usableArea,  int? noOfBuilding, Condo condo, Address titleAddress, Address dopaAddress, string? notes)
    {
        switch (collateralType)
        {
            case "L": 
                ValidateLand(titleNo, deedType, rawang, landNo, surveyNo, "-", landArea);
                break;
            case "B":
                ValidateBuilding(buildingType, usableArea, noOfBuilding);
                break;
            case "LB":
                ValidateLand(titleNo, deedType, rawang, landNo, surveyNo, ownerName, landArea);
                ValidateBuilding(buildingType, usableArea, noOfBuilding);
                break;
            case "C":
                ValidateCondo(condo, usableArea, ownerName);
                break;
            case "M":
                ValidateMachine(registrationNo, machine);
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
        TitleNo = titleNo;
        DeedType = deedType;
        TitleDetail = titleDetail;
        Rawang = rawang;
        LandNo = landNo;
        SurveyNo = surveyNo;
        LandArea = landArea;
        OwnerName = ownerName;
        RegistrationNo = registrationNo;
        Vehicle = vehicle;
        Machine = machine;
        BuildingType = buildingType;
        UsableArea = usableArea;
        NoOfBuilding = noOfBuilding;
        Condo = condo;
        TitleAddress = titleAddress;
        DopaAddress = dopaAddress;
        Notes = notes;
    }

    public void UpdateDraftDetails(string? collateralType, bool? collateralStatus, string? titleNo, string? deedType, string? titleDetail, string? rawang, string? landNo, string? surveyNo, LandArea landArea, string? ownerName, string? registrationNo, Vehicle vehicle, Machine machine, string? buildingType, decimal? usableArea,  int? noOfBuilding, Condo condo, Address titleAddress, Address dopaAddress, string? notes)
    {
        CollateralType = collateralType;
        CollateralStatus = collateralStatus;
        TitleNo = titleNo;
        DeedType = deedType;
        TitleDetail = titleDetail;
        Rawang = rawang;
        LandNo = landNo;
        SurveyNo = surveyNo;
        LandArea = landArea;
        OwnerName = ownerName;
        RegistrationNo = registrationNo;
        Vehicle = vehicle;
        Machine = machine;
        BuildingType = buildingType;
        UsableArea = usableArea;
        NoOfBuilding = noOfBuilding;
        Condo = condo;
        TitleAddress = titleAddress;
        DopaAddress = dopaAddress;
        Notes = notes;
    }
    
    public bool HasSameContentAs(string collateralType, bool? collateralStatus, string? titleNo, string? deedType, string? titleDetail, string? rawang, string? landNo, string? surveyNo, LandArea landArea, string? ownerName, string? registrationNo, Vehicle vehicle, Machine machine, string? buildingType, decimal? usableArea,  int? noOfBuilding, Condo condo, Address titleAddress, Address dopaAddress, string? notes)
    {
        bool checkCollateralType = CollateralType == collateralType;
        bool checkCollateralStatus = CollateralStatus == collateralStatus;
        bool checkTitleNo = TitleNo == titleNo;
        bool checkDeedType = DeedType == deedType;
        bool checkTitleDetail = TitleDetail == titleDetail;
        bool checkRawang = Rawang == rawang;
        bool checkLandNo = LandNo == landNo;
        bool checkSurveyNo = SurveyNo == surveyNo;
        bool checkLandArea = LandArea.ToString() == landArea.ToString();
        bool checkOwnerName = OwnerName == ownerName;
        bool checkRegistrationNo = RegistrationNo == registrationNo;
        bool checkVehicle = Vehicle.ToString() == vehicle.ToString();
        bool checkMachine = Machine.ToString() == machine.ToString();
        bool checkBuildingType = BuildingType == buildingType;
        bool checkUsableArea = UsableArea == usableArea;
        bool checkNoOfBuilding = NoOfBuilding == noOfBuilding;
        bool checkCondo = Condo.ToString() == condo.ToString();
        bool checkTitleAddress = TitleAddress.ToString() == titleAddress.ToString();
        bool checkDopaAddress = DopaAddress.ToString() == dopaAddress.ToString();
        bool checkNotes = Notes == notes;
        
        return checkCollateralType && checkCollateralStatus && checkTitleNo && checkDeedType && checkTitleDetail &&
               checkRawang && checkLandNo && checkSurveyNo && checkLandArea && checkOwnerName && checkVehicle &&
               checkRegistrationNo && checkMachine && checkBuildingType && checkUsableArea && checkNoOfBuilding &&
               checkCondo && checkTitleAddress && checkDopaAddress && checkNotes;
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

    public static void ValidateLand(string titleNo, string deedType, string rawang, string landNo, string surveyNo, string? ownerName, LandArea landArea)
    {
        var ruleCheck = RuleCheck.Valid();

        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleNo), "titleNo");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(deedType), "deedType");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(rawang), "rawang");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(landNo), "landNo");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(surveyNo), "surveyNo");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(ownerName), "ownerName");

        ruleCheck.AddErrorIf(landArea.AreaNgan < 0, "AreaNgan must be greater than or equal to 0.");
        ruleCheck.AddErrorIf(landArea.AreaRai < 0, "AreaRai must be greater than or equal to 0.");
        ruleCheck.AddErrorIf(landArea.AreaSquareWa < 0, "AreaSquareWa must be greater than or equal to 0.");

        List<string> deedTypeList = new List<string>() { "Chanote", "NorSor3", "NorSor3Kor" };
        ruleCheck.AddErrorIf(!deedTypeList.Contains(deedType), "deedType");

        ruleCheck.ThrowIfInvalid();
    }

    public static void ValidateBuilding(string buildingType, decimal? usableArea, int? noOfBuilding)
    {
        var ruleCheck = RuleCheck.Valid();

        ruleCheck.AddErrorIf(string.IsNullOrEmpty(buildingType), "buildingType");
        ruleCheck.AddErrorIf(usableArea < 0, "usableArea must be greater than or equal to 0.");
        ruleCheck.AddErrorIf(noOfBuilding < 0, "noOfBuilding must be greater than or equal to 0.");

        ruleCheck.ThrowIfInvalid();
    }

    public static void ValidateMachine(string registrationNo, Machine machine)
    {
        var ruleCheck = RuleCheck.Valid();

        ruleCheck.AddErrorIf(string.IsNullOrEmpty(registrationNo), "RegistrationNo");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(machine.MachineStatus), "MachineStatus");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(machine.MachineType), "MachineType");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(machine.InstallationStatus), "InstallationStatus");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(machine.InvoiceNumber), "InvoiceNumber");

        ruleCheck.AddErrorIf(machine.NumberOfMachinery < 0, "NumberOfMachine");

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

    public static void ValidateCondo(Condo condo, decimal? usableArea, string ownerName)
    {
        var ruleCheck = RuleCheck.Valid();

        ruleCheck.AddErrorIf(string.IsNullOrEmpty(condo.CondoName), "condoName");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(condo.BuildingNo), "buildingNo");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(condo.RoomNo), "roomNo");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(condo.FloorNo), "floorNo");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(ownerName), "ownerName");

        ruleCheck.AddErrorIf(usableArea < 0, "usableArea must be greater than or equal to 0.");

        ruleCheck.ThrowIfInvalid();
    }

    public static RequestTitle CreateLand(string titleNo, string deedType, string titleDetail, string rawang, string landNo, string surveyNo, LandArea landArea, Address titleAddress, Address dopaAddress, string? notes)
    {
        ValidateLand(titleNo, deedType, rawang, landNo, surveyNo, "-", landArea);
        ValidateTitleDocAddr(titleAddress);
        ValidateDopaAddr(dopaAddress);

        return new RequestTitle() { TitleNo = titleNo, DeedType = deedType, TitleDetail = titleDetail, Rawang = rawang, LandNo = landNo, SurveyNo = surveyNo, LandArea = landArea, TitleAddress = titleAddress, DopaAddress = dopaAddress, Notes = notes };
    }

    public static RequestTitle CreateBuilding(string buildingType, decimal? usableArea, int? noOfBuilding, Address titleAddress, Address dopaAddress, string? notes)
    {
        ValidateBuilding(buildingType, usableArea, noOfBuilding);
        ValidateTitleDocAddr(titleAddress);
        ValidateDopaAddr(dopaAddress);

        return new RequestTitle() { BuildingType = buildingType, UsableArea = usableArea, NoOfBuilding = noOfBuilding, TitleAddress = titleAddress, DopaAddress = dopaAddress, Notes = notes };
    }

    public static RequestTitle CreateCondo(Condo condo, decimal? usableArea, string ownerName, string titleDetail, Address titleAddress, Address dopaAddress, string? notes)
    { 
        ValidateCondo(condo, usableArea, ownerName);
        ValidateTitleDocAddr(titleAddress);
        ValidateDopaAddr(dopaAddress);

        return new RequestTitle() { Condo = condo, UsableArea = usableArea, OwnerName = ownerName, TitleDetail = titleDetail, TitleAddress = titleAddress, DopaAddress = dopaAddress, Notes = notes };
    }

    public static RequestTitle CreateVechicle(string registrationNo, Vehicle vehicle, Address titleAddress, Address dopaAddress, string? notes)
    {
        ValidateVehicle(registrationNo, vehicle);
        ValidateTitleDocAddr(titleAddress);
        ValidateDopaAddr(dopaAddress);

        return new RequestTitle() { RegistrationNo = registrationNo, Vehicle = vehicle, TitleAddress = titleAddress, DopaAddress = dopaAddress, Notes = notes };
    }

    public static RequestTitle CreateMachine(string registrationNo, Machine machine, Address titleAddress, Address dopaAddress, string? notes)
    {
        ValidateMachine(registrationNo, machine);
        ValidateTitleDocAddr(titleAddress);
        ValidateDopaAddr(dopaAddress);

        return new RequestTitle() { RegistrationNo = registrationNo, Machine = machine, TitleAddress = titleAddress, DopaAddress = dopaAddress, Notes = notes };
    }

    public static RequestTitle CreateLandBuilding(string titleNo, string deedType, string titleDetail, string rawang, string landNo, string surveyNo, string ownerName, LandArea landArea, string buildingType, decimal? usableArea, int? noOfBuilding, Address titleAddress, Address dopaAddress, string? notes)
    {
        ValidateLand(titleNo, deedType, rawang, landNo, surveyNo, ownerName, landArea);
        ValidateBuilding(buildingType, usableArea, noOfBuilding);
        ValidateTitleDocAddr(titleAddress);
        ValidateDopaAddr(dopaAddress);

        return new RequestTitle() { TitleNo = titleNo, DeedType = deedType, TitleDetail = titleDetail, Rawang = rawang, LandNo = landNo, SurveyNo = surveyNo, LandArea = landArea, BuildingType = buildingType, UsableArea = usableArea, NoOfBuilding = noOfBuilding, TitleAddress = titleAddress, DopaAddress = dopaAddress, Notes = notes };
    }
}