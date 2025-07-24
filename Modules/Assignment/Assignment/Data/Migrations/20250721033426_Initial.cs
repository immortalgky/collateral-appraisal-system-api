using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Assignment.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "assignment");

            migrationBuilder.CreateTable(
                name: "Assignments",
                schema: "assignment",
                columns: table => new
                {
                    AssignmentId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RequestId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssignmentMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ExternalCompanyID = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ExternalCompanyAssignType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExtApprStaff = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ExtApprStaffAssignmentType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IntApprStaff = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    IntApprStaffAssignmentType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Remark = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assignments", x => x.AssignmentId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Assignments",
                schema: "assignment");
        }
    }
}
