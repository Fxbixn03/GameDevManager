using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ExportLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Layout",
                table: "ExportProfiles",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Layout",
                table: "ExportProfiles");
        }
    }
}
