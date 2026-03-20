using Parameter.DocumentRequirements.Models;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("parameter");

        modelBuilder.ApplyGlobalConventions();

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);
    }
}