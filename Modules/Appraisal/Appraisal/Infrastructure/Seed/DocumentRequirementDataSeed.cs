using Shared.Data.Seed;

namespace Appraisal.Infrastructure.Seed;

/// <summary>
/// Seeds common document types and requirements for the appraisal module
/// </summary>
public class DocumentRequirementDataSeed : IDataSeeder<AppraisalDbContext>
{
    private readonly AppraisalDbContext _context;
    private readonly ILogger<DocumentRequirementDataSeed> _logger;

    public DocumentRequirementDataSeed(
        AppraisalDbContext context,
        ILogger<DocumentRequirementDataSeed> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAllAsync()
    {
        if (await _context.DocumentTypes.AnyAsync())
        {
            _logger.LogInformation("Document types already seeded, skipping...");
            return;
        }

        _logger.LogInformation("Seeding document types and requirements...");

        // Create document types
        var documentTypes = CreateDocumentTypes();
        _context.DocumentTypes.AddRange(documentTypes);
        await _context.SaveChangesAsync();

        // Create requirements
        var requirements = CreateRequirements(documentTypes);
        _context.DocumentRequirements.AddRange(requirements);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Seeded {TypeCount} document types and {ReqCount} requirements",
            documentTypes.Count, requirements.Count);
    }

    private static List<DocumentType> CreateDocumentTypes()
    {
        var types = new List<DocumentType>();
        var sortOrder = 0;

        // ========================================
        // Application-Level Documents (General)
        // ========================================
        types.Add(DocumentType.Create("APP_FORM", "Application Form", "Loan/appraisal application form", "General", sortOrder++));
        types.Add(DocumentType.Create("ID_COPY", "ID Card Copy", "Copy of applicant's ID card", "General", sortOrder++));
        types.Add(DocumentType.Create("HOUSE_REG", "House Registration", "Copy of house registration document", "General", sortOrder++));
        types.Add(DocumentType.Create("POA", "Power of Attorney", "Power of attorney document if applicable", "Legal", sortOrder++));
        types.Add(DocumentType.Create("COMPANY_REG", "Company Registration", "Company registration certificate for juristic persons", "Legal", sortOrder++));
        types.Add(DocumentType.Create("COMPANY_AFF", "Company Affidavit", "Company affidavit and authorized signatories", "Legal", sortOrder++));

        // ========================================
        // Land Documents
        // ========================================
        types.Add(DocumentType.Create("TITLE_DEED", "Title Deed", "Land title deed (Chanote, Nor Sor 3, etc.)", "Legal", sortOrder++));
        types.Add(DocumentType.Create("SURVEY_MAP", "Survey Map", "Land survey map from Land Department", "Technical", sortOrder++));
        types.Add(DocumentType.Create("LAND_USE", "Land Use Certificate", "Land use certificate or zoning document", "Technical", sortOrder++));
        types.Add(DocumentType.Create("GPS_COORD", "GPS Coordinates", "GPS coordinates documentation", "Technical", sortOrder++));
        types.Add(DocumentType.Create("AERIAL_PHOTO", "Aerial Photo", "Aerial or satellite photo of the land", "Technical", sortOrder++));

        // ========================================
        // Building Documents
        // ========================================
        types.Add(DocumentType.Create("BLDG_PERMIT", "Building Permit", "Construction permit from local authority", "Legal", sortOrder++));
        types.Add(DocumentType.Create("FLOOR_PLAN", "Floor Plan", "Architectural floor plan drawings", "Technical", sortOrder++));
        types.Add(DocumentType.Create("BLDG_CERT", "Building Certificate", "Building completion certificate (Ror 4)", "Legal", sortOrder++));
        types.Add(DocumentType.Create("STRUCT_CALC", "Structural Calculation", "Structural engineering calculations", "Technical", sortOrder++));
        types.Add(DocumentType.Create("MEP_PLAN", "MEP Plan", "Mechanical, electrical, plumbing plans", "Technical", sortOrder++));

        // ========================================
        // Condominium Documents
        // ========================================
        types.Add(DocumentType.Create("CONDO_TITLE", "Condo Title", "Condominium unit ownership certificate", "Legal", sortOrder++));
        types.Add(DocumentType.Create("CONDO_REG", "Condo Registration", "Condominium registration document", "Legal", sortOrder++));
        types.Add(DocumentType.Create("JURISTIC_DOC", "Juristic Person Doc", "Condominium juristic person registration", "Legal", sortOrder++));
        types.Add(DocumentType.Create("COMMON_FEE", "Common Fee Statement", "Common area fee payment statement", "Financial", sortOrder++));
        types.Add(DocumentType.Create("CONDO_RULES", "Condo Rules", "Condominium rules and regulations", "Legal", sortOrder++));

        // ========================================
        // Vehicle Documents
        // ========================================
        types.Add(DocumentType.Create("VEH_REG", "Vehicle Registration", "Vehicle registration certificate", "Legal", sortOrder++));
        types.Add(DocumentType.Create("VEH_TAX", "Vehicle Tax Receipt", "Annual vehicle tax payment receipt", "Financial", sortOrder++));
        types.Add(DocumentType.Create("VEH_INSPECT", "Vehicle Inspection", "Vehicle inspection certificate", "Technical", sortOrder++));
        types.Add(DocumentType.Create("VEH_INSURANCE", "Vehicle Insurance", "Compulsory and voluntary insurance", "Financial", sortOrder++));

        // ========================================
        // Vessel Documents
        // ========================================
        types.Add(DocumentType.Create("SHIP_CERT", "Ship Certificate", "Ship registration certificate", "Legal", sortOrder++));
        types.Add(DocumentType.Create("SHIP_CLASS", "Ship Classification", "Ship classification certificate", "Technical", sortOrder++));
        types.Add(DocumentType.Create("SHIP_SURVEY", "Ship Survey Report", "Ship survey and inspection report", "Technical", sortOrder++));
        types.Add(DocumentType.Create("SHIP_INSURANCE", "Ship Insurance", "Marine insurance policy", "Financial", sortOrder++));

        // ========================================
        // Machinery Documents
        // ========================================
        types.Add(DocumentType.Create("MAC_INVOICE", "Purchase Invoice", "Original purchase invoice for machinery", "Financial", sortOrder++));
        types.Add(DocumentType.Create("MAC_SPEC", "Technical Specification", "Machinery technical specifications", "Technical", sortOrder++));
        types.Add(DocumentType.Create("MAC_MAINT", "Maintenance Record", "Maintenance and service records", "Technical", sortOrder++));
        types.Add(DocumentType.Create("MAC_CERT", "Certification", "Safety or quality certification", "Technical", sortOrder++));
        types.Add(DocumentType.Create("MAC_WARRANTY", "Warranty Document", "Warranty documentation", "Legal", sortOrder++));

        // ========================================
        // Lease Agreement Documents
        // ========================================
        types.Add(DocumentType.Create("LEASE_AGR", "Lease Agreement", "Lease agreement contract", "Legal", sortOrder++));
        types.Add(DocumentType.Create("LEASE_REG", "Lease Registration", "Registered lease at Land Department", "Legal", sortOrder++));
        types.Add(DocumentType.Create("LESSOR_ID", "Lessor ID", "Lessor identification documents", "Legal", sortOrder++));

        return types;
    }

    private static List<DocumentRequirement> CreateRequirements(List<DocumentType> types)
    {
        var requirements = new List<DocumentRequirement>();
        var typeDict = types.ToDictionary(t => t.Code, t => t.Id);

        // Helper function to add requirement
        void AddReq(string typeCode, string? collateralType, bool isRequired, string? notes = null)
        {
            if (!typeDict.TryGetValue(typeCode, out var typeId)) return;

            var req = collateralType is null
                ? DocumentRequirement.CreateApplicationLevel(typeId, isRequired, notes)
                : DocumentRequirement.CreateForCollateral(typeId, collateralType, isRequired, notes);

            requirements.Add(req);
        }

        // ========================================
        // Application-Level Requirements (for ALL appraisals)
        // ========================================
        AddReq("APP_FORM", null, true, "Required for all appraisal requests");
        AddReq("ID_COPY", null, true, "Applicant and guarantor ID cards");
        AddReq("HOUSE_REG", null, false, "If applicable");
        AddReq("POA", null, false, "Required if representative is not the owner");
        AddReq("COMPANY_REG", null, false, "Required for juristic person applicants");
        AddReq("COMPANY_AFF", null, false, "Required for juristic person applicants");

        // ========================================
        // Land (L) Requirements
        // ========================================
        AddReq("TITLE_DEED", "L", true, "Must be certified copy from Land Department");
        AddReq("SURVEY_MAP", "L", true, "Official survey map");
        AddReq("LAND_USE", "L", false, "Land use zoning information");
        AddReq("GPS_COORD", "L", false, "GPS coordinates of property boundaries");
        AddReq("AERIAL_PHOTO", "L", false, "Recent aerial or satellite imagery");

        // ========================================
        // Building (B) Requirements
        // ========================================
        AddReq("TITLE_DEED", "B", true, "Land title deed for building location");
        AddReq("BLDG_PERMIT", "B", true, "Valid building construction permit");
        AddReq("FLOOR_PLAN", "B", true, "As-built floor plans");
        AddReq("BLDG_CERT", "B", false, "Building completion certificate (Ror 4)");
        AddReq("STRUCT_CALC", "B", false, "For buildings over 3 stories");

        // ========================================
        // Land and Building (LB) Requirements
        // ========================================
        AddReq("TITLE_DEED", "LB", true, "Certified copy from Land Department");
        AddReq("SURVEY_MAP", "LB", true, "Official land survey map");
        AddReq("BLDG_PERMIT", "LB", true, "Building construction permit");
        AddReq("FLOOR_PLAN", "LB", true, "As-built floor plans");
        AddReq("BLDG_CERT", "LB", false, "Building completion certificate");
        AddReq("GPS_COORD", "LB", false, "GPS coordinates");

        // ========================================
        // Condominium (U) Requirements
        // ========================================
        AddReq("CONDO_TITLE", "U", true, "Condominium unit ownership certificate");
        AddReq("CONDO_REG", "U", true, "Condominium registration");
        AddReq("JURISTIC_DOC", "U", true, "Juristic person registration");
        AddReq("FLOOR_PLAN", "U", false, "Unit floor plan");
        AddReq("COMMON_FEE", "U", false, "Common fee payment history");
        AddReq("CONDO_RULES", "U", false, "Condominium regulations");

        // ========================================
        // Vehicle (VEH) Requirements
        // ========================================
        AddReq("VEH_REG", "VEH", true, "Original vehicle registration book");
        AddReq("VEH_TAX", "VEH", true, "Current year tax receipt");
        AddReq("VEH_INSPECT", "VEH", false, "Recent inspection certificate");
        AddReq("VEH_INSURANCE", "VEH", false, "Insurance policy documents");

        // ========================================
        // Vessel (VES) Requirements
        // ========================================
        AddReq("SHIP_CERT", "VES", true, "Ship registration certificate");
        AddReq("SHIP_CLASS", "VES", true, "Classification society certificate");
        AddReq("SHIP_SURVEY", "VES", true, "Recent survey report");
        AddReq("SHIP_INSURANCE", "VES", false, "Marine insurance policy");

        // ========================================
        // Machinery (MAC) Requirements
        // ========================================
        AddReq("MAC_INVOICE", "MAC", true, "Original purchase invoice");
        AddReq("MAC_SPEC", "MAC", true, "Technical specifications");
        AddReq("MAC_MAINT", "MAC", false, "Maintenance records");
        AddReq("MAC_CERT", "MAC", false, "Safety/quality certifications");
        AddReq("MAC_WARRANTY", "MAC", false, "Warranty documents if available");

        // ========================================
        // Lease Agreement Land (LSL) Requirements
        // ========================================
        AddReq("TITLE_DEED", "LSL", true, "Land title deed");
        AddReq("LEASE_AGR", "LSL", true, "Executed lease agreement");
        AddReq("LEASE_REG", "LSL", true, "Registered lease at Land Department");
        AddReq("LESSOR_ID", "LSL", true, "Lessor identification");
        AddReq("SURVEY_MAP", "LSL", false, "Land survey map");

        // ========================================
        // Lease Agreement Building (LSB) Requirements
        // ========================================
        AddReq("TITLE_DEED", "LSB", true, "Land title deed");
        AddReq("LEASE_AGR", "LSB", true, "Executed lease agreement");
        AddReq("LEASE_REG", "LSB", true, "Registered lease");
        AddReq("LESSOR_ID", "LSB", true, "Lessor identification");
        AddReq("BLDG_PERMIT", "LSB", true, "Building permit");
        AddReq("FLOOR_PLAN", "LSB", false, "Floor plans");

        // ========================================
        // Lease Agreement Land and Building (LS) Requirements
        // ========================================
        AddReq("TITLE_DEED", "LS", true, "Land title deed");
        AddReq("LEASE_AGR", "LS", true, "Executed lease agreement");
        AddReq("LEASE_REG", "LS", true, "Registered lease");
        AddReq("LESSOR_ID", "LS", true, "Lessor identification");
        AddReq("BLDG_PERMIT", "LS", true, "Building permit");
        AddReq("FLOOR_PLAN", "LS", false, "Floor plans");
        AddReq("SURVEY_MAP", "LS", false, "Land survey map");

        // ========================================
        // Lease Agreement Condo (LSU) Requirements
        // ========================================
        AddReq("CONDO_TITLE", "LSU", true, "Condo ownership certificate");
        AddReq("LEASE_AGR", "LSU", true, "Executed lease agreement");
        AddReq("LESSOR_ID", "LSU", true, "Lessor identification");
        AddReq("JURISTIC_DOC", "LSU", true, "Juristic person registration");
        AddReq("FLOOR_PLAN", "LSU", false, "Unit floor plan");

        return requirements;
    }
}
