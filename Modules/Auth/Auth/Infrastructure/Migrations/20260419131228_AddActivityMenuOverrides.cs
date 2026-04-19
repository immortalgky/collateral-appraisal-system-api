using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityMenuOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityMenuOverrides",
                schema: "auth",
                columns: table => new
                {
                    ActivityMenuOverrideId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActivityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsVisible = table.Column<bool>(type: "bit", nullable: false),
                    CanEdit = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityMenuOverrides", x => x.ActivityMenuOverrideId);
                    table.ForeignKey(
                        name: "FK_ActivityMenuOverrides_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalSchema: "auth",
                        principalTable: "MenuItems",
                        principalColumn: "MenuItemId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityMenuOverrides_ActivityId",
                schema: "auth",
                table: "ActivityMenuOverrides",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityMenuOverrides_ActivityId_MenuItemId",
                schema: "auth",
                table: "ActivityMenuOverrides",
                columns: new[] { "ActivityId", "MenuItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityMenuOverrides_MenuItemId",
                schema: "auth",
                table: "ActivityMenuOverrides",
                column: "MenuItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityMenuOverrides",
                schema: "auth");
        }
    }
}
