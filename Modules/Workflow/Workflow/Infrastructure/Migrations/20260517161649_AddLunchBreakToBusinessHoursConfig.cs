using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLunchBreakToBusinessHoursConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "LunchEndTime",
                schema: "workflow",
                table: "BusinessHoursConfigs",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "LunchStartTime",
                schema: "workflow",
                table: "BusinessHoursConfigs",
                type: "time",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LunchEndTime",
                schema: "workflow",
                table: "BusinessHoursConfigs");

            migrationBuilder.DropColumn(
                name: "LunchStartTime",
                schema: "workflow",
                table: "BusinessHoursConfigs");
        }
    }
}
