using Common.Domain.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Infrastructure.Configurations;

public class SystemMetricSampleConfiguration : IEntityTypeConfiguration<SystemMetricSample>
{
    public void Configure(EntityTypeBuilder<SystemMetricSample> builder)
    {
        builder.ToTable("SystemMetricSamples");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.TimeStamp).HasColumnType("datetime2(3)");
        builder.Property(x => x.MachineName).HasColumnType("nvarchar(128)").IsRequired();
        builder.Property(x => x.ProcessStartedAt).HasColumnType("datetime2(3)");
        builder.Property(x => x.CpuPercent).HasColumnType("decimal(5,1)");
        builder.Property(x => x.MachineMemoryPercent).HasColumnType("decimal(5,1)");

        builder.HasIndex(x => x.TimeStamp).HasDatabaseName("IX_SystemMetricSamples_TimeStamp");
    }
}
