using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class ContentTypeParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "ContentTypes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentTypes_ParentId",
                table: "ContentTypes",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentTypes_ContentTypes_ParentId",
                table: "ContentTypes",
                column: "ParentId",
                principalTable: "ContentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ContentTypes_ContentTypes_ParentId",
                table: "ContentTypes");

            migrationBuilder.DropIndex(
                name: "IX_ContentTypes_ParentId",
                table: "ContentTypes");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "ContentTypes");
        }
    }
}
