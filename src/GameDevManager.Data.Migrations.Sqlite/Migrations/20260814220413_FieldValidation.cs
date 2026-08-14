using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class FieldValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "MaxValue",
                table: "FieldDefinitions",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MinValue",
                table: "FieldDefinitions",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pattern",
                table: "FieldDefinitions",
                type: "TEXT",
                maxLength: 400,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxValue",
                table: "FieldDefinitions");

            migrationBuilder.DropColumn(
                name: "MinValue",
                table: "FieldDefinitions");

            migrationBuilder.DropColumn(
                name: "Pattern",
                table: "FieldDefinitions");
        }
    }
}
