using Parameter.ConstructionWork.Models;
using Parameter.DocumentRequirements.Models;
using Parameter.PricingParameters.Models;
using Parameter.PricingTemplates.Models;

namespace Parameter.Data;

public class ParameterDbContext : DbContext
{
    public ParameterDbContext(DbContextOptions<ParameterDbContext> options) : base(options)
    {
    }

    public DbSet<Parameters.Models.Parameter> Parameters => Set<Parameters.Models.Parameter>();

    public DbSet<TitleProvince> TitleProvinces => Set<TitleProvince>();
    public DbSet<TitleDistrict> TitleDistricts => Set<TitleDistrict>();
    public DbSet<TitleSubDistrict> TitleSubDistricts => Set<TitleSubDistrict>();
    public DbSet<DopaProvince> DopaProvinces => Set<DopaProvince>();
    public DbSet<DopaDistrict> DopaDistricts => Set<DopaDistrict>();
    public DbSet<DopaSubDistrict> DopaSubDistricts => Set<DopaSubDistrict>();

    // Document Requirements
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<DocumentRequirement> DocumentRequirements => Set<DocumentRequirement>();

    // Construction Work Lookups
    public DbSet<ConstructionWorkGroup> ConstructionWorkGroups => Set<ConstructionWorkGroup>();
    public DbSet<ConstructionWorkItem> ConstructionWorkItems => Set<ConstructionWorkItem>();

    // Pricing Templates
    public DbSet<PricingTemplate> PricingTemplates => Set<PricingTemplate>();
    public DbSet<PricingTemplateSection> PricingTemplateSections => Set<PricingTemplateSection>();
    public DbSet<PricingTemplateCategory> PricingTemplateCategories => Set<PricingTemplateCategory>();
    public DbSet<PricingTemplateAssumption> PricingTemplateAssumptions => Set<PricingTemplateAssumption>();

    // Pricing Parameters (reference lookups)
    public DbSet<PricingParameterRoomType> PricingParameterRoomTypes => Set<PricingParameterRoomType>();
    public DbSet<PricingParameterJobPosition> PricingParameterJobPositions => Set<PricingParameterJobPosition>();
    public DbSet<PricingParameterTaxBracket> PricingParameterTaxBrackets => Set<PricingParameterTaxBracket>();
    public DbSet<PricingParameterAssumptionType> PricingParameterAssumptionTypes => Set<PricingParameterAssumptionType>();
    public DbSet<PricingParameterAssumptionMethod> PricingParameterAssumptionMethods => Set<PricingParameterAssumptionMethod>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("parameter");

        modelBuilder.ApplyGlobalConventions();

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);
    }
}