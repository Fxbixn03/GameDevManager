using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class KanbanCardDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedUserId",
                table: "KanbanCards",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "KanbanCards",
                type: "nvarchar(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "KanbanCards",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "KanbanCards",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetEntityId",
                table: "KanbanCards",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetModuleKey",
                table: "KanbanCards",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_KanbanCards_AssignedUserId",
                table: "KanbanCards",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_KanbanCards_TargetEntityId",
                table: "KanbanCards",
                column: "TargetEntityId");

            migrationBuilder.AddForeignKey(
                name: "FK_KanbanCards_AppUsers_AssignedUserId",
                table: "KanbanCards",
                column: "AssignedUserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KanbanCards_AppUsers_AssignedUserId",
                table: "KanbanCards");

            migrationBuilder.DropIndex(
                name: "IX_KanbanCards_AssignedUserId",
                table: "KanbanCards");

            migrationBuilder.DropIndex(
                name: "IX_KanbanCards_TargetEntityId",
                table: "KanbanCards");

            migrationBuilder.DropColumn(
                name: "AssignedUserId",
                table: "KanbanCards");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "KanbanCards");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "KanbanCards");

            migrationBuilder.DropColumn(
                name: "Label",
                table: "KanbanCards");

            migrationBuilder.DropColumn(
                name: "TargetEntityId",
                table: "KanbanCards");

            migrationBuilder.DropColumn(
                name: "TargetModuleKey",
                table: "KanbanCards");
        }
    }
}
