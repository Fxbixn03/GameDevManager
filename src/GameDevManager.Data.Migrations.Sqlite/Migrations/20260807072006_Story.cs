using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Story : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: true),
                    GameProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryEntries_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoryEntries_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StoryParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    StoryEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetModuleKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TargetEntityId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryParticipants_StoryEntries_StoryEntryId",
                        column: x => x.StoryEntryId,
                        principalTable: "StoryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoryEntries_ContentTypeId",
                table: "StoryEntries",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryEntries_GameProjectId",
                table: "StoryEntries",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryEntries_GameProjectId_SortOrder",
                table: "StoryEntries",
                columns: new[] { "GameProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryEntries_Name",
                table: "StoryEntries",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_StoryParticipants_StoryEntryId",
                table: "StoryParticipants",
                column: "StoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryParticipants_TargetEntityId",
                table: "StoryParticipants",
                column: "TargetEntityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoryParticipants");

            migrationBuilder.DropTable(
                name: "StoryEntries");
        }
    }
}
