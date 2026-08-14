using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class UserPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedModuleKeys",
                table: "AppUsers",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanExport",
                table: "AppUsers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanImport",
                table: "AppUsers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanWrite",
                table: "AppUsers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedModuleKeys",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "CanExport",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "CanImport",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "CanWrite",
                table: "AppUsers");
        }
    }
}
