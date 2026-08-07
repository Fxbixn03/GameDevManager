using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class Cutscenes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cutscenes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StoryEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    DialogueId = table.Column<Guid>(type: "uuid", nullable: true),
                    GameProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cutscenes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cutscenes_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cutscenes_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CutsceneShots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CutsceneId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CutsceneShots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CutsceneShots_Cutscenes_CutsceneId",
                        column: x => x.CutsceneId,
                        principalTable: "Cutscenes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cutscenes_ContentTypeId",
                table: "Cutscenes",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Cutscenes_DialogueId",
                table: "Cutscenes",
                column: "DialogueId");

            migrationBuilder.CreateIndex(
                name: "IX_Cutscenes_GameProjectId",
                table: "Cutscenes",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Cutscenes_Name",
                table: "Cutscenes",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Cutscenes_StoryEntryId",
                table: "Cutscenes",
                column: "StoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_CutsceneShots_CutsceneId",
                table: "CutsceneShots",
                column: "CutsceneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CutsceneShots");

            migrationBuilder.DropTable(
                name: "Cutscenes");
        }
    }
}
