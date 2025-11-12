using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Data.Migrations
{
    /// <inheritdoc />
    public partial class Update_RequestComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "request",
                table: "RequestComments",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldDefaultValueSql: "NEWSEQUENTIALID()");

            migrationBuilder.AddForeignKey(
                name: "FK_RequestComment_Request",
                schema: "request",
                table: "RequestComments",
                column: "RequestId",
                principalSchema: "request",
                principalTable: "Requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RequestComment_Request",
                schema: "request",
                table: "RequestComments");

            migrationBuilder.AlterColumn<Guid>(
                name: "Id",
                schema: "request",
                table: "RequestComments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWSEQUENTIALID()",
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");
        }
    }
}
