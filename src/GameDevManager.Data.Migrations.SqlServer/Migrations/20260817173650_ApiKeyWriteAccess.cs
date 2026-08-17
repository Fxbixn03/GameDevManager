using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class ApiKeyWriteAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AppUserId",
                table: "ApiKeys",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanWrite",
                table: "ApiKeys",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_AppUserId",
                table: "ApiKeys",
                column: "AppUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiKeys_AppUsers_AppUserId",
                table: "ApiKeys",
                column: "AppUserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiKeys_AppUsers_AppUserId",
                table: "ApiKeys");

            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_AppUserId",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "AppUserId",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "CanWrite",
                table: "ApiKeys");
        }
    }
}
