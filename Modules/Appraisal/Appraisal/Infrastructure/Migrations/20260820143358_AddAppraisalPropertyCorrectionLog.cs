using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppraisalPropertyCorrectionLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppraisalPropertyCorrectionLogs",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalPropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropertyType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ChangedFields = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppraisalPropertyCorrectionLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalPropertyCorrectionLogs_Appraisal_ChangedAt",
                schema: "appraisal",
                table: "AppraisalPropertyCorrectionLogs",
                columns: new[] { "AppraisalId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalPropertyCorrectionLogs_Property",
                schema: "appraisal",
                table: "AppraisalPropertyCorrectionLogs",
                column: "AppraisalPropertyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppraisalPropertyCorrectionLogs",
                schema: "appraisal");
        }
    }
}
