using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Quests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Quests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    GiverNpcId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StoryEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DialogueId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GameProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quests_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Quests_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Quests_ContentTypeId",
                table: "Quests",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Quests_DialogueId",
                table: "Quests",
                column: "DialogueId");

            migrationBuilder.CreateIndex(
                name: "IX_Quests_GameProjectId",
                table: "Quests",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Quests_GameProjectId_Kind",
                table: "Quests",
                columns: new[] { "GameProjectId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_Quests_GiverNpcId",
                table: "Quests",
                column: "GiverNpcId");

            migrationBuilder.CreateIndex(
                name: "IX_Quests_Name",
                table: "Quests",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Quests_StoryEntryId",
                table: "Quests",
                column: "StoryEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Quests");
        }
    }
}
