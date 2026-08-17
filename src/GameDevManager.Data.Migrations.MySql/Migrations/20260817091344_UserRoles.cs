using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class UserRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "OverridesRole",
                table: "AppUsers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "RoleId",
                table: "AppUsers",
                type: "char(36)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    CanWrite = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    CanExport = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    CanImport = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    AllowedModuleKeys = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_RoleId",
                table: "AppUsers",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_Name",
                table: "UserRoles",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AppUsers_UserRoles_RoleId",
                table: "AppUsers",
                column: "RoleId",
                principalTable: "UserRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppUsers_UserRoles_RoleId",
                table: "AppUsers");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_AppUsers_RoleId",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "OverridesRole",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "AppUsers");
        }
    }
}
