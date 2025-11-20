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
    public VehicleInfo VehicleInfo { get; private set; } = default!;
    public MachineInfo MachineInfo { get; private set; } = default!;
    public BuildingInfo BuildingInfo { get; private set; } = default!;
    public CondoInfo CondoInfo { get; private set; } = default!;
    public Address TitleAddress { get; private set; } = default!;
    public Address DopaAddress { get; private set; } = default!;
    public string? Notes { get; private set; }
    
    private readonly List<RequestTitleDocument> _requestTitleDocuments = [];
    public IReadOnlyList<RequestTitleDocument> RequestTitleDocuments => _requestTitleDocuments.AsReadOnly();

    private RequestTitle()
    {
        // For EF Core
    }

    public static RequestTitle Create(RequestTitleData requestTitleData)
    {
        RequestTitleValidator.Validate(requestTitleData);

        var requestTitle = new RequestTitle()
        {
            Id = Guid.NewGuid(),
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            SurveyInfo = requestTitleData.SurveyInfo,
            LandArea = requestTitleData.LandArea,
            OwnerName = requestTitleData.OwnerName,
            RegistrationNo = requestTitleData.RegistrationNo,
            VehicleInfo = requestTitleData.VehicleInfo,
            MachineInfo = requestTitleData.MachineInfo,
            BuildingInfo = requestTitleData.BuildingInfo,
            CondoInfo = requestTitleData.CondoInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        
        requestTitle.AddDomainEvent(new RequestTitleAddedEvent(requestTitle.RequestId, requestTitle));
        
        // requestTitle.AddIntegrationEvent(new DocumentLinkedIntegrationEvent("Title", requestTitle.Id, requestTitleDocuments.Select(rtd => rtd.DocumentId).ToList()));
        
        return requestTitle;
    }

    public static RequestTitle CreateDraft(RequestTitleData requestTitleData)
    {
        var requestTitle = new RequestTitle()
        {
            Id = Guid.NewGuid(),
            RequestId = requestTitleData.RequestId,
            CollateralType = requestTitleData.CollateralType,
            CollateralStatus = requestTitleData.CollateralStatus,
            TitleDeedInfo = requestTitleData.TitleDeedInfo,
            SurveyInfo = requestTitleData.SurveyInfo,
            LandArea = requestTitleData.LandArea,
            OwnerName = requestTitleData.OwnerName,
            RegistrationNo = requestTitleData.RegistrationNo,
            VehicleInfo = requestTitleData.VehicleInfo,
            MachineInfo = requestTitleData.MachineInfo,
            BuildingInfo = requestTitleData.BuildingInfo,
            CondoInfo = requestTitleData.CondoInfo,
            TitleAddress = requestTitleData.TitleAddress,
            DopaAddress = requestTitleData.DopaAddress,
            Notes = requestTitleData.Notes
        };
        
        requestTitle.AddDomainEvent(new RequestTitleAddedEvent(requestTitle.RequestId, requestTitle));

        // requestTitle.AddIntegrationEvent(new DocumentLinkedIntegrationEvent("Title", requestTitle.Id, requestTitleDocuments.Select(rtd => rtd.DocumentId).ToList()));
        
        return requestTitle;
    }

    public void UpdateDetails(RequestTitleData requestTitleData)
    {
        RequestTitleValidator.Validate(requestTitleData);

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

        // requestTitle.AddIntegrationEvent(new DocumentLinkedIntegrationEvent("Title", requestTitle.Id, requestTitleDocuments.Select(rtd => rtd.DocumentId).ToList()));
    }

    public void UpdateDraftDetails(RequestTitleData requestTitleData)
    {
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

        // requestTitle.AddIntegrationEvent(new DocumentLinkedIntegrationEvent("Title", requestTitle.Id, requestTitleDocuments.Select(rtd => rtd.DocumentId).ToList()));
    }

    public RequestTitleDocument CreateLinkRequestTitleDocument(RequestTitleDocumentData requestTitleDocumentData)
    {
        var requestTitleDoc =  RequestTitleDocument.Create(requestTitleDocumentData);
        _requestTitleDocuments.Add(requestTitleDoc);

        return requestTitleDoc;
    }
}

public record RequestTitleData(
    Guid RequestId,
    string? CollateralType,
    bool? CollateralStatus,
    TitleDeedInfo TitleDeedInfo,
    SurveyInfo SurveyInfo,
    LandArea LandArea,
    string? OwnerName,
    string? RegistrationNo,
    VehicleInfo VehicleInfo,
    MachineInfo MachineInfo,
    BuildingInfo BuildingInfo,
    CondoInfo CondoInfo,
    Address TitleAddress,
    Address DopaAddress,
    string? Notes
);

public static class RequestTitleValidator
{
    public static void Validate(RequestTitleData titleData)
    {
        var ruleCheck = new RuleCheck();
        // validate
        switch (titleData.CollateralType)
        {
            case "L": 
                TitleLandValidator.Validate(titleData,  ruleCheck);
                break;
            case "B":
                TitleBuildingValidator.Validate(titleData, ruleCheck);
                break;
            case "LB":
                TitleLandBuildingValidator.Validate(titleData, ruleCheck);
                break;
            case "C":
                TitleCondoValidator.Validate(titleData, ruleCheck);
                break;
            case "M":
                TitleMachineValidator.Validate(titleData, ruleCheck);
                break;
            case "V":
                TitleVehicleValidator.Validate(titleData, ruleCheck);
                break;
            default:
                break;
        }
        
        TitleAddressValidator.Validate(titleData.TitleAddress, ruleCheck);
        DopaAddressValidator.Validate(titleData.DopaAddress, ruleCheck);
        
        ruleCheck.ThrowIfInvalid();
    }
}

public static class TitleAddressValidator
{
    public static void Validate(Address titleAddress, RuleCheck ruleCheck)
    {
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.Postcode), "postcode is null or contains only whitespace.");
    }
}

public static class DopaAddressValidator
{
    public static void Validate(Address titleAddress, RuleCheck ruleCheck)
    {
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.SubDistrict), "subDistrict is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.District), "district is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.Province), "province is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleAddress.Postcode), "postcode is null or contains only whitespace.");
    }
}

public static class SurveyInfoValidator
{
    public static void Validate(SurveyInfo surveyInfo, RuleCheck ruleCheck)
    {
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(surveyInfo.Rawang), "rawang is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(surveyInfo.LandNo), "landNo is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(surveyInfo.SurveyNo), "surveyNo is null or contains only whitespace.");
    }
}

public static class BuildingInfoValidator
{
    public static void Validate(BuildingInfo buildingInfo, RuleCheck ruleCheck)
    {
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(buildingInfo.BuildingType), "buildingType is null or contains only whitespace.");
        ruleCheck.AddErrorIf(buildingInfo.UsableArea is null || buildingInfo.UsableArea < 0, "usableArea must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(buildingInfo.NumberOfBuilding is null || buildingInfo.NumberOfBuilding < 0, "numberOfBuildings must be greater than or equal to 0 or not null.");
    }
}

public static class LandAreaValidator
{
    public static void Validate(LandArea landArea, RuleCheck ruleCheck)
    {
        ruleCheck.AddErrorIf(landArea.AreaNgan is null || landArea.AreaNgan < 0, "areaNgan must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(landArea.AreaRai is null || landArea.AreaRai < 0, "areaRai must be greater than or equal to 0 or not null.");
        ruleCheck.AddErrorIf(landArea.AreaSquareWa is null || landArea.AreaSquareWa < 0, "areaSquareWa must be greater than or equal to 0 or not null.");
    }
}

public static class TitleDeedInfoValidator
{
    public static void Validate(TitleDeedInfo titleDeedInfo, RuleCheck ruleCheck)
    {
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleDeedInfo.DeedType), "deedType is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleDeedInfo.TitleNo), "titleNo is null or contains only whitespace.");

        var deedTypeChecklist = new List<string>() { "Chanote", "NorSor3", "NorSor3Kor" };
        ruleCheck.AddErrorIf(!deedTypeChecklist.Contains(titleDeedInfo.DeedType), "deedType is invalid.");
    }
}

public static class TitleLandValidator
{
    public static void Validate(RequestTitleData titleData, RuleCheck ruleCheck)
    {
        SurveyInfoValidator.Validate(titleData.SurveyInfo, ruleCheck);
        LandAreaValidator.Validate(titleData.LandArea, ruleCheck);
        TitleDeedInfoValidator.Validate(titleData.TitleDeedInfo, ruleCheck);
    }
}

public static class TitleBuildingValidator
{
    public static void Validate(RequestTitleData titleData, RuleCheck ruleCheck)
    {
        BuildingInfoValidator.Validate(titleData.BuildingInfo, ruleCheck);
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleData.TitleDeedInfo.TitleDetail), "titleDetail is null or contains only whitespace.");
    }
}

public static class TitleLandBuildingValidator
{
    public static void Validate(RequestTitleData titleData, RuleCheck ruleCheck)
    {
        TitleLandValidator.Validate(titleData, ruleCheck);
        TitleBuildingValidator.Validate(titleData, ruleCheck);
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleData.TitleDeedInfo.TitleDetail), "titleDetail is null or contains only whitespace.");
    }
}

public static class TitleMachineValidator
{
    public static void Validate(RequestTitleData titleData, RuleCheck ruleCheck)
    {
        MachineInfoValidator.Validate(titleData.MachineInfo, ruleCheck);
    }
}

public static class TitleVehicleValidator
{
    public static void Validate(RequestTitleData titleData, RuleCheck ruleCheck)
    {
        VehicleInfoValidator.Validate(titleData.VehicleInfo, ruleCheck);
    }
}

public static class VehicleInfoValidator
{
    public static void Validate(VehicleInfo vehicleInfo, RuleCheck ruleCheck)
    {
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(vehicleInfo.VehicleType), "vehicleType");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(vehicleInfo.VehicleAppointmentLocation), "vehicleAppointmentLocation");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(vehicleInfo.ChassisNumber), "chassisNumber");
    }
}

public static class MachineInfoValidator
{
    public static void Validate(MachineInfo machineInfo, RuleCheck ruleCheck)
    {
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(machineInfo.MachineStatus), "machineStatus is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(machineInfo.MachineType), "machineType  is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(machineInfo.InstallationStatus), "installationStatus  is null or contains only whitespace.");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(machineInfo.InvoiceNumber), "invoiceNumber   is null or contains only whitespace.");

        ruleCheck.AddErrorIf(machineInfo.NumberOfMachinery is null || machineInfo.NumberOfMachinery < 0, "numberOfMachine must be greater than or equal to 0 or not null.");
    }
}

public static class CondoInfoValidator
{
    public static void Validate(CondoInfo condoInfo, RuleCheck ruleCheck)
    {
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(condoInfo.CondoName), "condoName");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(condoInfo.BuildingNo), "buildingNo");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(condoInfo.RoomNo), "roomNo");
        ruleCheck.AddErrorIf(string.IsNullOrEmpty(condoInfo.FloorNo), "floorNo");
    }
}

public static class TitleCondoValidator
{
    public static void Validate(RequestTitleData titleData, RuleCheck ruleCheck)
    {
        CondoInfoValidator.Validate(titleData.CondoInfo, ruleCheck);
        
        ruleCheck.AddErrorIf(string.IsNullOrWhiteSpace(titleData.OwnerName), "ownerName is null or contains only whitespace.");
        ruleCheck.AddErrorIf(titleData.BuildingInfo.UsableArea is null || titleData.BuildingInfo.UsableArea < 0, "usableArea must be greater than or equal to 0 or not null.");
    }
}