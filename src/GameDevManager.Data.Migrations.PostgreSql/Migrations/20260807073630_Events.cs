using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class Events : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Chance = table.Column<double>(type: "double precision", nullable: false),
                    RewardLootTableId = table.Column<Guid>(type: "uuid", nullable: true),
                    GameProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameEvents_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameEvents_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventSpawns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcId = table.Column<Guid>(type: "uuid", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSpawns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventSpawns_GameEvents_GameEventId",
                        column: x => x.GameEventId,
                        principalTable: "GameEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EventSpawns_GameEventId",
                table: "EventSpawns",
                column: "GameEventId");

            migrationBuilder.CreateIndex(
                name: "IX_EventSpawns_NpcId",
                table: "EventSpawns",
                column: "NpcId");

            migrationBuilder.CreateIndex(
                name: "IX_GameEvents_ContentTypeId",
                table: "GameEvents",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_GameEvents_GameProjectId",
                table: "GameEvents",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_GameEvents_Name",
                table: "GameEvents",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_GameEvents_RewardLootTableId",
                table: "GameEvents",
                column: "RewardLootTableId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EventSpawns");

            migrationBuilder.DropTable(
                name: "GameEvents");
        }
    }
}
