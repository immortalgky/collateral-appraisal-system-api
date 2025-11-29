using Microsoft.CodeAnalysis.CSharp.Syntax;

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
    protected void Sync(RequestTitleData requestTitleData)
    {
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

public enum CollateralType
{
    Land,
    LeaseAgreementLand,
    Building,
    LeaseAgreementBuilding,
    LandAndBuilding,
    LeaseAgreementLandAndBuilding,
    Condo,
    LeaseAgreementCondo,
    Machine,
    Vehicle,
    Vessel
}

public class CollateralTypeMapping
{
    public static string ToCode(CollateralType type)
    {
        return type switch
        {
            CollateralType.Land => "L",
            CollateralType.LeaseAgreementLand => "LA-L",
            CollateralType.Building => "B",
            CollateralType.LeaseAgreementBuilding => "LA-B",
            CollateralType.LandAndBuilding => "LB",
            CollateralType.LeaseAgreementLandAndBuilding => "LA-LB",
            CollateralType.Condo => "C",
            CollateralType.LeaseAgreementCondo => "LA-C",
            CollateralType.Machine => "M",
            CollateralType.Vehicle => "V",
            CollateralType.Vessel => "VES",
            _ => throw new Exception("Collateral Type is out of scope.")
        };
    }

    public static CollateralType FromCode(string code) => code switch
    {
        "L" => CollateralType.Land,
        "LA-L" => CollateralType.LeaseAgreementLand,
        "B" => CollateralType.Building,
        "LA-B" => CollateralType.LeaseAgreementBuilding,
        "LB" => CollateralType.LandAndBuilding,
        "LA-LB" => CollateralType.LeaseAgreementLandAndBuilding,
        "C" => CollateralType.Condo,
        "LA-C" => CollateralType.LeaseAgreementCondo,
        "M" => CollateralType.Machine,
        "V" => CollateralType.Vehicle,
        "VES" => CollateralType.Vessel,
        _ => throw new Exception("CollateralType is out of scope.")
    };

    public static string DisplayName(string code) => code switch
    {
        "L" => "Land",
        "LA-L" => "Lease Agreement Land",
        "B" => "Building",
        "LA-B" => "Lease Agreement Building",
        "LB" => "Land and Building",
        "LA-LB" => "Lease Land and Building",
        "C" => "Condo",
        "LA-C" => "Lease Agreement Condo",
        "M" => "Machine",
        "V" => "Vehicle",
        "VES" => "Vessel",
        _ => code
    };
} 

public class RequestTitleFactory
{
    public static RequestTitle Create(string code)
    {
        return CollateralTypeMapping.FromCode(code) switch
        {
            CollateralType.Land => new TitleLand(),
            CollateralType.LeaseAgreementLand => new TitleLeaseAgreementLand(),
            CollateralType.Building => new TitleBuilding(),
            CollateralType.LeaseAgreementBuilding => new TitleLeaseAgreementBuilding(),
            CollateralType.LandAndBuilding => new TitleLandBuilding(),
            CollateralType.LeaseAgreementLandAndBuilding => new TitleLeaseAgreementLandBuilding(),
            CollateralType.Condo => new TitleCondo(),
            CollateralType.LeaseAgreementCondo => new TitleLeaseAgreementCondo(),
            CollateralType.Machine => new TitleMachine(),
            CollateralType.Vehicle => new TitleVehicle(),
            CollateralType.Vessel => new TitleVessel(),
            _ => throw new ArgumentException($"{code} is out of scope.")
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
        return new TitleLand(landData);
    }

    public override void Update(RequestTitleData requestTitleData)
    {
        Validate(requestTitleData);

        var landData = new RequestTitleData
        {
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            SurveyInfo = requestTitleData.SurveyInfo,
            LandArea = requestTitleData.LandArea,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };

        base.Sync(landData);
    }
    
    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
         var landData = new RequestTitleData
        {
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            SurveyInfo = requestTitleData.SurveyInfo,
            LandArea = requestTitleData.LandArea,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };

        base.Sync(landData);
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
        var leaseAgreementLandData = new RequestTitleData
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
        return new TitleLeaseAgreementLand(leaseAgreementLandData);
    }

    public override TitleLeaseAgreementLand Draft(RequestTitleData requestTitleData)
    {
        var leaseAgreementLandData = new RequestTitleData
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
        return new TitleLeaseAgreementLand(leaseAgreementLandData);
    }

    public override void Update(RequestTitleData requestTitleData)
    {
        Validate(requestTitleData);
        var leaseAgreementLandData = new RequestTitleData
        {
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            SurveyInfo = requestTitleData.SurveyInfo,
            LandArea = requestTitleData.LandArea,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };

        base.Sync(leaseAgreementLandData);
    }
    
    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        var leaseAgreementLandData = new RequestTitleData
        {
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            SurveyInfo = requestTitleData.SurveyInfo,
            LandArea = requestTitleData.LandArea,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };

        base.Sync(leaseAgreementLandData);
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
        var buildingData = new RequestTitleData
        {
            CollateralStatus = requestTitleData.CollateralStatus,
            BuildingInfo = requestTitleData.BuildingInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };

        base.Sync(buildingData);
    }
    
    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        var buildingData = new RequestTitleData
        {
            CollateralStatus = requestTitleData.CollateralStatus,
            BuildingInfo = requestTitleData.BuildingInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };

        base.Sync(buildingData);
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
        var leaseAgreementBuildingData = new RequestTitleData
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
        return new TitleLeaseAgreementBuilding(leaseAgreementBuildingData);
    }

    public override TitleLeaseAgreementBuilding Draft(RequestTitleData requestTitleData)
    {
        var leaseAgreementBuildingData = new RequestTitleData
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
        return new TitleLeaseAgreementBuilding(leaseAgreementBuildingData);
    }

    public override void Update(RequestTitleData requestTitleData)
    {
        Validate(requestTitleData);
        var leaseAgreementBuildingData = new RequestTitleData
        {
            CollateralStatus = requestTitleData.CollateralStatus,
            BuildingInfo = requestTitleData.BuildingInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };

        base.Sync(leaseAgreementBuildingData);
    }
    
    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        var leaseAgreementBuildingData = new RequestTitleData
        {
            CollateralStatus = requestTitleData.CollateralStatus,
            BuildingInfo = requestTitleData.BuildingInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };

        base.Sync(leaseAgreementBuildingData);
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
        var landBuildingData = new RequestTitleData
        {
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

        base.Sync(landBuildingData);
    }

    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        var landBuildingData = new RequestTitleData
        {
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

        base.Sync(landBuildingData);
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
        var leaseAgreementLandBuildingData = new RequestTitleData
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
        return new TitleLeaseAgreementLandBuilding(leaseAgreementLandBuildingData);
    }

    public override TitleLeaseAgreementLandBuilding Draft(RequestTitleData requestTitleData)
    {
        var leaseAgreementLandBuildingData = new RequestTitleData
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
        return new TitleLeaseAgreementLandBuilding(leaseAgreementLandBuildingData);
    }

    public override void Update(RequestTitleData requestTitleData)
    {
        Validate(requestTitleData);
        var leaseAgreementLandBuildingData = new RequestTitleData
        {
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
        base.Sync(leaseAgreementLandBuildingData);
    }

    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        var leaseAgreementLandBuildingData = new RequestTitleData
        {
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
        base.Sync(leaseAgreementLandBuildingData);
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
        Validate(requestTitleData);
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
        Validate(requestTitleData);
        var condoData = new RequestTitleData
        {
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            OwnerName = requestTitleData.OwnerName,
            BuildingInfo = requestTitleData.BuildingInfo,
            CondoInfo = requestTitleData.CondoInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        base.Sync(condoData);
    }

    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        var condoData = new RequestTitleData
        {
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            OwnerName = requestTitleData.OwnerName,
            BuildingInfo = requestTitleData.BuildingInfo,
            CondoInfo = requestTitleData.CondoInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        base.Sync(condoData);
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
        Validate(requestTitleData);
        var leaseAgreementCondoData = new RequestTitleData
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
        
        return new TitleLeaseAgreementCondo(leaseAgreementCondoData);
    }

    public override RequestTitle Draft(RequestTitleData requestTitleData)
    {
        var leaseAgreementCondoData = new RequestTitleData
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
        
        return new TitleLeaseAgreementCondo(leaseAgreementCondoData);
    }

    public override void Update(RequestTitleData requestTitleData)
    {
        Validate(requestTitleData);
        var leaseAgreementCondoData = new RequestTitleData
        {
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            OwnerName = requestTitleData.OwnerName,
            BuildingInfo = requestTitleData.BuildingInfo,
            CondoInfo = requestTitleData.CondoInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        base.Sync(leaseAgreementCondoData);
    }

    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        var leaseAgreementCondoData = new RequestTitleData
        {
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            OwnerName = requestTitleData.OwnerName,
            BuildingInfo = requestTitleData.BuildingInfo,
            CondoInfo = requestTitleData.CondoInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        base.Sync(leaseAgreementCondoData);
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
        var machineData = new RequestTitleData
        {
            CollateralStatus = requestTitleData.CollateralStatus,
            RegistrationNo = requestTitleData.RegistrationNo,
            MachineInfo = requestTitleData.MachineInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        base.Sync(machineData);
    }

    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        var machineData = new RequestTitleData
        {
            CollateralStatus = requestTitleData.CollateralStatus,
            RegistrationNo = requestTitleData.RegistrationNo,
            MachineInfo = requestTitleData.MachineInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        base.Sync(machineData);
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
        Validate(requestTitleData);
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
        Validate(requestTitleData);
        var vehicleData = new RequestTitleData
        {
            CollateralStatus = requestTitleData.CollateralStatus,
            RegistrationNo = requestTitleData.RegistrationNo,
            VehicleInfo = requestTitleData.VehicleInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        base.Sync(vehicleData);
    }

    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        var vehicleData = new RequestTitleData
        {
            CollateralStatus = requestTitleData.CollateralStatus,
            RegistrationNo = requestTitleData.RegistrationNo,
            VehicleInfo = requestTitleData.VehicleInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        base.Sync(vehicleData);
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
        Validate(requestTitleData);
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
        Validate(requestTitleData);
        var vesselData = new RequestTitleData
        {
            CollateralStatus = requestTitleData.CollateralStatus,
            RegistrationNo = requestTitleData.RegistrationNo,
            VehicleInfo = requestTitleData.VehicleInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        base.Sync(vesselData);
    }

    public override void UpdateDraft(RequestTitleData requestTitleData)
    {
        var vesselData = new RequestTitleData
        {
            CollateralStatus = requestTitleData.CollateralStatus,
            RegistrationNo = requestTitleData.RegistrationNo,
            VehicleInfo = requestTitleData.VehicleInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        base.Sync(vesselData);
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