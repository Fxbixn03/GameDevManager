using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.MySql.Migrations
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
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    Chance = table.Column<double>(type: "double", nullable: false),
                    RewardLootTableId = table.Column<Guid>(type: "char(36)", nullable: true),
                    GameProjectId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EventSpawns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    GameEventId = table.Column<Guid>(type: "char(36)", nullable: false),
                    NpcId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
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
                })
                .Annotation("MySQL:Charset", "utf8mb4");

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
