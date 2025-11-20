namespace Request.RequestTitles.Models;

public class RequestTitle : Aggregate<Guid>
{
    public Guid RequestId { get; protected set; }
    public string? CollateralType { get; protected set; }
    public bool? CollateralStatus { get; protected set; }
    public TitleDeedInfo TitleDeedInfo { get; protected set; } = default!;
    public SurveyInfo SurveyInfo { get; protected set; } = default!;
    public LandArea LandArea { get; protected set; } = default!;
    public string? OwnerName { get; protected set; }
    public string? RegistrationNo { get; protected set; } 
    public VehicleInfo VehicleInfo { get; protected set; } = default!;
    public MachineInfo MachineInfo { get; protected set; } = default!;
    public BuildingInfo BuildingInfo { get; protected set; } = default!;
    public CondoInfo CondoInfo { get; protected set; } = default!;
    public Address TitleAddress { get; protected set; } = default!;
    public Address DopaAddress { get; protected set; } = default!;
    public string? Notes { get; protected set; }
    
    private readonly List<RequestTitleDocument> _requestTitleDocuments = [];
    public IReadOnlyList<RequestTitleDocument> RequestTitleDocuments => _requestTitleDocuments.AsReadOnly();

    protected RequestTitle()
    {
        // For EF Core
    }

    protected RequestTitle(RequestTitleData requestTitleData)
    {
        Id = Guid.NewGuid();
        RequestId = requestTitleData.RequestId;
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        TitleDeedInfo = requestTitleData.TitleDeedInfo;
        SurveyInfo = requestTitleData.SurveyInfo;
        LandArea = requestTitleData.LandArea;
        OwnerName = requestTitleData.OwnerName;
        RegistrationNo = requestTitleData.RegistrationNo;
        VehicleInfo = requestTitleData.VehicleInfo;
        MachineInfo = requestTitleData.MachineInfo;
        BuildingInfo = requestTitleData.BuildingInfo;
        CondoInfo = requestTitleData.CondoInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public RequestTitleDocument CreateLinkRequestTitleDocument(RequestTitleDocumentData requestTitleDocumentData)
    {
        var requestTitleDoc =  RequestTitleDocument.Create(requestTitleDocumentData);
        _requestTitleDocuments.Add(requestTitleDoc);

        return requestTitleDoc;
    }
}

public class RequestTitleLand : RequestTitle
{
    private RequestTitleLand(RequestTitleData requestTitleData) : base(requestTitleData)
    {
    }
    public static RequestTitle Create(RequestTitleData requestTitleData)
    {
        RequestTitleValidatorFactory.Create(requestTitleData.CollateralType).Validate(requestTitleData);
        var landData = new RequestTitleData()
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            SurveyInfo = requestTitleData.SurveyInfo,
            LandArea = requestTitleData.LandArea,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        
        // requestTitle.AddDomainEvent(new RequestTitleAddedEvent(requestTitle.RequestId, requestTitle));
        return new RequestTitleLand(landData);
    }
    
    public static RequestTitle CreateDraft(RequestTitleData requestTitleData)
    {
        var landData = new RequestTitleData()
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            SurveyInfo = requestTitleData.SurveyInfo,
            LandArea = requestTitleData.LandArea,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        // requestTitle.AddDomainEvent(new RequestTitleAddedEvent(requestTitle.RequestId, requestTitle));
        return new RequestTitleLand(landData);
    }

    public void Update(RequestTitleData requestTitleData)
    {
        RequestTitleValidatorFactory.Create(requestTitleData.CollateralType).Validate(requestTitleData);
        
        RequestId = requestTitleData.RequestId;
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        TitleDeedInfo = requestTitleData.TitleDeedInfo;
        SurveyInfo = requestTitleData.SurveyInfo;
        LandArea = requestTitleData.LandArea;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public void UpdateDraft(RequestTitleData requestTitleData)
    {
        RequestId = requestTitleData.RequestId;
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        TitleDeedInfo = requestTitleData.TitleDeedInfo;
        SurveyInfo = requestTitleData.SurveyInfo;
        LandArea = requestTitleData.LandArea;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }
}

public class RequestTitleBuilding : RequestTitle
{
    private RequestTitleBuilding(RequestTitleData requestTitleData) : base(requestTitleData)
    {
    }
    public static RequestTitle Create(RequestTitleData requestTitleData)
    {
        RequestTitleValidatorFactory.Create(requestTitleData.CollateralType).Validate(requestTitleData);
        var buildingData = new RequestTitleData()
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            BuildingInfo = requestTitleData.BuildingInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        
        // requestTitle.AddDomainEvent(new RequestTitleAddedEvent(requestTitle.RequestId, requestTitle));
        return new RequestTitleBuilding(buildingData);
    }
    
    public static RequestTitle CreateDraft(RequestTitleData requestTitleData)
    {
        var buildingData = new RequestTitleData()
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            BuildingInfo = requestTitleData.BuildingInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        // requestTitle.AddDomainEvent(new RequestTitleAddedEvent(requestTitle.RequestId, requestTitle));
        return new RequestTitleBuilding(buildingData);
    }

    public void Update(RequestTitleData requestTitleData)
    {
        RequestTitleValidatorFactory.Create(requestTitleData.CollateralType).Validate(requestTitleData);
        
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        BuildingInfo = requestTitleData.BuildingInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public void UpdateDraft(RequestTitleData requestTitleData)
    {
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        BuildingInfo = requestTitleData.BuildingInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }
}

public class RequestTitleLandBuilding : RequestTitle
{
    private RequestTitleLandBuilding(RequestTitleData requestTitleData) : base(requestTitleData)
    {
    }
    public static RequestTitle Create(RequestTitleData requestTitleData)
    {
        RequestTitleValidatorFactory.Create(requestTitleData.CollateralType).Validate(requestTitleData);
        
        var landBuildingData = new RequestTitleData()
        {
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            SurveyInfo = requestTitleData.SurveyInfo,
            LandArea = requestTitleData.LandArea,
            BuildingInfo = requestTitleData.BuildingInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        
        // requestTitle.AddDomainEvent(new RequestTitleAddedEvent(requestTitle.RequestId, requestTitle));
        return new RequestTitleLandBuilding(landBuildingData);
    }
    
    public static RequestTitle CreateDraft(RequestTitleData requestTitleData)
    {
        var landBuildingData = new RequestTitleData()
        {
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            SurveyInfo = requestTitleData.SurveyInfo,
            LandArea = requestTitleData.LandArea,
            BuildingInfo = requestTitleData.BuildingInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        // requestTitle.AddDomainEvent(new RequestTitleAddedEvent(requestTitle.RequestId, requestTitle));
        return new RequestTitleLandBuilding(landBuildingData);
    }

    public void Update(RequestTitleData requestTitleData)
    {
        RequestTitleValidatorFactory.Create(requestTitleData.CollateralType).Validate(requestTitleData);
        
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        TitleDeedInfo = requestTitleData.TitleDeedInfo;
        SurveyInfo = requestTitleData.SurveyInfo;
        LandArea = requestTitleData.LandArea;
        BuildingInfo = requestTitleData.BuildingInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public void UpdateDraft(RequestTitleData requestTitleData)
    {
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        TitleDeedInfo = requestTitleData.TitleDeedInfo;
        SurveyInfo = requestTitleData.SurveyInfo;
        LandArea = requestTitleData.LandArea;
        BuildingInfo = requestTitleData.BuildingInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }
}

public class RequestTitleCondo : RequestTitle
{
    private RequestTitleCondo(RequestTitleData requestTitleData) : base(requestTitleData)
    {
    }
    public static RequestTitle Create(RequestTitleData requestTitleData)
    {
        RequestTitleValidatorFactory.Create(requestTitleData.CollateralType).Validate(requestTitleData);
        
        var condoData = new RequestTitleData()
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            BuildingInfo = requestTitleData.BuildingInfo,
            CondoInfo = requestTitleData.CondoInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        
        // requestTitle.AddDomainEvent(new RequestTitleAddedEvent(requestTitle.RequestId, requestTitle));
        return new RequestTitleCondo(condoData);
    }
    
    public static RequestTitle CreateDraft(RequestTitleData requestTitleData)
    {
        var condoData = new RequestTitleData()
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            BuildingInfo = requestTitleData.BuildingInfo,
            CondoInfo = requestTitleData.CondoInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        // requestTitle.AddDomainEvent(new RequestTitleAddedEvent(requestTitle.RequestId, requestTitle));
        return new RequestTitleCondo(condoData);
    }

    public void Update(RequestTitleData requestTitleData)
    {
        RequestTitleValidatorFactory.Create(requestTitleData.CollateralType).Validate(requestTitleData);
        
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        BuildingInfo = requestTitleData.BuildingInfo;
        CondoInfo = requestTitleData.CondoInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public void UpdateDraft(RequestTitleData requestTitleData)
    {
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        BuildingInfo = requestTitleData.BuildingInfo;
        CondoInfo = requestTitleData.CondoInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }
}

public class RequestTitleMachine : RequestTitle
{
    private RequestTitleMachine(RequestTitleData requestTitleData) : base(requestTitleData)
    {
    }

    public static RequestTitle Create(RequestTitleData requestTitleData)
    {
        RequestTitleValidatorFactory.Create(requestTitleData.CollateralType).Validate(requestTitleData);
        
        var machineData = new RequestTitleData()
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            RegistrationNo = requestTitleData.RegistrationNo,
            MachineInfo = requestTitleData.MachineInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };

        // requestTitle.AddDomainEvent(new RequestTitleAddedEvent(requestTitle.RequestId, requestTitle));
        return new RequestTitleMachine(machineData);
    }

    public static RequestTitle CreateDraft(RequestTitleData requestTitleData)
    {
        var machineData = new RequestTitleData()
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            RegistrationNo = requestTitleData.RegistrationNo,
            MachineInfo = requestTitleData.MachineInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        // requestTitle.AddDomainEvent(new RequestTitleAddedEvent(requestTitle.RequestId, requestTitle));
        return new RequestTitleMachine(machineData);
    }

    public void Update(RequestTitleData requestTitleData)
    {
        RequestTitleValidatorFactory.Create(requestTitleData.CollateralType).Validate(requestTitleData);

        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        RegistrationNo = requestTitleData.RegistrationNo;
        MachineInfo = requestTitleData.MachineInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public void UpdateDraft(RequestTitleData requestTitleData)
    {
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        RegistrationNo = requestTitleData.RegistrationNo;
        MachineInfo = requestTitleData.MachineInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }
}

public class RequestTitleVehicle : RequestTitle
{
    private RequestTitleVehicle(RequestTitleData requestTitleData) : base(requestTitleData)
    {
    }

    public static RequestTitle Create(RequestTitleData requestTitleData)
    {
        RequestTitleValidatorFactory.Create(requestTitleData.CollateralType).Validate(requestTitleData);
        
        var vehicleData = new RequestTitleData()
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            RegistrationNo = requestTitleData.RegistrationNo,
            VehicleInfo = requestTitleData.VehicleInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };

        // requestTitle.AddDomainEvent(new RequestTitleAddedEvent(requestTitle.RequestId, requestTitle));
        return new RequestTitleVehicle(vehicleData);
    }

    public static RequestTitle CreateDraft(RequestTitleData requestTitleData)
    {
        var vehicleData = new RequestTitleData()
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            RegistrationNo = requestTitleData.RegistrationNo,
            VehicleInfo = requestTitleData.VehicleInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        // requestTitle.AddDomainEvent(new RequestTitleAddedEvent(requestTitle.RequestId, requestTitle));
        return new RequestTitleVehicle(vehicleData);
    }

    public void Update(RequestTitleData requestTitleData)
    {
        RequestTitleValidatorFactory.Create(requestTitleData.CollateralType).Validate(requestTitleData);

        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        RegistrationNo = requestTitleData.RegistrationNo;
        VehicleInfo = requestTitleData.VehicleInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public void UpdateDraft(RequestTitleData requestTitleData)
    {
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        RegistrationNo = requestTitleData.RegistrationNo;
        VehicleInfo = requestTitleData.VehicleInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }
}

public class RequestTitleFactory
{
    public static RequestTitle Create(RequestTitleData requestTitleData)
    {
        return requestTitleData.CollateralType switch
        {
            "Land" => RequestTitleLand.Create(requestTitleData),
            "Building" => RequestTitleBuilding.Create(requestTitleData),
            "LandBuilding" => RequestTitleLandBuilding.Create(requestTitleData),
            "Condo" => RequestTitleCondo.Create(requestTitleData),
            "Machine" => RequestTitleMachine.Create(requestTitleData),
            "Vehicle" => RequestTitleVehicle.Create(requestTitleData),
            _ => throw new Exception("Unknown RequestTitle type: " + requestTitleData.CollateralType)
        };
    }
    
    public static RequestTitle CreateDraft(RequestTitleData requestTitleData)
    {
        return requestTitleData.CollateralType switch
        {
            "Land" => RequestTitleLand.CreateDraft(requestTitleData),
            "Building" => RequestTitleBuilding.CreateDraft(requestTitleData),
            "LandBuilding" => RequestTitleLandBuilding.CreateDraft(requestTitleData),
            "Condo" => RequestTitleCondo.CreateDraft(requestTitleData),
            "Machine" => RequestTitleMachine.CreateDraft(requestTitleData),
            "Vehicle" => RequestTitleVehicle.CreateDraft(requestTitleData),
            _ => throw new Exception("Unknown RequestTitle type: " + requestTitleData.CollateralType)
        };
    }

    public static RequestTitle Update(RequestTitle requestTitle)
    {
        return requestTitle.CollateralType switch
        {
            "Land" => requestTitle as RequestTitleLand,
            "Building" => requestTitle as RequestTitleBuilding,
            "LandBuilding" => requestTitle as RequestTitleLandBuilding,
            "Condo" => requestTitle as RequestTitleCondo,
            "Machine" => requestTitle as RequestTitleMachine,
            "Vehicle" => requestTitle as RequestTitleVehicle,
            _ => throw new Exception("Unknown RequestTitle type: " + requestTitle.CollateralType)
        };
    }
}

public record RequestTitleData
{
    public Guid RequestId { get; init; }
    public string CollateralType { get; init; } = default!;
    public bool CollateralStatus { get; init; } = false;
    public TitleDeedInfo TitleDeedInfo { get; init; } = default!;
    public SurveyInfo SurveyInfo { get; init; } = default!;
    public LandArea LandArea { get; init; } = default!;
    public string OwnerName { get; init; } = default!;
    public string RegistrationNo { get; init; } = default!;
    public VehicleInfo VehicleInfo { get; init; } = default!;
    public MachineInfo MachineInfo { get; init; } = default!;
    public BuildingInfo BuildingInfo { get; init; } = default!;
    public CondoInfo CondoInfo { get; init; } = default!;
    public Address TitleAddress { get; init; } = default!;
    public Address DopaAddress { get; init; } = default!;
    public string Notes { get; init; } = default!;
};

public class RequestTitleValidatorFactory
{
    public static RequestTitleValidator Create(string type)
    {
        return type switch
        {
            "Land" => new TitleLandValidator(),
            "Building" => new TitleBuildingValidator(),
            "LandBuilding" => new TitleLandBuildingValidator(),
            "Condo" => new TitleCondoValidator(),
            "Machine" => new TitleMachineValidator(),
            "Vehicle" => new TitleVehicleValidator(),
            _ => throw new Exception($"Invalid request title type {type}"),
        };
    }
}

public abstract class RequestTitleValidator
{
    public abstract void Validate(RequestTitleData requestTitleData);
}

public class TitleLandValidator : RequestTitleValidator
{
    public override void Validate(RequestTitleData requestTitleData)
    {
        var ruleCheck = new RuleCheck();
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.SurveyInfo.Rawang), "rawang is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.SurveyInfo.LandNo), "landNo is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.SurveyInfo.SurveyNo), "surveyNo is null or contains only whitespace.");
        
        ruleCheck.AddErrorIf(requestTitleData.LandArea.AreaNgan is null || requestTitleData.LandArea.AreaNgan < 0, "areaNgan must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(requestTitleData.LandArea.AreaRai is null || requestTitleData.LandArea.AreaRai < 0, "areaRai must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(requestTitleData.LandArea.AreaSquareWa is null || requestTitleData.LandArea.AreaSquareWa < 0, "areaSquareWa must be greater than or equal to 0 or not null.");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleDeedInfo.DeedType), "deedType is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleDeedInfo.TitleNo), "titleNo is null or contains only whitespace.");

        var deedTypeChecklist = new List<string>() { "Chanote", "NorSor3", "NorSor3Kor" };
        ruleCheck.AddErrorIf(!deedTypeChecklist.Contains(requestTitleData.TitleDeedInfo.DeedType), "deedType is invalid.");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Postcode), "postcode is null or contains only whitespace.");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.SubDistrict), "subDistrict(DOPA) is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.District), "district(DOPA) is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Province), "province(DOPA) is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Postcode), "postcode(DOPA) is null or contains only whitespace.");
        
        ruleCheck.ThrowIfInvalid();
    }
}

public class TitleBuildingValidator : RequestTitleValidator
{
    public override void Validate(RequestTitleData requestTitleData)
    {
        var ruleCheck = new RuleCheck();
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.BuildingInfo.BuildingType), "buildingType is null or contains only whitespace.");
        ruleCheck.AddErrorIf(requestTitleData.BuildingInfo.UsableArea is null || requestTitleData.BuildingInfo.UsableArea < 0, "usableArea must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(requestTitleData.BuildingInfo.NumberOfBuilding is null || requestTitleData.BuildingInfo.NumberOfBuilding < 0, "numberOfBuildings must be greater than or equal to 0 or not null.");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleDeedInfo.TitleDetail), "titleDetail is null or contains only whitespace.");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Postcode), "postcode is null or contains only whitespace.");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.SubDistrict), "subDistrict(DOPA) is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.District), "district(DOPA) is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Province), "province(DOPA) is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Postcode), "postcode(DOPA) is null or contains only whitespace.");
        
        ruleCheck.ThrowIfInvalid();
    }
}

public class TitleLandBuildingValidator : RequestTitleValidator
{
    public override void Validate(RequestTitleData requestTitleData)
    {
        var ruleCheck = new RuleCheck();
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.SurveyInfo.Rawang), "rawang is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.SurveyInfo.LandNo), "landNo is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.SurveyInfo.SurveyNo), "surveyNo is null or contains only whitespace.");
        
        ruleCheck.AddErrorIf(requestTitleData.LandArea.AreaNgan is null || requestTitleData.LandArea.AreaNgan < 0, "areaNgan must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(requestTitleData.LandArea.AreaRai is null || requestTitleData.LandArea.AreaRai < 0, "areaRai must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(requestTitleData.LandArea.AreaSquareWa is null || requestTitleData.LandArea.AreaSquareWa < 0, "areaSquareWa must be greater than or equal to 0 or not null.");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleDeedInfo.DeedType), "deedType is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleDeedInfo.TitleNo), "titleNo is null or contains only whitespace.");

        var deedTypeChecklist = new List<string>() { "Chanote", "NorSor3", "NorSor3Kor" };
        ruleCheck.AddErrorIf(!deedTypeChecklist.Contains(requestTitleData.TitleDeedInfo.DeedType), "deedType is invalid.");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.BuildingInfo.BuildingType), "buildingType is null or contains only whitespace.");
        ruleCheck.AddErrorIf(requestTitleData.BuildingInfo.UsableArea is null || requestTitleData.BuildingInfo.UsableArea < 0, "usableArea must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(requestTitleData.BuildingInfo.NumberOfBuilding is null || requestTitleData.BuildingInfo.NumberOfBuilding < 0, "numberOfBuildings must be greater than or equal to 0 or not null.");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.OwnerName), "ownerName is null or contains only whitespace.");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleDeedInfo.TitleDetail), "titleDetail is null or contains only whitespace.");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Postcode), "postcode is null or contains only whitespace.");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.SubDistrict), "subDistrict(DOPA) is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.District), "district(DOPA) is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Province), "province(DOPA) is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Postcode), "postcode(DOPA) is null or contains only whitespace.");
        
        
        ruleCheck.ThrowIfInvalid();
    }
}

public class TitleMachineValidator : RequestTitleValidator
{
    public override void Validate(RequestTitleData requestTitleData)
    {
        var ruleCheck = new RuleCheck();
        
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(requestTitleData.MachineInfo.MachineStatus), "machineStatus is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(requestTitleData.MachineInfo.MachineType), "machineType  is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(requestTitleData.MachineInfo.InstallationStatus), "installationStatus  is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(requestTitleData.MachineInfo.InvoiceNumber), "invoiceNumber   is null or contains only whitespace.");

        ruleCheck.AddErrorIf(requestTitleData.MachineInfo.NumberOfMachinery is null || requestTitleData.MachineInfo.NumberOfMachinery < 0, "numberOfMachine must be greater than or equal to 0 or not null.");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Postcode), "postcode is null or contains only whitespace.");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.SubDistrict), "subDistrict(DOPA) is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.District), "district(DOPA) is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Province), "province(DOPA) is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Postcode), "postcode(DOPA) is null or contains only whitespace.");
        
        ruleCheck.ThrowIfInvalid();
    }
}

public class TitleVehicleValidator : RequestTitleValidator
{
    public override void Validate(RequestTitleData requestTitleData)
    {
        var ruleCheck = new RuleCheck();
        
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(requestTitleData.RegistrationNo), "registrationNo is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(requestTitleData.VehicleInfo.VehicleType), "vehicleType");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(requestTitleData.VehicleInfo.VehicleAppointmentLocation), "vehicleAppointmentLocation");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(requestTitleData.VehicleInfo.ChassisNumber), "chassisNumber");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Postcode), "postcode is null or contains only whitespace.");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.SubDistrict), "subDistrict(DOPA) is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.District), "district(DOPA) is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Province), "province(DOPA) is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Postcode), "postcode(DOPA) is null or contains only whitespace.");
        
        ruleCheck.ThrowIfInvalid();
    }
}

public class TitleCondoValidator : RequestTitleValidator
{
    public override void Validate(RequestTitleData requestTitleData)
    {
        var ruleCheck = new RuleCheck();
        
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(requestTitleData.CondoInfo.CondoName), "condoName");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(requestTitleData.CondoInfo.BuildingNo), "buildingNo");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(requestTitleData.CondoInfo.RoomNo), "roomNo");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(requestTitleData.CondoInfo.FloorNo), "floorNo");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Postcode), "postcode is null or contains only whitespace.");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.SubDistrict), "subDistrict(DOPA) is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.District), "district(DOPA) is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Province), "province(DOPA) is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Postcode), "postcode(DOPA) is null or contains only whitespace.");
        
        ruleCheck.ThrowIfInvalid();
    }
}
