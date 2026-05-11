using Dapper;
using Request.Contracts.RequestDocuments.Dto;
using Request.Contracts.Requests.Dtos;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalCopyTemplate;

/// <summary>
/// Returns a copy-ready snapshot of a completed appraisal's request data.
/// Uses Dapper + cross-schema joins (appraisal → request) — both schemas
/// live in the same SQL Server database so the join is free.
/// </summary>
public class GetAppraisalCopyTemplateQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetAppraisalCopyTemplateQuery, AppraisalCopyTemplateDto>
{
    public async Task<AppraisalCopyTemplateDto> Handle(
        GetAppraisalCopyTemplateQuery query,
        CancellationToken cancellationToken)
    {
        var connection = connectionFactory.GetOpenConnection();
        var parameters = new DynamicParameters();
        parameters.Add("AppraisalId", query.AppraisalId);

        // ── 1. Header row (from the view we created) ──────────────────────
        const string headerSql = """
            SELECT AppraisalId, AppraisalNumber, AppointmentDate, [Status], AppraisalValue, RequestId,
                   HouseNumber, ProjectName, Moo, Soi, Road, SubDistrict, District, Province, Postcode,
                   ContactPersonName, ContactPersonPhone, DealerCode,
                   BankingSegment, LoanApplicationNumber, FacilityLimit,
                   AdditionalFacilityLimit, PreviousFacilityLimit, TotalSellingPrice,
                   HasAppraisalBook
            FROM appraisal.vw_AppraisalCopyTemplate
            WHERE AppraisalId = @AppraisalId
            """;

        var header = await connection.QueryFirstOrDefaultAsync<CopyTemplateHeaderRow>(headerSql, parameters);

        if (header is null)
            throw new AppraisalNotFoundException(query.AppraisalId);

        if (header.Status != "Completed")
            throw new ConflictException(
                $"Appraisal '{header.AppraisalNumber ?? query.AppraisalId.ToString()}' is not eligible for copy " +
                $"because its status is '{header.Status}'. Only Completed appraisals can be copied.");

        // ── 2. Customers ──────────────────────────────────────────────────
        var requestParams = new DynamicParameters();
        requestParams.Add("RequestId", header.RequestId);

        const string customerSql = "SELECT Name, ContactNumber FROM request.RequestCustomers WHERE RequestId = @RequestId";
        var customerRows = await connection.QueryAsync<CustomerRow>(customerSql, requestParams);

        // ── 3. Properties ─────────────────────────────────────────────────
        const string propertySql = "SELECT PropertyType, BuildingType, SellingPrice FROM request.RequestProperties WHERE RequestId = @RequestId";
        var propertyRows = await connection.QueryAsync<PropertyRow>(propertySql, requestParams);

        // ── 4. Documents (reference-copy only — filename + storage key) ───
        const string documentSql = """
            SELECT Id, RequestId, DocumentId, DocumentType, FileName, Prefix, [Set], Notes,
                   FilePath, Source, IsRequired, UploadedBy, UploadedByName, UploadedAt
            FROM request.RequestDocuments
            WHERE RequestId = @RequestId
            """;
        var documentRows = await connection.QueryAsync<DocumentRow>(documentSql, requestParams);

        // ── 5. Titles (flat TPH table — all collateral-type columns are on the same row) ─
        // RequestTitles uses table-per-hierarchy; owned entities are stored as flat columns.
        const string titleSql = """
            SELECT
                t.Id, t.RequestId, t.CollateralType, t.CollateralStatus,
                -- TitleDeedInfo (flat columns on same table)
                t.TitleNumber, t.TitleType,
                -- LandLocationInfo (flat columns on same table)
                t.BookNumber, t.PageNumber, t.LandParcelNumber, t.SurveyNumber,
                t.MapSheetNumber, t.Rawang, t.AerialMapName, t.AerialMapNumber,
                -- LandArea (flat columns on same table)
                t.AreaRai, t.AreaNgan, t.AreaSquareWa,
                -- Shared
                t.OwnerName,
                -- VehicleInfo (flat columns on same table)
                t.VehicleType, t.VehicleLocation, t.VIN, t.LicensePlateNumber,
                -- VesselInfo (flat columns on same table)
                t.VesselType, t.VesselLocation, t.HIN, t.VesselRegistrationNumber,
                -- MachineInfo (flat columns on same table)
                t.RegistrationStatus, t.RegistrationNumber AS RegistrationNo,
                t.MachineType, t.InstallationStatus, t.InvoiceNumber, t.NumberOfMachine,
                -- BuildingInfo (flat columns on same table)
                t.BuildingType, t.UsableArea, t.NumberOfBuilding,
                -- CondoInfo (flat columns on same table)
                t.CondoName, t.BuildingNumber, t.RoomNumber, t.FloorNumber,
                -- TitleAddress (flat columns on same table; prefixed in alias for mapping)
                t.HouseNumber    AS TitleHouseNumber,
                t.ProjectName    AS TitleProjectName,
                t.Moo            AS TitleMoo,
                t.Soi            AS TitleSoi,
                t.Road           AS TitleRoad,
                t.SubDistrict    AS TitleSubDistrict,
                t.District       AS TitleDistrict,
                t.Province       AS TitleProvince,
                t.Postcode       AS TitlePostcode,
                -- DopaAddress (flat columns on same table)
                t.DopaHouseNumber,
                t.DopaProjectName,
                t.DopaMoo,
                t.DopaSoi,
                t.DopaRoad,
                t.DopaSubDistrict,
                t.DopaDistrict,
                t.DopaProvince,
                t.DopaPostcode,
                t.Notes
            FROM request.RequestTitles t
            WHERE t.RequestId = @RequestId
            """;
        var titleRows = await connection.QueryAsync<TitleRow>(titleSql, requestParams);

        // ── Map to DTOs ───────────────────────────────────────────────────

        var prevAppraisal = new PrevAppraisalSnapshotDto(
            header.AppraisalId,
            header.AppraisalNumber,
            header.AppraisalValue,       // From appraisal.ValuationAnalyses.AppraisedValue (LEFT JOIN; null if no valuation yet)
            header.AppointmentDate);

        var detail = new RequestDetailCopyDto(
            header.HasAppraisalBook,
            new LoanDetailDto(
                header.BankingSegment,
                header.LoanApplicationNumber,
                header.FacilityLimit,
                header.AdditionalFacilityLimit,
                header.PreviousFacilityLimit,
                header.TotalSellingPrice),
            new AddressDto(
                header.HouseNumber,
                header.ProjectName,
                header.Moo,
                header.Soi,
                header.Road,
                header.SubDistrict,
                header.District,
                header.Province,
                header.Postcode),
            new ContactDto(
                header.ContactPersonName,
                header.ContactPersonPhone,
                header.DealerCode));

        var customers = customerRows
            .Select(r => new RequestCustomerDto(r.Name, r.ContactNumber))
            .ToList();

        var properties = propertyRows
            .Select(r => new RequestPropertyDto(r.PropertyType, r.BuildingType, r.SellingPrice))
            .ToList();

        var documents = documentRows
            .Select(r => new RequestDocumentDto(
                r.Id,
                r.RequestId,
                r.DocumentId,
                r.DocumentType,
                r.FileName,
                r.Prefix,
                r.Set,
                r.Notes,
                r.FilePath,
                r.Source,
                r.IsRequired,
                r.UploadedBy,
                r.UploadedByName,
                r.UploadedAt))
            .ToList();

        var titles = titleRows
            .Select(r => new RequestTitleDto
            {
                Id              = r.Id,
                RequestId       = r.RequestId,
                CollateralType  = r.CollateralType,
                CollateralStatus = r.CollateralStatus,
                TitleNumber     = r.TitleNumber,
                TitleType       = r.TitleType,
                BookNumber      = r.BookNumber,
                PageNumber      = r.PageNumber,
                LandParcelNumber = r.LandParcelNumber,
                SurveyNumber    = r.SurveyNumber,
                MapSheetNumber  = r.MapSheetNumber,
                Rawang          = r.Rawang,
                AerialMapName   = r.AerialMapName,
                AerialMapNumber = r.AerialMapNumber,
                AreaRai         = r.AreaRai,
                AreaNgan        = r.AreaNgan,
                AreaSquareWa    = r.AreaSquareWa,
                OwnerName       = r.OwnerName,
                VehicleType     = r.VehicleType,
                VehicleLocation = r.VehicleLocation,
                VIN             = r.VIN,
                LicensePlateNumber = r.LicensePlateNumber,
                VesselType      = r.VesselType,
                VesselLocation  = r.VesselLocation,
                HIN             = r.HIN,
                VesselRegistrationNumber = r.VesselRegistrationNumber,
                RegistrationStatus = r.RegistrationStatus,
                RegistrationNo  = r.RegistrationNo,
                MachineType     = r.MachineType,
                InstallationStatus = r.InstallationStatus,
                InvoiceNumber   = r.InvoiceNumber,
                NumberOfMachine = r.NumberOfMachine,
                BuildingType    = r.BuildingType,
                UsableArea      = r.UsableArea,
                NumberOfBuilding = r.NumberOfBuilding,
                CondoName       = r.CondoName,
                BuildingNumber  = r.BuildingNumber,
                RoomNumber      = r.RoomNumber,
                FloorNumber     = r.FloorNumber,
                TitleAddress    = new AddressDto(
                    r.TitleHouseNumber, r.TitleProjectName, r.TitleMoo, r.TitleSoi, r.TitleRoad,
                    r.TitleSubDistrict, r.TitleDistrict, r.TitleProvince, r.TitlePostcode),
                DopaAddress     = new AddressDto(
                    r.DopaHouseNumber, r.DopaProjectName, r.DopaMoo, r.DopaSoi, r.DopaRoad,
                    r.DopaSubDistrict, r.DopaDistrict, r.DopaProvince, r.DopaPostcode),
                Notes           = r.Notes,
                Documents       = []
            })
            .ToList();

        return new AppraisalCopyTemplateDto(prevAppraisal, detail, customers, properties, titles, documents);
    }

    // ── Private flat-row types for Dapper hydration ──────────────────────

    private class CopyTemplateHeaderRow
    {
        public Guid AppraisalId { get; set; }
        public string? AppraisalNumber { get; set; }
        public DateTime? AppointmentDate { get; set; }
        public string Status { get; set; } = "";
        public decimal? AppraisalValue { get; set; }
        public Guid RequestId { get; set; }
        public string? HouseNumber { get; set; }
        public string? ProjectName { get; set; }
        public string? Moo { get; set; }
        public string? Soi { get; set; }
        public string? Road { get; set; }
        public string? SubDistrict { get; set; }
        public string? District { get; set; }
        public string? Province { get; set; }
        public string? Postcode { get; set; }
        public string? ContactPersonName { get; set; }
        public string? ContactPersonPhone { get; set; }
        public string? DealerCode { get; set; }
        public string? BankingSegment { get; set; }
        public string? LoanApplicationNumber { get; set; }
        public decimal? FacilityLimit { get; set; }
        public decimal? AdditionalFacilityLimit { get; set; }
        public decimal? PreviousFacilityLimit { get; set; }
        public decimal? TotalSellingPrice { get; set; }
        public bool HasAppraisalBook { get; set; }
    }

    private class CustomerRow
    {
        public string? Name { get; set; }
        public string? ContactNumber { get; set; }
    }

    private class PropertyRow
    {
        public string? PropertyType { get; set; }
        public string? BuildingType { get; set; }
        public decimal? SellingPrice { get; set; }
    }

    private class DocumentRow
    {
        public Guid? Id { get; set; }
        public Guid RequestId { get; set; }
        public Guid? DocumentId { get; set; }
        public string DocumentType { get; set; } = "";
        public string? FileName { get; set; }
        public string? Prefix { get; set; }
        public short? Set { get; set; }
        public string? Notes { get; set; }
        public string? FilePath { get; set; }
        public string? Source { get; set; }
        public bool IsRequired { get; set; }
        public string? UploadedBy { get; set; }
        public string? UploadedByName { get; set; }
        public DateTime? UploadedAt { get; set; }
    }

    private class TitleRow
    {
        public Guid Id { get; set; }
        public Guid RequestId { get; set; }
        public string CollateralType { get; set; } = "";
        public bool CollateralStatus { get; set; }
        // TitleDeedInfo
        public string? TitleNumber { get; set; }
        public string? TitleType { get; set; }
        // LandLocationInfo
        public string? BookNumber { get; set; }
        public string? PageNumber { get; set; }
        public string? LandParcelNumber { get; set; }
        public string? SurveyNumber { get; set; }
        public string? MapSheetNumber { get; set; }
        public string? Rawang { get; set; }
        public string? AerialMapName { get; set; }
        public string? AerialMapNumber { get; set; }
        // LandArea
        public int? AreaRai { get; set; }
        public int? AreaNgan { get; set; }
        public decimal? AreaSquareWa { get; set; }
        // Shared
        public string? OwnerName { get; set; }
        // VehicleInfo
        public string? VehicleType { get; set; }
        public string? VehicleLocation { get; set; }
        public string? VIN { get; set; }
        public string? LicensePlateNumber { get; set; }
        // VesselInfo
        public string? VesselType { get; set; }
        public string? VesselLocation { get; set; }
        public string? HIN { get; set; }
        public string? VesselRegistrationNumber { get; set; }
        // MachineInfo
        public bool RegistrationStatus { get; set; }
        public string? RegistrationNo { get; set; }
        public string? MachineType { get; set; }
        public string? InstallationStatus { get; set; }
        public string? InvoiceNumber { get; set; }
        public int? NumberOfMachine { get; set; }
        // BuildingInfo
        public string? BuildingType { get; set; }
        public decimal? UsableArea { get; set; }
        public int? NumberOfBuilding { get; set; }
        // CondoInfo
        public string? CondoName { get; set; }
        public string? BuildingNumber { get; set; }
        public string? RoomNumber { get; set; }
        public string? FloorNumber { get; set; }
        // TitleAddress (flat)
        public string? TitleHouseNumber { get; set; }
        public string? TitleProjectName { get; set; }
        public string? TitleMoo { get; set; }
        public string? TitleSoi { get; set; }
        public string? TitleRoad { get; set; }
        public string? TitleSubDistrict { get; set; }
        public string? TitleDistrict { get; set; }
        public string? TitleProvince { get; set; }
        public string? TitlePostcode { get; set; }
        // DopaAddress (flat)
        public string? DopaHouseNumber { get; set; }
        public string? DopaProjectName { get; set; }
        public string? DopaMoo { get; set; }
        public string? DopaSoi { get; set; }
        public string? DopaRoad { get; set; }
        public string? DopaSubDistrict { get; set; }
        public string? DopaDistrict { get; set; }
        public string? DopaProvince { get; set; }
        public string? DopaPostcode { get; set; }
        public string? Notes { get; set; }
    }
}
