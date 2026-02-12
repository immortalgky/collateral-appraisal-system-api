namespace Request.Extensions;

public static class DtoExtensions
{
    // Domain → DTO mappings
    public static RequestDetailDto? ToDto(this RequestDetail? detail)
    {
        if (detail is null) return null;

        return new RequestDetailDto(
            detail.HasAppraisalBook,
            detail.LoanDetail?.ToDto(),
            detail.PrevAppraisalId,
            detail.Address?.ToDto(),
            detail.Contact?.ToDto(),
            detail.Appointment?.ToDto(),
            detail.Fee?.ToDto()
        );
    }

    public static LoanDetailDto? ToDto(this LoanDetail? loanDetail)
    {
        if (loanDetail is null) return null;

        return new LoanDetailDto(
            loanDetail.BankingSegment,
            loanDetail.LoanApplicationNumber,
            loanDetail.FacilityLimit,
            loanDetail.AdditionalFacilityLimit,
            loanDetail.PreviousFacilityLimit,
            loanDetail.TotalSellingPrice
        );
    }

    public static AddressDto? ToDto(this Address? address)
    {
        if (address is null) return null;

        return new AddressDto(
            address.HouseNumber,
            address.ProjectName,
            address.Moo,
            address.Soi,
            address.Road,
            address.SubDistrict,
            address.District,
            address.Province,
            address.Postcode
        );
    }

    public static ContactDto? ToDto(this Contact? contact)
    {
        if (contact is null) return null;

        return new ContactDto(
            contact.ContactPersonName,
            contact.ContactPersonPhone,
            contact.DealerCode
        );
    }

    public static AppointmentDto? ToDto(this Appointment? appointment)
    {
        if (appointment is null) return null;

        return new AppointmentDto(
            appointment.AppointmentDateTime,
            appointment.AppointmentLocation
        );
    }

    public static FeeDto? ToDto(this Fee? fee)
    {
        if (fee is null) return null;

        return new FeeDto(
            fee.FeePaymentType,
            fee.FeeNotes,
            fee.AbsorbedAmount
        );
    }

    public static RequestCustomerDto ToDto(this RequestCustomer customer)
    {
        return new RequestCustomerDto(customer.Name, customer.ContactNumber);
    }

    public static RequestPropertyDto ToDto(this RequestProperty property)
    {
        return new RequestPropertyDto(property.PropertyType, property.BuildingType, property.SellingPrice);
    }

    public static RequestDocumentDto ToDto(this RequestDocument document)
    {
        return new RequestDocumentDto(
            document.Id,
            document.RequestId,
            document.DocumentId,
            document.DocumentType,
            document.FileName,
            document.Prefix,
            document.Set,
            document.Notes,
            document.FilePath,
            document.Source,
            document.IsRequired,
            document.UploadedBy,
            document.UploadedByName,
            document.UploadedAt
        );
    }

    public static RequestTitleDocumentDto ToDto(this TitleDocument doc)
    {
        return new RequestTitleDocumentDto
        {
            Id = doc.Id,
            TitleId = doc.TitleId,
            DocumentId = doc.DocumentId,
            DocumentType = doc.DocumentType,
            FileName = doc.FileName,
            Prefix = doc.Prefix,
            Set = doc.Set,
            Notes = doc.Notes,
            FilePath = doc.FilePath,
            IsRequired = doc.IsRequired,
            UploadedBy = doc.UploadedBy,
            UploadedByName = doc.UploadedByName,
            UploadedAt = doc.UploadedAt
        };
    }

    public static RequestTitleDto ToDto(this RequestTitle title)
    {
        // Start with base properties all titles have
        var dto = new RequestTitleDto
        {
            Id = title.Id,
            RequestId = title.RequestId,
            CollateralType = title.CollateralType,
            CollateralStatus = title.CollateralStatus ?? false,
            OwnerName = title.OwnerName,
            TitleAddress = title.TitleAddress?.ToDto() ??
                           new AddressDto(null, null, null, null, null, null, null, null, null),
            DopaAddress = title.DopaAddress?.ToDto() ??
                          new AddressDto(null, null, null, null, null, null, null, null, null),
            Notes = title.Notes,
            Documents = title.Documents?.Select(d => d.ToDto()).ToList() ?? new List<RequestTitleDocumentDto>()
        };

        // Type-specific properties via pattern matching
        switch (title)
        {
            case TitleLand land:
                dto = dto with
                {
                    TitleNumber = land.TitleDeedInfo?.TitleNumber,
                    TitleType = land.TitleDeedInfo?.TitleType,
                    BookNumber = land.LandLocationInfo?.BookNumber,
                    PageNumber = land.LandLocationInfo?.PageNumber,
                    LandParcelNumber = land.LandLocationInfo?.LandParcelNumber,
                    SurveyNumber = land.LandLocationInfo?.SurveyNumber,
                    MapSheetNumber = land.LandLocationInfo?.MapSheetNumber,
                    Rawang = land.LandLocationInfo?.Rawang,
                    AerialMapName = land.LandLocationInfo?.AerialMapName,
                    AerialMapNumber = land.LandLocationInfo?.AerialMapNumber,
                    AreaRai = land.LandArea?.AreaRai,
                    AreaNgan = land.LandArea?.AreaNgan,
                    AreaSquareWa = land.LandArea?.AreaSquareWa
                };
                break;
            case TitleBuilding building:
                dto = dto with
                {
                    BuildingType = building.BuildingInfo?.BuildingType,
                    UsableArea = building.BuildingInfo?.UsableArea,
                    NumberOfBuilding = building.BuildingInfo?.NumberOfBuilding
                };
                break;
            case TitleLandBuilding landBuilding:
                dto = dto with
                {
                    TitleNumber = landBuilding.TitleDeedInfo?.TitleNumber,
                    TitleType = landBuilding.TitleDeedInfo?.TitleType,
                    BookNumber = landBuilding.LandLocationInfo?.BookNumber,
                    PageNumber = landBuilding.LandLocationInfo?.PageNumber,
                    LandParcelNumber = landBuilding.LandLocationInfo?.LandParcelNumber,
                    SurveyNumber = landBuilding.LandLocationInfo?.SurveyNumber,
                    MapSheetNumber = landBuilding.LandLocationInfo?.MapSheetNumber,
                    Rawang = landBuilding.LandLocationInfo?.Rawang,
                    AerialMapName = landBuilding.LandLocationInfo?.AerialMapName,
                    AerialMapNumber = landBuilding.LandLocationInfo?.AerialMapNumber,
                    AreaRai = landBuilding.LandArea?.AreaRai,
                    AreaNgan = landBuilding.LandArea?.AreaNgan,
                    AreaSquareWa = landBuilding.LandArea?.AreaSquareWa,
                    BuildingType = landBuilding.BuildingInfo?.BuildingType,
                    UsableArea = landBuilding.BuildingInfo?.UsableArea,
                    NumberOfBuilding = landBuilding.BuildingInfo?.NumberOfBuilding
                };
                break;
            case TitleCondo condo:
                dto = dto with
                {
                    TitleNumber = condo.TitleDeedInfo?.TitleNumber,
                    TitleType = condo.TitleDeedInfo?.TitleType,
                    CondoName = condo.CondoInfo?.CondoName,
                    BuildingNumber = condo.CondoInfo?.BuildingNumber,
                    RoomNumber = condo.CondoInfo?.RoomNumber,
                    FloorNumber = condo.CondoInfo?.FloorNumber,
                    UsableArea = condo.CondoInfo?.UsableArea
                };
                break;
            case TitleLeaseAgreementLand leaseLand:
                dto = dto with
                {
                    TitleNumber = leaseLand.TitleDeedInfo?.TitleNumber,
                    TitleType = leaseLand.TitleDeedInfo?.TitleType,
                    LandParcelNumber = leaseLand.LandLocationInfo?.LandParcelNumber,
                    SurveyNumber = leaseLand.LandLocationInfo?.SurveyNumber,
                    Rawang = leaseLand.LandLocationInfo?.Rawang,
                    AreaRai = leaseLand.LandArea?.AreaRai,
                    AreaNgan = leaseLand.LandArea?.AreaNgan,
                    AreaSquareWa = leaseLand.LandArea?.AreaSquareWa
                };
                break;
            case TitleLeaseAgreementBuilding leaseBuilding:
                dto = dto with
                {
                    BuildingType = leaseBuilding.BuildingInfo?.BuildingType,
                    UsableArea = leaseBuilding.BuildingInfo?.UsableArea,
                    NumberOfBuilding = leaseBuilding.BuildingInfo?.NumberOfBuilding
                };
                break;
            case TitleLeaseAgreementLandBuilding leaseLandBuilding:
                dto = dto with
                {
                    TitleNumber = leaseLandBuilding.TitleDeedInfo?.TitleNumber,
                    TitleType = leaseLandBuilding.TitleDeedInfo?.TitleType,
                    LandParcelNumber = leaseLandBuilding.LandLocationInfo?.LandParcelNumber,
                    SurveyNumber = leaseLandBuilding.LandLocationInfo?.SurveyNumber,
                    Rawang = leaseLandBuilding.LandLocationInfo?.Rawang,
                    AreaRai = leaseLandBuilding.LandArea?.AreaRai,
                    AreaNgan = leaseLandBuilding.LandArea?.AreaNgan,
                    AreaSquareWa = leaseLandBuilding.LandArea?.AreaSquareWa,
                    BuildingType = leaseLandBuilding.BuildingInfo?.BuildingType,
                    UsableArea = leaseLandBuilding.BuildingInfo?.UsableArea,
                    NumberOfBuilding = leaseLandBuilding.BuildingInfo?.NumberOfBuilding
                };
                break;
            case TitleLeaseAgreementCondo leaseCondo:
                dto = dto with
                {
                    TitleNumber = leaseCondo.TitleDeedInfo?.TitleNumber,
                    TitleType = leaseCondo.TitleDeedInfo?.TitleType,
                    CondoName = leaseCondo.CondoInfo?.CondoName,
                    BuildingNumber = leaseCondo.CondoInfo?.BuildingNumber,
                    RoomNumber = leaseCondo.CondoInfo?.RoomNumber,
                    FloorNumber = leaseCondo.CondoInfo?.FloorNumber,
                    UsableArea = leaseCondo.CondoInfo?.UsableArea
                };
                break;
            case TitleVehicle vehicle:
                dto = dto with
                {
                    VehicleType = vehicle.VehicleInfo?.VehicleType,
                    VehicleLocation = vehicle.VehicleInfo?.VehicleLocation,
                    VIN = vehicle.VehicleInfo?.VIN,
                    LicensePlateNumber = vehicle.VehicleInfo?.LicensePlateNumber
                };
                break;
            case TitleVessel vessel:
                dto = dto with
                {
                    VesselType = vessel.VesselInfo?.VesselType,
                    VesselLocation = vessel.VesselInfo?.VesselLocation,
                    HIN = vessel.VesselInfo?.HIN,
                    VesselRegistrationNumber = vessel.VesselInfo?.VesselRegistrationNumber
                };
                break;
            case TitleMachine machine:
                dto = dto with
                {
                    RegistrationStatus = machine.MachineInfo?.RegistrationStatus ?? false,
                    RegistrationNo = machine.MachineInfo?.RegistrationNumber,
                    MachineType = machine.MachineInfo?.MachineType,
                    InstallationStatus = machine.MachineInfo?.InstallationStatus,
                    InvoiceNumber = machine.MachineInfo?.InvoiceNumber,
                    NumberOfMachine = machine.MachineInfo?.NumberOfMachine
                };
                break;
        }

        return dto;
    }

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
            dto?.AppointmentDateTime,
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
            TitleDeedInfo = TitleDeedInfo.Create(dto.TitleNumber, dto.TitleType),
            LandLocationInfo = LandLocationInfo.Create(dto.BookNumber, dto.PageNumber, dto.LandParcelNumber,
                dto.SurveyNumber, dto.MapSheetNumber, dto.Rawang, dto.AerialMapName, dto.AerialMapNumber),
            LandArea = LandArea.Of(dto.AreaRai, dto.AreaNgan, dto.AreaSquareWa),
            // Building-related fields
            BuildingInfo = BuildingInfo.Create(dto.BuildingType, dto.UsableArea, dto.NumberOfBuilding),
            // Condo-related fields (UsableArea shared with Building in the same column)
            CondoInfo = CondoInfo.Create(dto.CondoName, dto.BuildingNumber, dto.RoomNumber, dto.FloorNumber,
                dto.UsableArea),
            // Vehicle/Vessel/Machine fields
            VehicleInfo = VehicleInfo.Create(dto.VehicleType, dto.VehicleLocation, dto.VIN,
                dto.LicensePlateNumber),
            VesselInfo = VesselInfo.Create(dto.VesselType, dto.VesselLocation, dto.HIN,
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
            FileName = dto.FileName,
            Prefix = dto.Prefix,
            Set = dto.Set,
            Notes = dto.Notes,
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