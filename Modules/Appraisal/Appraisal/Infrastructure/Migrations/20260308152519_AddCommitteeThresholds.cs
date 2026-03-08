using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommitteeThresholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommitteeThresholds",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CommitteeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MinValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommitteeThresholds", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommitteeThresholds_CommitteeId",
                schema: "appraisal",
                table: "CommitteeThresholds",
                column: "CommitteeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommitteeThresholds",
                schema: "appraisal");
        }
    }
}
