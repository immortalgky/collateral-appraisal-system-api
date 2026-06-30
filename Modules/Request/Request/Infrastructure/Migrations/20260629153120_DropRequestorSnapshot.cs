using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropRequestorSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestorAoCode",
                schema: "request",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "RequestorContactNo",
                schema: "request",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "RequestorCostCenterCode",
                schema: "request",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "RequestorCostCenterDesc",
                schema: "request",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "RequestorDepartment",
                schema: "request",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "RequestorEmail",
                schema: "request",
                table: "Requests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequestorAoCode",
                schema: "request",
                table: "Requests",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestorContactNo",
                schema: "request",
                table: "Requests",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestorCostCenterCode",
                schema: "request",
                table: "Requests",
                type: "nvarchar(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestorCostCenterDesc",
                schema: "request",
                table: "Requests",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestorDepartment",
                schema: "request",
                table: "Requests",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestorEmail",
                schema: "request",
                table: "Requests",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);
        }
    }
}
