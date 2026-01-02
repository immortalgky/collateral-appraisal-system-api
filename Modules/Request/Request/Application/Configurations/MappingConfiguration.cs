using Request.Application.ReadModels;

namespace Request.Application.Configurations;

public static class MappingConfiguration
{
    public static void ConfigureMappings()
    {
        TypeAdapterConfig<LoanDetailDto, LoanDetail>
            .NewConfig()
            .ConstructUsing(src => LoanDetail.Create(new LoanDetailData(
                src.BankingSegment,
                src.LoanApplicationNumber,
                src.FacilityLimit,
                src.AdditionalFacilityLimit,
                src.PreviousFacilityLimit,
                src.TotalSellingPrice)));

        TypeAdapterConfig<ReferenceDto, Reference>
            .NewConfig()
            .ConstructUsing(src => Reference.Create(
                src.PrevAppraisalNo,
                src.PrevAppraisalValue,
                src.PrevAppraisalDate
            ));

        TypeAdapterConfig<AddressDto, Address>
            .NewConfig()
            .ConstructUsing(src => Address.Create(new AddressData(
                src.HouseNumber,
                src.ProjectName,
                src.Moo,
                src.Soi,
                src.Road,
                src.SubDistrict,
                src.District,
                src.Province,
                src.Postcode
            )));

        TypeAdapterConfig<ContactDto, Contact>
            .NewConfig()
            .ConstructUsing(src => Contact.Create(
                src.ContactPersonName,
                src.ContactPersonPhone,
                src.DealerCode
            ));

        TypeAdapterConfig<FeeDto, Fee>
            .NewConfig()
            .ConstructUsing(src => Fee.Create(
                src.FeePaymentType,
                src.FeeNotes,
                src.AbsorbedAmount
            ));

        TypeAdapterConfig<RequestorDto, Requestor>
            .NewConfig()
            .ConstructUsing(src => Requestor.Create(
                src.RequestorEmpId,
                src.RequestorName,
                src.RequestorEmail,
                src.RequestorContactNo,
                src.RequestorAo,
                src.RequestorBranch,
                src.RequestorBusinessUnit,
                src.RequestorDepartment,
                src.RequestorSection,
                src.RequestorCostCenter
            ));

        TypeAdapterConfig<RequestCustomerDto, RequestCustomer>
            .NewConfig()
            .ConstructUsing(src => RequestCustomer.Create(
                src.Name,
                src.ContactNumber
            ));

        TypeAdapterConfig<RequestPropertyDto, RequestProperty>
            .NewConfig()
            .ConstructUsing(src => RequestProperty.Create(
                src.PropertyType,
                src.BuildingType,
                src.SellingPrice
            ));

        TypeAdapterConfig<SourceDto, Source>
            .NewConfig()
            .ConstructUsing(src => Source.Create(
                src.RequestedBy,
                src.RequestedByName,
                src.Channel
            ));

        // TitleDocument mappings (DTO -> Data for creation/update)
        TypeAdapterConfig<RequestTitleDocumentDto, TitleDocumentData>
            .NewConfig()
            .Map(dest => dest.DocumentId, src => src.DocumentId)
            .Map(dest => dest.DocumentType, src => src.DocumentType)
            .Map(dest => dest.Filename, src => src.Filename)
            .Map(dest => dest.Prefix, src => src.Prefix)
            .Map(dest => dest.Set, src => src.Set)
            .Map(dest => dest.DocumentDescription, src => src.DocumentDescription)
            .Map(dest => dest.FilePath, src => src.FilePath)
            .Map(dest => dest.CreatedWorkstation, src => src.CreatedWorkstation)
            .Map(dest => dest.UploadedBy, src => src.UploadedBy)
            .Map(dest => dest.UploadedByName, src => src.UploadedByName)
            .Map(dest => dest.UploadedAt, src => src.UploadedAt);

        // TitleDocument mappings (Domain -> DTO for queries)
        TypeAdapterConfig<TitleDocument, RequestTitleDocumentDto>
            .NewConfig()
            .Map(dest => dest.Id, src => src.Id)
            .Map(dest => dest.TitleId, src => src.TitleId)
            .Map(dest => dest.DocumentId, src => src.DocumentId)
            .Map(dest => dest.DocumentType, src => src.DocumentType)
            .Map(dest => dest.Filename, src => src.Filename)
            .Map(dest => dest.Prefix, src => src.Prefix)
            .Map(dest => dest.Set, src => src.Set)
            .Map(dest => dest.DocumentDescription, src => src.DocumentDescription)
            .Map(dest => dest.FilePath, src => src.FilePath)
            .Map(dest => dest.CreatedWorkstation, src => src.CreatedWorkstation)
            .Map(dest => dest.IsRequired, src => src.IsRequired)
            .Map(dest => dest.UploadedBy, src => src.UploadedBy)
            .Map(dest => dest.UploadedByName, src => src.UploadedByName);

        // Row → DTO mappings
        TypeAdapterConfig<RequestCustomerRow, RequestCustomerDto>
            .NewConfig()
            .Map(dest => dest.Name, src => src.CustomerName);

        TypeAdapterConfig<RequestDocumentRow, RequestDocumentDto>
            .NewConfig()
            .Map(dest => dest.Set, src => (short?)src.Set)
            .Map(dest => dest.Prefix, src => (string?)null)
            .Map(dest => dest.FilePath, src => (string?)null)
            .Map(dest => dest.Source, src => (string?)null)
            .Map(dest => dest.IsRequired, src => false);

        TypeAdapterConfig<RequestTitleRow, RequestTitleDto>
            .NewConfig()
            .Ignore(dest => dest.Documents)
            .Map(dest => dest.TitleAddress, src => new AddressDto(
                src.HouseNumber,
                src.ProjectName,
                src.Moo,
                src.Soi,
                src.Road,
                src.SubDistrict,
                src.District,
                src.Province,
                src.Postcode))
            .Map(dest => dest.DopaAddress, src => new AddressDto(
                src.DopaHouseNumber,
                src.DopaProjectName,
                src.DopaMoo,
                src.DopaSoi,
                src.DopaRoad,
                src.DopaSubDistrict,
                src.DopaDistrict,
                src.DopaProvince,
                src.DopaPostcode));

        TypeAdapterConfig<RequestRow, RequestDetailDto>
            .NewConfig()
            .Map(dest => dest.LoanDetail, src => src.Adapt<LoanDetailDto>())
            .Map(dest => dest.Address, src => src.Adapt<AddressDto>())
            .Map(dest => dest.Contact, src => src.Adapt<ContactDto>())
            .Map(dest => dest.Appointment, src => src.Adapt<AppointmentDto>())
            .Map(dest => dest.Fee, src => src.Adapt<FeeDto>());
    }
}