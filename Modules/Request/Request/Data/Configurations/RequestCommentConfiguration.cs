using Request.RequestComments.Models;

namespace Request.Data.Configurations;

public class RequestCommentConfiguration : IEntityTypeConfiguration<RequestComment>
{
    public void Configure(EntityTypeBuilder<RequestComment> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .UseIdentityColumn();

        builder.Property(p => p.RequestId)
            .IsRequired();

        builder.Property(p => p.Comment)
            .IsRequired()
            .HasMaxLength(250);

        builder.HasIndex(p => p.RequestId);
    }
}