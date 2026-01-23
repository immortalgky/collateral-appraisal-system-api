namespace Request.Extensions;

public static class DtoExtensions
{
    public static Reference ToDomain(this ReferenceDto? dto)
    {
        return Reference.Create(
            dto?.PrevAppraisalNo,
            dto?.PrevAppraisalValue,
            dto?.PrevAppraisalDate
        );
    }

    public static RequestDetail ToDomain(this RequestDetailDto? dto)
    {
        return RequestDetail.Create(new RequestDetailData(
            dto?.HasAppraisalBook ?? false,
            dto?.LoanDetail.ToDomain(),
            dto?.PrevAppraisalId,
            dto?.Address?.ToDomain(),
            dto?.Contact?.ToDomain(),
            dto?.Appointment?.ToDomain(),
            dto?.Fee?.ToDomain()
        ));
    }

    public static LoanDetail ToDomain(this LoanDetailDto? dto)
    {
        return LoanDetail.Create(new LoanDetailData(
            dto?.BankingSegment,
            dto?.LoanApplicationNumber,
            dto?.FacilityLimit,
            dto?.AdditionalFacilityLimit,
            dto?.PreviousFacilityLimit,
            dto?.TotalSellingPrice
        ));
    }

    public static Address ToDomain(this AddressDto? dto)
    {
        return Address.Create(new AddressData(
            dto?.HouseNumber,
            dto?.ProjectName,
            dto?.Moo,
            dto?.Soi,
            dto?.Road,
            dto?.SubDistrict,
            dto?.District,
            dto?.Province,
            dto?.Postcode
        ));
    }

    public static Contact ToDomain(this ContactDto? dto)
    {
        return Contact.Create(
            dto?.ContactPersonName,
            dto?.ContactPersonPhone,
            dto?.DealerCode
        );
    }

    public static Fee ToDomain(this FeeDto? dto)
    {
        return Fee.Create(
            dto?.FeePaymentType,
            dto?.FeeNotes,
            dto?.AbsorbedAmount
        );
    }

    public static SoftDelete ToDomain(this SoftDeleteDto dto)
    {
        return SoftDelete.Create(
            dto.IsDeleted,
            dto.DeletedOn,
            dto.DeletedBy
        );
    }

    public static Requestor ToDomain(this RequestorDto dto)
    {
        return Requestor.Create(
            dto.RequestorEmpId,
            dto.RequestorName,
            dto.RequestorEmail,
            dto.RequestorContactNo,
            dto.RequestorAo,
            dto.RequestorBranch,
            dto.RequestorBusinessUnit,
            dto.RequestorDepartment,
            dto.RequestorSection,
            dto.RequestorCostCenter
        );
    }


    public static Appointment ToDomain(this AppointmentDto? dto)
    {
        return Appointment.Create(
            dto?.AppointmentDate,
            dto?.AppointmentLocation
        );
    }

    public static RequestCustomer ToDomain(this RequestCustomerDto? dto)
    {
        return RequestCustomer.Create(
            dto?.Name,
            dto?.ContactNumber
        );
    }

    public static RequestProperty ToDomain(this RequestPropertyDto? dto)
    {
        return RequestProperty.Create(
            dto?.PropertyType,
            dto?.BuildingType,
            dto?.SellingPrice
        );
    }

    public static UserInfo ToDomain(this UserInfoDto dto)
    {
        return new UserInfo(dto.UserId, dto.Username);
    }

    public static TitleDeedInfo ToDomain(this TitleDeedInfoDto dto)
    {
        return TitleDeedInfo.Create(dto.TitleNo, dto.DeedType);
    }

    public static LandLocationInfo ToDomain(this LandLocationInfoDto dto)
    {
        return LandLocationInfo.Create(dto.BookNumber, dto.PageNumber, dto.LandParcelNumber, dto.SurveyNumber,
            dto.MapSheetNumber, dto.Rawang, dto.AerialMapName, dto.AerialMapNumber);
    }

    public static LandArea ToDomain(this LandAreaDto dto)
    {
        return LandArea.Of(dto.AreaRai, dto.AreaNgan, dto.AreaSquareWa);
    }

    public static VehicleInfo ToDomain(this VehicleDto dto)
    {
        return VehicleInfo.Create(dto.VehicleType, dto.VehicleAppointmentLocation, dto.VIN, dto.LicensePlateNumber);
    }

    public static VesselInfo ToDomain(this VesselDto dto)
    {
        return VesselInfo.Create(dto.VesselType, dto.VesselAppointmentLocation, dto.HullIdentificationNumber,
            dto.VesselRegistrationNumber);
    }

    public static MachineInfo ToDomain(this MachineDto dto)
    {
        return MachineInfo.Create(dto.registrationStatus, dto.registrationNo, dto.MachineType, dto.InstallationStatus,
            dto.InvoiceNumber,
            dto.NumberOfMachine);
    }

    public static BuildingInfo ToDomain(this BuildingInfoDto dto)
    {
        return BuildingInfo.Create(dto.BuildingType, dto.UsableArea, dto.NumberOfBuilding);
    }

    public static CondoInfo ToDomain(this CondoInfoDto dto)
    {
        return CondoInfo.Create(dto.CondoName, dto.BuildingNo, dto.RoomNo, dto.FloorNo, dto.UsableArea);
    }

    public static RequestTitleData ToRequestTitleData(this RequestTitleDto dto)
    {
        return new RequestTitleData
        {
            CollateralType = dto.CollateralType,
            CollateralStatus = dto.CollateralStatus,
            OwnerName = dto.OwnerName,
            TitleAddress = dto.TitleAddress.ToDomain(),
            DopaAddress = dto.DopaAddress.ToDomain(),
            Notes = dto.Notes,
            // Land-related fields
            TitleDeedInfo = TitleDeedInfo.Create(dto.TitleNo, dto.DeedType),
            LandLocationInfo = LandLocationInfo.Create(dto.BookNumber, dto.PageNumber, dto.LandParcelNumber,
                dto.SurveyNumber, dto.MapSheetNumber, dto.Rawang, dto.AerialMapName, dto.AerialMapNumber),
            LandArea = LandArea.Of(dto.AreaRai, dto.AreaNgan, dto.AreaSquareWa),
            // Building-related fields
            BuildingInfo = BuildingInfo.Create(dto.BuildingType, dto.UsableArea, dto.NumberOfBuilding),
            // Condo-related fields (UsableArea shared with Building in the same column)
            CondoInfo = CondoInfo.Create(dto.CondoName, dto.BuildingNo, dto.RoomNo, dto.FloorNo, dto.UsableArea),
            // Vehicle/Vessel/Machine fields
            VehicleInfo = VehicleInfo.Create(dto.VehicleType, dto.VehicleAppointmentLocation, dto.VIN,
                dto.LicensePlateNumber),
            VesselInfo = VesselInfo.Create(dto.VesselType, dto.VesselAppointmentLocation, dto.HullIdentificationNumber,
                dto.VesselRegistrationNumber),
            MachineInfo = MachineInfo.Create(dto.RegistrationStatus, dto.RegistrationNo, dto.MachineType,
                dto.InstallationStatus, dto.InvoiceNumber, dto.NumberOfMachine)
        };
    }

    public static TitleDocumentData ToTitleDocumentData(this RequestTitleDocumentDto dto)
    {
        return new TitleDocumentData
        {
            DocumentId = dto.DocumentId,
            DocumentType = dto.DocumentType,
            Filename = dto.Filename,
            Prefix = dto.Prefix,
            Set = dto.Set,
            Notes = dto.DocumentDescription,
            FilePath = dto.FilePath,
            UploadedBy = dto.UploadedBy,
            UploadedByName = dto.UploadedByName,
            UploadedAt = dto.UploadedAt
        };
    }

    public static RequestDocumentData ToRequestDocumentData(this RequestDocumentDto dto)
    {
        return new RequestDocumentData(
            dto.DocumentId,
            dto.DocumentType,
            dto.FileName,
            dto.Prefix,
            dto.Set,
            dto.Notes,
            dto.FilePath,
            dto.Source,
            dto.IsRequired,
            dto.UploadedBy,
            dto.UploadedByName,
            dto.UploadedAt
        );
    }
}