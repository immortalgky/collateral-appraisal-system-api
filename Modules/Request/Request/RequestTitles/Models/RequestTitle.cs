namespace Request.RequestTitles.Models;

public class RequestTitle : Aggregate<Guid>
{
    public Guid RequestId { get; private set; }
    public string? CollateralType { get; private set; } = default!;
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
    public Condo Condo { get; private set; }

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

    public static RequestTitle CreateLand(string collateralType, bool? collateralStatus, string titleNo, string deedType, string titleDetail, string rawang, string landNo, string surveyNo, LandArea landArea, Address titleAddress, Address dopaAddress, string? notes)
    {
        var ruleCheck = RuleCheck.Valid();

        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleNo), $"{nameof(titleNo)}");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(deedType), $"{nameof(deedType)}");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(rawang), $"{nameof(rawang)}");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(landNo), $"{nameof(landNo)}");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(surveyNo), $"{nameof(surveyNo)}");
        
        ruleCheck.AddErrorIf(landArea.AreaNgan < 0, "AreaNgan must be greater than or equal to 0.");
        ruleCheck.AddErrorIf(landArea.AreaRai < 0, "AreaRai must be greater than or equal to 0.");
        ruleCheck.AddErrorIf(landArea.AreaSquareWa < 0, "AreaSquareWa must be greater than or equal to 0.");
        
        // titleAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.SubDistrict), "SubDistrict");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.District), "District");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.Province), "Province");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.Postcode), "Postcode");

        // dopaAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.SubDistrict), "SubDistrict");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.District), "District");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.Province), "Province");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.Postcode), "Postcode");

        List<string> deedTypeList = new List<string>() { "Chanote", "NorSor3", "NorSor3Kor" };
        ruleCheck.AddErrorIf(!deedTypeList.Contains(deedType), "DeedType");

        ruleCheck.ThrowIfInvalid();

        return new RequestTitle()
        {
            CollateralType = collateralType,
            CollateralStatus = collateralStatus,
            TitleNo = titleNo,
            DeedType = deedType,
            TitleDetail = titleDetail,
            Rawang = rawang,
            LandNo = landNo,
            SurveyNo = surveyNo,
            LandArea = landArea,
            TitleAddress = titleAddress,
            DopaAddress = dopaAddress,
            Notes = notes
        };
    }

    public static RequestTitle CreateBuilding(string collateralType, bool? collateralStatus, string buildingType, decimal? usableArea, int? noOfBuilding, Address titleAddress, Address dopaAddress, string? notes)
    {
        var ruleCheck = RuleCheck.Valid();

        ruleCheck.AddErrorIf(string.IsNullOrEmpty(buildingType), "BuildingType");
        ruleCheck.AddErrorIf(usableArea < 0, "UsableArea must be greater than or equal to 0.");
        ruleCheck.AddErrorIf(noOfBuilding < 0, "NoOfBuilding must be greater than or equal to 0.");
        
        // titleAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.SubDistrict), "SubDistrict");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.District), "District");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.Province), "Province");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.Postcode), "Postcode");

        // dopaAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.SubDistrict), "SubDistrict");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.District), "District");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.Province), "Province");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.Postcode), "Postcode");

        ruleCheck.ThrowIfInvalid();

        return new RequestTitle()
        {
            CollateralType = collateralType,
            CollateralStatus = collateralStatus,
            BuildingType = buildingType,
            UsableArea = usableArea,
            NoOfBuilding = noOfBuilding,
            TitleAddress = titleAddress,
            DopaAddress = dopaAddress,
            Notes = notes
        };
    }

    public static RequestTitle CreateCondo(string collateralType, bool? collateralStatus, Condo condo, decimal? usableArea, string ownerName, string titleDetail, Address titleAddress, Address dopaAddress, string? notes)
    {
        var ruleCheck = RuleCheck.Valid();

        ruleCheck.AddErrorIf(string.IsNullOrEmpty(condo.CondoName), "CondoName");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(condo.BuildingNo), "BuildingNo");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(condo.RoomNo), "RoomNo");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(condo.FloorNo), "FloorNo");

        ruleCheck.AddErrorIf(usableArea < 0, "UsableArea must be greater than or equal to 0.");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(ownerName), "OwnerName");
        
        // titleAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.SubDistrict), "SubDistrict");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.District), "District");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.Province), "Province");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.Postcode), "Postcode");

        // dopaAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.SubDistrict), "SubDistrict");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.District), "District");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.Province), "Province");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.Postcode), "Postcode");

        ruleCheck.ThrowIfInvalid();

        return new RequestTitle()
        {
            CollateralType = collateralType,
            CollateralStatus = collateralStatus,
            Condo = condo,
            UsableArea = usableArea,
            OwnerName = ownerName,
            TitleDetail = titleDetail,
            TitleAddress = titleAddress,
            DopaAddress = dopaAddress,
            Notes = notes
        };
    }

    public static RequestTitle CreateVechicle(string collateralType, bool? collateralStatus, string registrationNo, Vehicle vehicle, Address titleAddress, Address dopaAddress, string? notes)
    {
        var ruleCheck = RuleCheck.Valid();

        ruleCheck.AddErrorIf(string.IsNullOrEmpty(registrationNo), "RegistrationNo");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(vehicle.VehicleType), "VehicleType");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(vehicle.VehicleAppointmentLocation), "VehicleAppointmentLocation");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(vehicle.ChassisNumber), "ChassisNumber");

        // titleAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.SubDistrict), "SubDistrict");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.District), "District");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.Province), "Province");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.Postcode), "Postcode");

        // dopaAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.SubDistrict), "SubDistrict");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.District), "District");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.Province), "Province");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.Postcode), "Postcode");

        ruleCheck.ThrowIfInvalid();

        return new RequestTitle()
        {
            CollateralType = collateralType,
            CollateralStatus = collateralStatus,
            RegistrationNo = registrationNo,
            Vehicle = vehicle,
            TitleAddress = titleAddress,
            DopaAddress = dopaAddress,
            Notes = notes
        };
    }

    public static RequestTitle CreateMachine(string collateralType, bool? collateralStatus, string registrationNo, Machine machine, Address titleAddress, Address dopaAddress, string? notes)
    {
        var ruleCheck = RuleCheck.Valid();

        ruleCheck.AddErrorIf(string.IsNullOrEmpty(registrationNo), "RegistrationNo");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(machine.MachineStatus), "MachineStatus");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(machine.MachineType), "MachineType");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(machine.InstallationStatus), "InstallationStatus");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(machine.InvoiceNumber), "InvoiceNumber");

        ruleCheck.AddErrorIf(machine.NumberOfMachinery < 0, "NumberOfMachine");

        // titleAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.SubDistrict), "SubDistrict");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.District), "District");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.Province), "Province");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.Postcode), "Postcode");

        // dopaAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.SubDistrict), "SubDistrict");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.District), "District");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.Province), "Province");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.Postcode), "Postcode");

        ruleCheck.ThrowIfInvalid();

        return new RequestTitle()
        {
            CollateralType = collateralType,
            CollateralStatus = collateralStatus,
            RegistrationNo = registrationNo,
            Machine = machine,
            TitleAddress = titleAddress,
            DopaAddress = dopaAddress,
            Notes = notes
        };
    }

    public static RequestTitle CreateLandBuilding(string collateralType, bool? collateralStatus, string titleNo, string deedType, string titleDetail, string rawang, string landNo, string surveyNo, LandArea landArea, string buildingType, decimal? usableArea, int? noOfBuilding, Address titleAddress, Address dopaAddress, string? notes)
    {
        var ruleCheck = RuleCheck.Valid();
        // Rule check for Land
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleNo), $"{nameof(titleNo)}");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(deedType), $"{nameof(deedType)}");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(rawang), $"{nameof(rawang)}");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(landNo), $"{nameof(landNo)}");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(surveyNo), $"{nameof(surveyNo)}");

        ruleCheck.AddErrorIf(landArea.AreaNgan < 0, "AreaNgan must be greater than or equal to 0.");
        ruleCheck.AddErrorIf(landArea.AreaRai < 0, "AreaRai must be greater than or equal to 0.");
        ruleCheck.AddErrorIf(landArea.AreaSquareWa < 0, "AreaSquareWa must be greater than or equal to 0.");

        // Rule check for Building
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(buildingType), "BuildingType");
        ruleCheck.AddErrorIf(usableArea < 0, "UsableArea must be greater than or equal to 0.");
        ruleCheck.AddErrorIf(noOfBuilding < 0, "NoOfBuilding must be greater than or equal to 0.");
        
        // titleAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.SubDistrict), "SubDistrict");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.District), "District");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.Province), "Province");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.Postcode), "Postcode");

        // dopaAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.SubDistrict), "SubDistrict");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.District), "District");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.Province), "Province");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(dopaAddress.Postcode), "Postcode");

        List<string> deedTypeList = new List<string>() { "Chanote", "NorSor3", "NorSor3Kor" };
        ruleCheck.AddErrorIf(!deedTypeList.Contains(deedType), "DeedType");

        ruleCheck.ThrowIfInvalid();

        return new RequestTitle()
        {
            CollateralType = collateralType,
            CollateralStatus = collateralStatus,
            TitleNo = titleNo,
            DeedType = deedType,
            TitleDetail = titleDetail,
            Rawang = rawang,
            LandNo = landNo,
            SurveyNo = surveyNo,
            LandArea = landArea,
            BuildingType = buildingType,
            UsableArea = usableArea,
            NoOfBuilding = noOfBuilding,
            TitleAddress = titleAddress,
            DopaAddress = dopaAddress,
            Notes = notes
        };
    }

    public static RequestTitle Create(Guid requestId, string? collateralType, bool? collateralStatus, string? titleNo, string? deedType, string? titleDetail, string? rawang, string? landNo, string? surveyNo, LandArea landArea, string? ownerName, string? registrationNo, Vehicle vehicle, Machine machine, string? buildingType, decimal? usableArea,  int? noOfBuilding, Condo condo, Address titleAddress, Address dopaAddress, string? notes)
    {
        var requestTitle = collateralType switch
        {
            "L" => CreateLand(collateralType, collateralStatus, titleNo, deedType, titleDetail, rawang, landNo, surveyNo, landArea, titleAddress, dopaAddress, notes),
            "B" => CreateBuilding(collateralType, collateralStatus, buildingType, usableArea, noOfBuilding, titleAddress, dopaAddress, notes),
            "LB" => CreateLandBuilding( collateralType, collateralStatus,  titleNo,  deedType,  titleDetail,  rawang,  landNo,  surveyNo, landArea,  buildingType, usableArea, noOfBuilding, titleAddress, dopaAddress, notes),
            "C" => CreateCondo(collateralType, collateralStatus, condo, usableArea, ownerName, titleDetail, titleAddress, dopaAddress, notes),
            "V" => CreateVechicle(collateralType, collateralStatus, registrationNo, vehicle, titleAddress, dopaAddress, notes),
            "M" => CreateMachine(collateralType, collateralStatus, registrationNo, machine, titleAddress, dopaAddress, notes),
            _ => throw new NotFoundException("Collateral Type is not valid.")
        };

        requestTitle.Id = Guid.NewGuid();
        requestTitle.RequestId = requestId;

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
        var requestTitle = collateralType switch
        {
            "L" => CreateLand(collateralType, collateralStatus, titleNo, deedType, titleDetail, rawang, landNo, surveyNo, landArea, titleAddress, dopaAddress, notes),
            "B" => CreateBuilding(collateralType, collateralStatus, buildingType, usableArea, noOfBuilding, titleAddress, dopaAddress, notes),
            "LB" => CreateLandBuilding( collateralType, collateralStatus,  titleNo,  deedType,  titleDetail,  rawang,  landNo,  surveyNo, landArea,  buildingType, usableArea, noOfBuilding, titleAddress, dopaAddress, notes),
            "C" => CreateCondo(collateralType, collateralStatus, condo, usableArea, ownerName, titleDetail, titleAddress, dopaAddress, notes),
            "V" => CreateVechicle(collateralType, collateralStatus, registrationNo, vehicle, titleAddress, dopaAddress, notes),
            "M" => CreateMachine(collateralType, collateralStatus, registrationNo, machine, titleAddress, dopaAddress, notes),
            _ => throw new NotFoundException("Collateral Type is not valid.")
        };

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
    public void SaveDraft()
    {
        
    }

}