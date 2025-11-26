namespace Request.RequestTitles.Models;

public abstract class RequestTitle : Aggregate<Guid>
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
        Validate(requestTitleData);
        
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

    public abstract RequestTitle Create(RequestTitleData requestTitleData);
    public abstract RequestTitle Draft(RequestTitleData requestTitleData);
    public abstract void Update(RequestTitleData requestTitleData);
    public abstract void UpdateDraft(RequestTitleData requestTitleData);

    public RequestTitleDocument CreateLinkRequestTitleDocument(RequestTitleDocumentData requestTitleDocumentData)
    {
        var requestTitleDoc =  RequestTitleDocument.Create(requestTitleDocumentData);
        _requestTitleDocuments.Add(requestTitleDoc);

        return requestTitleDoc;
    }

    private void Validate(RequestTitleData requestTitleData)
    {
        var ruleCheck = new RuleCheck();
        ruleCheck.AddErrorIf(requestTitleData.RequestId == Guid.Empty, "RequestId is Empty.");
        ruleCheck.ThrowIfInvalid();
    }
}

public class RequestTitleFactory
{
    public static RequestTitle Create(string collateralType)
    {
        return collateralType switch
        {
            "Land" => new TitleLand(),
            "LeaseLand" => new TitleLeaseAgreementLand(),
            "Building" => new TitleBuilding(),
            "LeaseBuilding" => new TitleLeaseAgreementBuilding(),
            "LandBuilding" => new TitleLandBuilding(),
            "LeaseLandBuilding" => new TitleLeaseAgreementLandBuilding(),
            "Condo" => new TitleCondo(),
            "LeaseCondo" => new TitleLeaseAgreementCondo(),
            "Machine" => new TitleMachine(),
            "Vehicle" => new TitleVehicle(),
            "Vessel" => new TitleVessel(),
            _ => throw new ArgumentOutOfRangeException(nameof(collateralType), collateralType, null)
        };
    }
}

public sealed class TitleLand : RequestTitle
{
    public TitleLand(){}
    private TitleLand(RequestTitleData requestTitleData) : base(requestTitleData){}
    public override TitleLand Create(RequestTitleData requestTitleData)
    {
        Validate(requestTitleData);
        var landData = new RequestTitleData
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            SurveyInfo = requestTitleData.SurveyInfo,
            LandArea = requestTitleData.LandArea,
            OwnerName = requestTitleData.OwnerName,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        
        this.AddDomainEvent(new RequestTitleAddedEvent(this.RequestId, this));
        return new TitleLand(landData);
    }

    public override TitleLand Draft(RequestTitleData requestTitleData)
    {
        var landData = new RequestTitleData
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
        
        this.AddDomainEvent(new RequestTitleAddedEvent(this.RequestId, this));
        // requestTitle.AddIntegrationEvent(new DocumentLinkedIntegrationEvent("Title", requestTitle.Id, requestTitleDocuments.Select(rtd => rtd.DocumentId).ToList()));
        return new TitleLand(landData);
    }

    public override void Update(RequestTitleData requestTitleData)
    {
        Validate(requestTitleData);
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        TitleDeedInfo = requestTitleData.TitleDeedInfo;
        SurveyInfo = requestTitleData.SurveyInfo;
        LandArea = requestTitleData.LandArea;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;

        // requestTitle.AddIntegrationEvent(new DocumentLinkedIntegrationEvent("Title", requestTitle.Id, requestTitleDocuments.Select(rtd => rtd.DocumentId).ToList()));
    }
    
    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        TitleDeedInfo = requestTitleData.TitleDeedInfo;
        SurveyInfo = requestTitleData.SurveyInfo;
        LandArea = requestTitleData.LandArea;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;

        // requestTitle.AddIntegrationEvent(new DocumentLinkedIntegrationEvent("Title", requestTitle.Id, requestTitleDocuments.Select(rtd => rtd.DocumentId).ToList()));
    }

    private void Validate(RequestTitleData requestTitleData)
    {
        var ruleCheck = new RuleCheck();
        
        // LandArea
        ruleCheck.AddErrorIf(requestTitleData.LandArea.AreaNgan is null || requestTitleData.LandArea.AreaNgan < 0, "areaNgan must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(requestTitleData.LandArea.AreaRai is null || requestTitleData.LandArea.AreaRai < 0, "areaRai must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(requestTitleData.LandArea.AreaSquareWa is null || requestTitleData.LandArea.AreaSquareWa < 0, "areaSquareWa must be greater than or equal to 0 or not null.");
        
        // Owner
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(requestTitleData.OwnerName),
            "ownerName is null or contains only whitespace.");
        
        // SurveyInfo
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.SurveyInfo.Rawang), "rawang is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.SurveyInfo.LandNo), "landNo is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.SurveyInfo.SurveyNo), "surveyNo is null or contains only whitespace.");
        
        // TitleDeedInfo
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleDeedInfo.DeedType), "deedType is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleDeedInfo.TitleNo), "titleNo is null or contains only whitespace.");

        var deedTypeChecklist = new List<string>() { "Chanote", "NorSor3", "NorSor3Kor" };
        ruleCheck.AddErrorIf(!deedTypeChecklist.Contains(requestTitleData.TitleDeedInfo.DeedType), "deedType is invalid.");
        
        // TitleAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Postcode), "postcode is null or contains only whitespace.");
        // DopaAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Postcode), "postcode is null or contains only whitespace.");
        
        ruleCheck.ThrowIfInvalid();
    }
}

public sealed class TitleLeaseAgreementLand : RequestTitle
{
    public TitleLeaseAgreementLand(){}
    private TitleLeaseAgreementLand(RequestTitleData requestTitleData) : base(requestTitleData){}
    public override TitleLeaseAgreementLand Create(RequestTitleData requestTitleData)
    {
        Validate(requestTitleData);
        var landData = new RequestTitleData
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
        
        this.AddDomainEvent(new RequestTitleAddedEvent(this.RequestId, this));
        // requestTitle.AddIntegrationEvent(new DocumentLinkedIntegrationEvent("Title", requestTitle.Id, requestTitleDocuments.Select(rtd => rtd.DocumentId).ToList()));
        return new TitleLeaseAgreementLand(landData);
    }

    public override TitleLeaseAgreementLand Draft(RequestTitleData requestTitleData)
    {
        var landData = new RequestTitleData
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
        
        this.AddDomainEvent(new RequestTitleAddedEvent(this.RequestId, this));
        // requestTitle.AddIntegrationEvent(new DocumentLinkedIntegrationEvent("Title", requestTitle.Id, requestTitleDocuments.Select(rtd => rtd.DocumentId).ToList()));
        return new TitleLeaseAgreementLand(landData);
    }

    public override void Update(RequestTitleData requestTitleData)
    {
        Validate(requestTitleData);
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        TitleDeedInfo = requestTitleData.TitleDeedInfo;
        SurveyInfo = requestTitleData.SurveyInfo;
        LandArea = requestTitleData.LandArea;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;

        // requestTitle.AddIntegrationEvent(new DocumentLinkedIntegrationEvent("Title", requestTitle.Id, requestTitleDocuments.Select(rtd => rtd.DocumentId).ToList()));
    }
    
    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        TitleDeedInfo = requestTitleData.TitleDeedInfo;
        SurveyInfo = requestTitleData.SurveyInfo;
        LandArea = requestTitleData.LandArea;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;

        // requestTitle.AddIntegrationEvent(new DocumentLinkedIntegrationEvent("Title", requestTitle.Id, requestTitleDocuments.Select(rtd => rtd.DocumentId).ToList()));
    }

    private void Validate(RequestTitleData requestTitleData)
    {
        var ruleCheck = new RuleCheck();
        
        // LandArea
        ruleCheck.AddErrorIf(requestTitleData.LandArea.AreaNgan is null || requestTitleData.LandArea.AreaNgan < 0, "areaNgan must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(requestTitleData.LandArea.AreaRai is null || requestTitleData.LandArea.AreaRai < 0, "areaRai must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(requestTitleData.LandArea.AreaSquareWa is null || requestTitleData.LandArea.AreaSquareWa < 0, "areaSquareWa must be greater than or equal to 0 or not null.");
        
        // Owner
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(requestTitleData.OwnerName),
            "ownerName is null or contains only whitespace.");
        
        // SurveyInfo
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.SurveyInfo.Rawang), "rawang is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.SurveyInfo.LandNo), "landNo is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.SurveyInfo.SurveyNo), "surveyNo is null or contains only whitespace.");
        
        // TitleDeedInfo
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleDeedInfo.DeedType), "deedType is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleDeedInfo.TitleNo), "titleNo is null or contains only whitespace.");

        var deedTypeChecklist = new List<string>() { "Chanote", "NorSor3", "NorSor3Kor" };
        ruleCheck.AddErrorIf(!deedTypeChecklist.Contains(requestTitleData.TitleDeedInfo.DeedType), "deedType is invalid.");
        
        // TitleAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Postcode), "postcode is null or contains only whitespace.");
        // DopaAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Postcode), "postcode is null or contains only whitespace.");
        
        ruleCheck.ThrowIfInvalid();
    }
}

public sealed class TitleBuilding : RequestTitle
{
    public TitleBuilding(){}
    private TitleBuilding(RequestTitleData requestTitleData) : base(requestTitleData){}
    public override TitleBuilding Create(RequestTitleData requestTitleData)
    {
        Validate(requestTitleData);
        var buildingData = new RequestTitleData
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            BuildingInfo = requestTitleData.BuildingInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        
        this.AddDomainEvent(new RequestTitleAddedEvent(this.RequestId, this));
        // requestTitle.AddIntegrationEvent(new DocumentLinkedIntegrationEvent("Title", requestTitle.Id, requestTitleDocuments.Select(rtd => rtd.DocumentId).ToList()));
        return new TitleBuilding(buildingData);
    }

    public override TitleBuilding Draft(RequestTitleData requestTitleData)
    {
        var buildingData = new RequestTitleData
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            BuildingInfo = requestTitleData.BuildingInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        
        this.AddDomainEvent(new RequestTitleAddedEvent(this.RequestId, this));
        return new TitleBuilding(buildingData);
    }

    public override void Update(RequestTitleData requestTitleData)
    {
        Validate(requestTitleData);
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        BuildingInfo = requestTitleData.BuildingInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }
    
    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        BuildingInfo = requestTitleData.BuildingInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    private void Validate(RequestTitleData requestTitleData)
    {
        var ruleCheck = new RuleCheck();
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.BuildingInfo.BuildingType), "buildingType is null or contains only whitespace.");
        ruleCheck.AddErrorIf(requestTitleData.BuildingInfo.UsableArea is null || requestTitleData.BuildingInfo.UsableArea < 0, "usableArea must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(requestTitleData.BuildingInfo.NumberOfBuilding is null || requestTitleData.BuildingInfo.NumberOfBuilding < 0, "numberOfBuildings must be greater than or equal to 0 or not null.");
        
        // TitleAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Postcode), "postcode is null or contains only whitespace.");
        // DopaAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Postcode), "postcode is null or contains only whitespace.");
        
        ruleCheck.ThrowIfInvalid();
    }
}

public sealed class TitleLeaseAgreementBuilding : RequestTitle
{
    public TitleLeaseAgreementBuilding(){}
    private TitleLeaseAgreementBuilding(RequestTitleData requestTitleData) : base(requestTitleData){}
    public override TitleLeaseAgreementBuilding Create(RequestTitleData requestTitleData)
    {
        Validate(requestTitleData);
        var buildingData = new RequestTitleData
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            BuildingInfo = requestTitleData.BuildingInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        
        this.AddDomainEvent(new RequestTitleAddedEvent(this.RequestId, this));
        // requestTitle.AddIntegrationEvent(new DocumentLinkedIntegrationEvent("Title", requestTitle.Id, requestTitleDocuments.Select(rtd => rtd.DocumentId).ToList()));
        return new TitleLeaseAgreementBuilding(buildingData);
    }

    public override TitleLeaseAgreementBuilding Draft(RequestTitleData requestTitleData)
    {
        var buildingData = new RequestTitleData
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            BuildingInfo = requestTitleData.BuildingInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        
        this.AddDomainEvent(new RequestTitleAddedEvent(this.RequestId, this));
        return new TitleLeaseAgreementBuilding(buildingData);
    }

    public override void Update(RequestTitleData requestTitleData)
    {
        Validate(requestTitleData);
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        BuildingInfo = requestTitleData.BuildingInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }
    
    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        BuildingInfo = requestTitleData.BuildingInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    private void Validate(RequestTitleData requestTitleData)
    {
        var ruleCheck = new RuleCheck();
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.BuildingInfo.BuildingType), "buildingType is null or contains only whitespace.");
        ruleCheck.AddErrorIf(requestTitleData.BuildingInfo.UsableArea is null || requestTitleData.BuildingInfo.UsableArea < 0, "usableArea must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(requestTitleData.BuildingInfo.NumberOfBuilding is null || requestTitleData.BuildingInfo.NumberOfBuilding < 0, "numberOfBuildings must be greater than or equal to 0 or not null.");
        
        // TitleAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Postcode), "postcode is null or contains only whitespace.");
        // DopaAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Postcode), "postcode is null or contains only whitespace.");
        
        ruleCheck.ThrowIfInvalid();
    }
}

public sealed class TitleLandBuilding : RequestTitle
{
    public TitleLandBuilding(){}
    private TitleLandBuilding(RequestTitleData requestTitleData) : base(requestTitleData){}
    public override TitleLandBuilding Create(RequestTitleData requestTitleData)
    {
        Validate(requestTitleData);
        var landBuildingData = new RequestTitleData
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            SurveyInfo = requestTitleData.SurveyInfo,
            LandArea = requestTitleData.LandArea,
            OwnerName = requestTitleData.OwnerName,
            BuildingInfo = requestTitleData.BuildingInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        
        this.AddDomainEvent(new RequestTitleAddedEvent(this.RequestId, this));
        // requestTitle.AddIntegrationEvent(new DocumentLinkedIntegrationEvent("Title", requestTitle.Id, requestTitleDocuments.Select(rtd => rtd.DocumentId).ToList()));
        return new TitleLandBuilding(landBuildingData);
    }

    public override TitleLandBuilding Draft(RequestTitleData requestTitleData)
    {
        var landBuildingData = new RequestTitleData
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            SurveyInfo = requestTitleData.SurveyInfo,
            LandArea = requestTitleData.LandArea,
            OwnerName = requestTitleData.OwnerName,
            BuildingInfo = requestTitleData.BuildingInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        
        this.AddDomainEvent(new RequestTitleAddedEvent(this.RequestId, this));
        return new TitleLandBuilding(landBuildingData);
    }

    public override void Update(RequestTitleData requestTitleData)
    {
        Validate(requestTitleData);
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        TitleDeedInfo = requestTitleData.TitleDeedInfo;
        SurveyInfo = requestTitleData.SurveyInfo;
        LandArea = requestTitleData.LandArea;
        OwnerName = requestTitleData.OwnerName;
        BuildingInfo = requestTitleData.BuildingInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        TitleDeedInfo = requestTitleData.TitleDeedInfo;
        SurveyInfo = requestTitleData.SurveyInfo;
        LandArea = requestTitleData.LandArea;
        OwnerName = requestTitleData.OwnerName;
        BuildingInfo = requestTitleData.BuildingInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    private void Validate(RequestTitleData requestTitleData)
    {
        var ruleCheck = new RuleCheck();
        // LandArea
        ruleCheck.AddErrorIf(requestTitleData.LandArea.AreaNgan is null || requestTitleData.LandArea.AreaNgan < 0, "areaNgan must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(requestTitleData.LandArea.AreaRai is null || requestTitleData.LandArea.AreaRai < 0, "areaRai must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(requestTitleData.LandArea.AreaSquareWa is null || requestTitleData.LandArea.AreaSquareWa < 0, "areaSquareWa must be greater than or equal to 0 or not null.");
        
        
        // SurveyInfo
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.SurveyInfo.Rawang), "rawang is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.SurveyInfo.LandNo), "landNo is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.SurveyInfo.SurveyNo), "surveyNo is null or contains only whitespace.");
        
        // TitleDeedInfo
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleDeedInfo.DeedType), "deedType is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleDeedInfo.TitleNo), "titleNo is null or contains only whitespace.");

        var deedTypeChecklist = new List<string>() { "Chanote", "NorSor3", "NorSor3Kor" };
        ruleCheck.AddErrorIf(!deedTypeChecklist.Contains(requestTitleData.TitleDeedInfo.DeedType), "deedType is invalid.");
        
        // BuildingInfo
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.BuildingInfo.BuildingType), "buildingType is null or contains only whitespace.");
        ruleCheck.AddErrorIf(requestTitleData.BuildingInfo.UsableArea is null || requestTitleData.BuildingInfo.UsableArea < 0, "usableArea must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(requestTitleData.BuildingInfo.NumberOfBuilding is null || requestTitleData.BuildingInfo.NumberOfBuilding < 0, "numberOfBuildings must be greater than or equal to 0 or not null.");
        
        // OwnerName
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.OwnerName), "ownerName is null or contains only whitespace.");
        
        // TitleAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Postcode), "postcode is null or contains only whitespace.");
        // DopaAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Postcode), "postcode is null or contains only whitespace.");
        
        ruleCheck.ThrowIfInvalid();
    }
}

public sealed class TitleLeaseAgreementLandBuilding : RequestTitle
{
    public TitleLeaseAgreementLandBuilding(){}
    private TitleLeaseAgreementLandBuilding(RequestTitleData requestTitleData) : base(requestTitleData){}
    public override TitleLeaseAgreementLandBuilding Create(RequestTitleData requestTitleData)
    {
        Validate(requestTitleData);
        var landBuildingData = new RequestTitleData
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            SurveyInfo = requestTitleData.SurveyInfo,
            LandArea = requestTitleData.LandArea,
            OwnerName = requestTitleData.OwnerName,
            BuildingInfo = requestTitleData.BuildingInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        
        this.AddDomainEvent(new RequestTitleAddedEvent(this.RequestId, this));
        // requestTitle.AddIntegrationEvent(new DocumentLinkedIntegrationEvent("Title", requestTitle.Id, requestTitleDocuments.Select(rtd => rtd.DocumentId).ToList()));
        return new TitleLeaseAgreementLandBuilding(landBuildingData);
    }

    public override TitleLeaseAgreementLandBuilding Draft(RequestTitleData requestTitleData)
    {
        var landBuildingData = new RequestTitleData
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            SurveyInfo = requestTitleData.SurveyInfo,
            LandArea = requestTitleData.LandArea,
            OwnerName = requestTitleData.OwnerName,
            BuildingInfo = requestTitleData.BuildingInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        
        this.AddDomainEvent(new RequestTitleAddedEvent(this.RequestId, this));
        return new TitleLeaseAgreementLandBuilding(landBuildingData);
    }

    public override void Update(RequestTitleData requestTitleData)
    {
        Validate(requestTitleData);
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        TitleDeedInfo = requestTitleData.TitleDeedInfo;
        SurveyInfo = requestTitleData.SurveyInfo;
        LandArea = requestTitleData.LandArea;
        OwnerName = requestTitleData.OwnerName;
        BuildingInfo = requestTitleData.BuildingInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        TitleDeedInfo = requestTitleData.TitleDeedInfo;
        SurveyInfo = requestTitleData.SurveyInfo;
        LandArea = requestTitleData.LandArea;
        OwnerName = requestTitleData.OwnerName;
        BuildingInfo = requestTitleData.BuildingInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    private void Validate(RequestTitleData requestTitleData)
    {
        var ruleCheck = new RuleCheck();
        // LandArea
        ruleCheck.AddErrorIf(requestTitleData.LandArea.AreaNgan is null || requestTitleData.LandArea.AreaNgan < 0, "areaNgan must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(requestTitleData.LandArea.AreaRai is null || requestTitleData.LandArea.AreaRai < 0, "areaRai must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(requestTitleData.LandArea.AreaSquareWa is null || requestTitleData.LandArea.AreaSquareWa < 0, "areaSquareWa must be greater than or equal to 0 or not null.");
        
        
        // SurveyInfo
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.SurveyInfo.Rawang), "rawang is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.SurveyInfo.LandNo), "landNo is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.SurveyInfo.SurveyNo), "surveyNo is null or contains only whitespace.");
        
        // TitleDeedInfo
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleDeedInfo.DeedType), "deedType is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleDeedInfo.TitleNo), "titleNo is null or contains only whitespace.");

        var deedTypeChecklist = new List<string>() { "Chanote", "NorSor3", "NorSor3Kor" };
        ruleCheck.AddErrorIf(!deedTypeChecklist.Contains(requestTitleData.TitleDeedInfo.DeedType), "deedType is invalid.");
        
        // BuildingInfo
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.BuildingInfo.BuildingType), "buildingType is null or contains only whitespace.");
        ruleCheck.AddErrorIf(requestTitleData.BuildingInfo.UsableArea is null || requestTitleData.BuildingInfo.UsableArea < 0, "usableArea must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(requestTitleData.BuildingInfo.NumberOfBuilding is null || requestTitleData.BuildingInfo.NumberOfBuilding < 0, "numberOfBuildings must be greater than or equal to 0 or not null.");
        
        // OwnerName
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.OwnerName), "ownerName is null or contains only whitespace.");
        
        // TitleAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Postcode), "postcode is null or contains only whitespace.");
        // DopaAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Postcode), "postcode is null or contains only whitespace.");
        
        ruleCheck.ThrowIfInvalid();
    }
}

public sealed class TitleCondo : RequestTitle
{
    public TitleCondo(){}
    private TitleCondo(RequestTitleData requestTitleData) : base(requestTitleData){}
    public override RequestTitle Create(RequestTitleData requestTitleData)
    {
        // validate
        var condoData = new RequestTitleData
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            OwnerName = requestTitleData.OwnerName,
            BuildingInfo = requestTitleData.BuildingInfo,
            CondoInfo = requestTitleData.CondoInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        
        return new TitleCondo(condoData);
    }

    public override RequestTitle Draft(RequestTitleData requestTitleData)
    {
        var condoData = new RequestTitleData
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            OwnerName = requestTitleData.OwnerName,
            BuildingInfo = requestTitleData.BuildingInfo,
            CondoInfo = requestTitleData.CondoInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        
        return new TitleCondo(condoData);
    }

    public override void Update(RequestTitleData requestTitleData)
    {
        // validate
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        TitleDeedInfo = requestTitleData.TitleDeedInfo;
        OwnerName = requestTitleData.OwnerName;
        BuildingInfo = requestTitleData.BuildingInfo;
        CondoInfo = requestTitleData.CondoInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        TitleDeedInfo = requestTitleData.TitleDeedInfo;
        OwnerName = requestTitleData.OwnerName;
        BuildingInfo = requestTitleData.BuildingInfo;
        CondoInfo = requestTitleData.CondoInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public void Validate(RequestTitleData requestTitleData)
    {
        var ruleCheck = new RuleCheck();
        
        ruleCheck.AddErrorIf(requestTitleData.BuildingInfo.UsableArea is null || requestTitleData.BuildingInfo.UsableArea < 0, "usableArea must be greater than or equal to 0 or not null.");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.CondoInfo.CondoName), "condoName");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.CondoInfo.BuildingNo), "buildingNo");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.CondoInfo.RoomNo), "roomNo");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.CondoInfo.FloorNo), "floorNo");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.OwnerName), "ownerName is null or contains only whitespace.");
        
        // TitleAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Postcode), "postcode is null or contains only whitespace.");
        // DopaAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Postcode), "postcode is null or contains only whitespace.");
        
        ruleCheck.ThrowIfInvalid();
    }
}

public sealed class TitleLeaseAgreementCondo : RequestTitle
{
    public TitleLeaseAgreementCondo(){}
    private TitleLeaseAgreementCondo(RequestTitleData requestTitleData) : base(requestTitleData){}
    public override RequestTitle Create(RequestTitleData requestTitleData)
    {
        // validate
        var condoData = new RequestTitleData
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            OwnerName = requestTitleData.OwnerName,
            BuildingInfo = requestTitleData.BuildingInfo,
            CondoInfo = requestTitleData.CondoInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        
        return new TitleLeaseAgreementCondo(condoData);
    }

    public override RequestTitle Draft(RequestTitleData requestTitleData)
    {
        var condoData = new RequestTitleData
        {
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            OwnerName = requestTitleData.OwnerName,
            BuildingInfo = requestTitleData.BuildingInfo,
            CondoInfo = requestTitleData.CondoInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        
        return new TitleLeaseAgreementCondo(condoData);
    }

    public override void Update(RequestTitleData requestTitleData)
    {
        // validate
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        TitleDeedInfo = requestTitleData.TitleDeedInfo;
        OwnerName = requestTitleData.OwnerName;
        BuildingInfo = requestTitleData.BuildingInfo;
        CondoInfo = requestTitleData.CondoInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        TitleDeedInfo = requestTitleData.TitleDeedInfo;
        OwnerName = requestTitleData.OwnerName;
        BuildingInfo = requestTitleData.BuildingInfo;
        CondoInfo = requestTitleData.CondoInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public void Validate(RequestTitleData requestTitleData)
    {
        var ruleCheck = new RuleCheck();
        
        ruleCheck.AddErrorIf(requestTitleData.BuildingInfo.UsableArea is null || requestTitleData.BuildingInfo.UsableArea < 0, "usableArea must be greater than or equal to 0 or not null.");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.CondoInfo.CondoName), "condoName");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.CondoInfo.BuildingNo), "buildingNo");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.CondoInfo.RoomNo), "roomNo");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.CondoInfo.FloorNo), "floorNo");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.OwnerName), "ownerName is null or contains only whitespace.");
        
        // TitleAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Postcode), "postcode is null or contains only whitespace.");
        // DopaAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Postcode), "postcode is null or contains only whitespace.");
        
        ruleCheck.ThrowIfInvalid();
    }
}

public sealed class TitleMachine : RequestTitle
{
    public TitleMachine(){}
    private TitleMachine(RequestTitleData requestTitleData) : base(requestTitleData){}
    public override RequestTitle Create(RequestTitleData requestTitleData)
    {
        Validate(requestTitleData);
        var machineData = new RequestTitleData
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
        
        return new TitleMachine(machineData);
    }

    public override RequestTitle Draft(RequestTitleData requestTitleData)
    {
        var machineData = new RequestTitleData
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
        
        return new TitleMachine(machineData);
    }

    public override void Update(RequestTitleData requestTitleData)
    {
        Validate(requestTitleData);
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        RegistrationNo = requestTitleData.RegistrationNo;
        MachineInfo = requestTitleData.MachineInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        RegistrationNo = requestTitleData.RegistrationNo;
        MachineInfo = requestTitleData.MachineInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public void Validate(RequestTitleData requestTitleData)
    {
        var ruleCheck = new RuleCheck();
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.RegistrationNo), "registrationNo is null or contains only whitespace.");
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.MachineInfo.MachineStatus), "machineStatus is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.MachineInfo.MachineType), "machineType  is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.MachineInfo.InstallationStatus), "installationStatus  is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.MachineInfo.InvoiceNumber), "invoiceNumber   is null or contains only whitespace.");

        ruleCheck.AddErrorIf(requestTitleData.MachineInfo.NumberOfMachinery is null || requestTitleData.MachineInfo.NumberOfMachinery < 0, "numberOfMachine must be greater than or equal to 0 or not null.");
        
        // TitleAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Postcode), "postcode is null or contains only whitespace.");
        // DopaAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Postcode), "postcode is null or contains only whitespace.");
        
        ruleCheck.ThrowIfInvalid();
    }
}

public sealed class TitleVehicle : RequestTitle
{
    public TitleVehicle(){}
    private TitleVehicle(RequestTitleData requestTitleData) : base(requestTitleData){}
    public override RequestTitle Create(RequestTitleData requestTitleData)
    {
        // validate
        var vehicleData = new RequestTitleData
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
        
        return new TitleVehicle(vehicleData);
    }

    public override RequestTitle Draft(RequestTitleData requestTitleData)
    {
        var vehicleData = new RequestTitleData
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
        
        return new TitleVehicle(vehicleData);
    }

    public override void Update(RequestTitleData requestTitleData)
    {
        // validate
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        RegistrationNo = requestTitleData.RegistrationNo;
        VehicleInfo = requestTitleData.VehicleInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        RegistrationNo = requestTitleData.RegistrationNo;
        VehicleInfo = requestTitleData.VehicleInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public void Validate(RequestTitleData requestTitleData)
    {
        var ruleCheck = new RuleCheck();
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.RegistrationNo), "registrationNo is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.VehicleInfo.VehicleType), "vehicleType");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.VehicleInfo.VehicleAppointmentLocation), "vehicleAppointmentLocation");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.VehicleInfo.ChassisNumber), "chassisNumber");
        
        // TitleAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Postcode), "postcode is null or contains only whitespace.");
        // DopaAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Postcode), "postcode is null or contains only whitespace.");
        
        ruleCheck.ThrowIfInvalid(); 
    }
}

public sealed class TitleVessel : RequestTitle
{
    public TitleVessel(){}
    private TitleVessel(RequestTitleData requestTitleData) : base(requestTitleData){}
    public override RequestTitle Create(RequestTitleData requestTitleData)
    {
        // validate
        var vesselData = new RequestTitleData
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
        
        return new TitleVessel(vesselData);
    }

    public override RequestTitle Draft(RequestTitleData requestTitleData)
    {
        var vesselData = new RequestTitleData
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
        
        return new TitleVessel(vesselData);
    }

    public override void Update(RequestTitleData requestTitleData)
    {
        // validate
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        RegistrationNo = requestTitleData.RegistrationNo;
        VehicleInfo = requestTitleData.VehicleInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        CollateralType = requestTitleData.CollateralType;
        CollateralStatus = requestTitleData.CollateralStatus;
        RegistrationNo = requestTitleData.RegistrationNo;
        VehicleInfo = requestTitleData.VehicleInfo;
        TitleAddress = requestTitleData.TitleAddress;
        DopaAddress = requestTitleData.DopaAddress;
        Notes = requestTitleData.Notes;
    }

    public void Validate(RequestTitleData requestTitleData)
    {
        var ruleCheck = new RuleCheck();
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.RegistrationNo), "registrationNo is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.VehicleInfo.VehicleType), "vehicleType");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.VehicleInfo.VehicleAppointmentLocation), "vehicleAppointmentLocation");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.VehicleInfo.ChassisNumber), "chassisNumber");
        
        // TitleAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.TitleAddress.Postcode), "postcode is null or contains only whitespace.");
        // DopaAddress
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(requestTitleData.DopaAddress.Postcode), "postcode is null or contains only whitespace.");
        
        ruleCheck.ThrowIfInvalid(); 
    }
}

public record RequestTitleData
{
    public Guid RequestId { get; init; }
    public string? CollateralType { get; init; }
    public bool? CollateralStatus { get; init; }
    public TitleDeedInfo TitleDeedInfo { get; init; } = default!;
    public SurveyInfo SurveyInfo { get; init; } = default!;
    public LandArea LandArea { get; init; } = default!;
    public string? OwnerName { get; init; }
    public string? RegistrationNo { get; init; }
    public VehicleInfo VehicleInfo { get; init; } = default!;
    public MachineInfo MachineInfo { get; init; } = default!;
    public BuildingInfo BuildingInfo { get; init; } = default!;
    public CondoInfo CondoInfo { get; init; } = default!;
    public Address TitleAddress { get; init; } = default!;
    public Address DopaAddress { get; init; } = default!;
    public string? Notes { get; init; }
}