using Common.Domain.Logs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Common.Infrastructure.Configurations;

public class LogConfiguration : IEntityTypeConfiguration<Log>
{
    public void Configure(EntityTypeBuilder<Log> builder)
    {
        builder.ToTable("Logs", "dbo");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.TimeStamp)
            .HasColumnType("datetime2(3)");

        builder.Property(x => x.Level)
            .HasColumnType("nvarchar(16)");

        builder.Property(x => x.Message)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Exception)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.Properties)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.CorrelationId)
            .HasColumnType("nvarchar(64)");

        builder.Property(x => x.EntityId)
            .HasColumnType("nvarchar(64)");

        builder.Property(x => x.AppraisalId)
            .HasColumnType("nvarchar(64)");

        builder.Property(x => x.RequestId)
            .HasColumnType("nvarchar(64)");

        builder.Property(x => x.WorkflowInstanceId)
            .HasColumnType("nvarchar(64)");

        builder.Property(x => x.CollateralId)
            .HasColumnType("nvarchar(64)");

        builder.Property(x => x.DocumentId)
            .HasColumnType("nvarchar(64)");

        builder.Property(x => x.MachineName)
            .HasColumnType("nvarchar(128)");

        builder.HasIndex(x => x.TimeStamp)
            .HasDatabaseName("IX_Logs_TimeStamp");

        builder.HasIndex(x => x.AppraisalId)
            .HasFilter("[AppraisalId] IS NOT NULL")
            .HasDatabaseName("IX_Logs_AppraisalId");

        builder.HasIndex(x => x.RequestId)
            .HasFilter("[RequestId] IS NOT NULL")
            .HasDatabaseName("IX_Logs_RequestId");

        builder.HasIndex(x => x.EntityId)
            .HasFilter("[EntityId] IS NOT NULL")
            .HasDatabaseName("IX_Logs_EntityId");

        builder.HasIndex(x => x.CorrelationId)
            .HasFilter("[CorrelationId] IS NOT NULL")
            .HasDatabaseName("IX_Logs_CorrelationId");

        builder.HasIndex(x => x.WorkflowInstanceId)
            .HasFilter("[WorkflowInstanceId] IS NOT NULL")
            .HasDatabaseName("IX_Logs_WorkflowInstanceId");

        builder.HasIndex(x => x.CollateralId)
            .HasFilter("[CollateralId] IS NOT NULL")
            .HasDatabaseName("IX_Logs_CollateralId");

        builder.HasIndex(x => x.DocumentId)
            .HasFilter("[DocumentId] IS NOT NULL")
            .HasDatabaseName("IX_Logs_DocumentId");
    }
}
