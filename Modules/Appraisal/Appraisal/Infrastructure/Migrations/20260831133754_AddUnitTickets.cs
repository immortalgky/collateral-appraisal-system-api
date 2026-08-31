using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitTickets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UnitTickets",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TicketNumber = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitSetKey = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    IssuedTo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitTickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnitTicketUnits",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitTicketId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UnitKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitTicketUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitTicketUnits_UnitTickets_UnitTicketId",
                        column: x => x.UnitTicketId,
                        principalSchema: "appraisal",
                        principalTable: "UnitTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_UnitTickets_Appraisal_UnitSet",
                schema: "appraisal",
                table: "UnitTickets",
                columns: new[] { "AppraisalId", "UnitSetKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_UnitTickets_TicketNumber",
                schema: "appraisal",
                table: "UnitTickets",
                column: "TicketNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitTicketUnits_ProjectUnitId",
                schema: "appraisal",
                table: "UnitTicketUnits",
                column: "ProjectUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_UnitTicketUnits_UnitTicketId",
                schema: "appraisal",
                table: "UnitTicketUnits",
                column: "UnitTicketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnitTicketUnits",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "UnitTickets",
                schema: "appraisal");
        }
    }
}
