namespace Appraisal.Infrastructure.Configurations;

public class PhotoTopicConfiguration : IEntityTypeConfiguration<PhotoTopic>
{
    public void Configure(EntityTypeBuilder<PhotoTopic> builder)
    {
        builder.ToTable("PhotoTopics");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(t => t.AppraisalId).IsRequired();
        builder.Property(t => t.TopicName).IsRequired().HasMaxLength(200);
        builder.Property(t => t.SortOrder).IsRequired();
        builder.Property(t => t.DisplayColumns).IsRequired().HasDefaultValue(1);

        builder.HasIndex(t => t.AppraisalId);
    }
}
