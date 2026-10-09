using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLandAreaDeductions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DeductedAreaInSqWa",
                schema: "appraisal",
                table: "LandAppraisalDetails",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LandAreaDeductions",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    LandAppraisalDetailId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReasonOther = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    AreaInSqWa = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LandAreaDeductions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LandAreaDeductions_LandAppraisalDetails_LandAppraisalDetailId",
                        column: x => x.LandAppraisalDetailId,
                        principalSchema: "appraisal",
                        principalTable: "LandAppraisalDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LandAreaDeductions_LandAppraisalDetailId",
                schema: "appraisal",
                table: "LandAreaDeductions",
                column: "LandAppraisalDetailId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LandAreaDeductions",
                schema: "appraisal");

            migrationBuilder.DropColumn(
                name: "DeductedAreaInSqWa",
                schema: "appraisal",
                table: "LandAppraisalDetails");
        }
    }
}
