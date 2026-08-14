using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.SqlServer.Migrations
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
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "MinValue",
                table: "FieldDefinitions",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pattern",
                table: "FieldDefinitions",
                type: "nvarchar(400)",
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
