using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConstructionInspection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConstructionInspections",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalPropertyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsFullDetail = table.Column<bool>(type: "bit", nullable: false),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SummaryDetail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SummaryPreviousProgressPct = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    SummaryPreviousValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SummaryCurrentProgressPct = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: true),
                    SummaryCurrentValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Remark = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentFileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DocumentFilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionInspections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConstructionInspections_AppraisalProperties_AppraisalPropertyId",
                        column: x => x.AppraisalPropertyId,
                        principalSchema: "appraisal",
                        principalTable: "AppraisalProperties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConstructionWorkGroups",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameTh = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionWorkGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConstructionWorkDetails",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ConstructionInspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConstructionWorkGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConstructionWorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkItemName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    ConstructionValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PreviousProgressPct = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    CurrentProgressPct = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    ProportionPct = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    CurrentProportionPct = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    PreviousPropertyValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentPropertyValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionWorkDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConstructionWorkDetails_ConstructionInspections_ConstructionInspectionId",
                        column: x => x.ConstructionInspectionId,
                        principalSchema: "appraisal",
                        principalTable: "ConstructionInspections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConstructionWorkItems",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ConstructionWorkGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameTh = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionWorkItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConstructionWorkItems_ConstructionWorkGroups_ConstructionWorkGroupId",
                        column: x => x.ConstructionWorkGroupId,
                        principalSchema: "appraisal",
                        principalTable: "ConstructionWorkGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionInspections_AppraisalPropertyId",
                schema: "appraisal",
                table: "ConstructionInspections",
                column: "AppraisalPropertyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionWorkDetails_ConstructionInspectionId",
                schema: "appraisal",
                table: "ConstructionWorkDetails",
                column: "ConstructionInspectionId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionWorkDetails_ConstructionWorkGroupId",
                schema: "appraisal",
                table: "ConstructionWorkDetails",
                column: "ConstructionWorkGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionWorkGroups_Code",
                schema: "appraisal",
                table: "ConstructionWorkGroups",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionWorkItems_ConstructionWorkGroupId_Code",
                schema: "appraisal",
                table: "ConstructionWorkItems",
                columns: new[] { "ConstructionWorkGroupId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConstructionWorkDetails",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ConstructionWorkItems",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ConstructionInspections",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ConstructionWorkGroups",
                schema: "appraisal");
        }
    }
}
