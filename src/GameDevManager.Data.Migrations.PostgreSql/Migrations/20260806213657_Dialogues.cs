using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class Dialogues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dialogues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    IncludesPlayer = table.Column<bool>(type: "boolean", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dialogues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dialogues_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dialogues_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DialogueLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DialogueId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpeakerNpcId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogueLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DialogueLines_Dialogues_DialogueId",
                        column: x => x.DialogueId,
                        principalTable: "Dialogues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DialogueParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DialogueId = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogueParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DialogueParticipants_Dialogues_DialogueId",
                        column: x => x.DialogueId,
                        principalTable: "Dialogues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DialogueChoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DialogueLineId = table.Column<Guid>(type: "uuid", nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    NextLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialogueChoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DialogueChoices_DialogueLines_DialogueLineId",
                        column: x => x.DialogueLineId,
                        principalTable: "DialogueLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DialogueChoices_DialogueLineId",
                table: "DialogueChoices",
                column: "DialogueLineId");

            migrationBuilder.CreateIndex(
                name: "IX_DialogueChoices_NextLineId",
                table: "DialogueChoices",
                column: "NextLineId");

            migrationBuilder.CreateIndex(
                name: "IX_DialogueLines_DialogueId",
                table: "DialogueLines",
                column: "DialogueId");

            migrationBuilder.CreateIndex(
                name: "IX_DialogueLines_SpeakerNpcId",
                table: "DialogueLines",
                column: "SpeakerNpcId");

            migrationBuilder.CreateIndex(
                name: "IX_DialogueParticipants_DialogueId",
                table: "DialogueParticipants",
                column: "DialogueId");

            migrationBuilder.CreateIndex(
                name: "IX_DialogueParticipants_NpcId",
                table: "DialogueParticipants",
                column: "NpcId");

            migrationBuilder.CreateIndex(
                name: "IX_Dialogues_ContentTypeId",
                table: "Dialogues",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Dialogues_GameProjectId",
                table: "Dialogues",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Dialogues_Name",
                table: "Dialogues",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DialogueChoices");

            migrationBuilder.DropTable(
                name: "DialogueParticipants");

            migrationBuilder.DropTable(
                name: "DialogueLines");

            migrationBuilder.DropTable(
                name: "Dialogues");
        }
    }
}
