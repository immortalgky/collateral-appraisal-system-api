using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTemporaryAccessWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxAccessWindowHours",
                schema: "auth",
                table: "PasswordPolicy",
                type: "int",
                nullable: false,
                defaultValue: 8);

            migrationBuilder.AddColumn<DateTime>(
                name: "AccessExpiresAt",
                schema: "auth",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsTemporaryAccess",
                schema: "auth",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxAccessWindowHours",
                schema: "auth",
                table: "PasswordPolicy");

            migrationBuilder.DropColumn(
                name: "AccessExpiresAt",
                schema: "auth",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsTemporaryAccess",
                schema: "auth",
                table: "AspNetUsers");
        }
    }
}
