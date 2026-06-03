using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovalMarkersToAppointmentAndFeeItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovalSubmittedAt",
                schema: "appraisal",
                table: "AppraisalFeeItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovalSubmittedAt",
                schema: "appraisal",
                table: "Appointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresApproval",
                schema: "appraisal",
                table: "Appointments",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovalSubmittedAt",
                schema: "appraisal",
                table: "AppraisalFeeItems");

            migrationBuilder.DropColumn(
                name: "ApprovalSubmittedAt",
                schema: "appraisal",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "RequiresApproval",
                schema: "appraisal",
                table: "Appointments");
        }
    }
}
