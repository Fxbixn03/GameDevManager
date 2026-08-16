using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class SpawnRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpawnRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetMapId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetMarkerId = table.Column<Guid>(type: "uuid", nullable: true),
                    MinCount = table.Column<int>(type: "integer", nullable: false),
                    MaxCount = table.Column<int>(type: "integer", nullable: false),
                    RespawnSeconds = table.Column<int>(type: "integer", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpawnRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpawnRules_Npcs_NpcId",
                        column: x => x.NpcId,
                        principalTable: "Npcs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpawnRules_NpcId",
                table: "SpawnRules",
                column: "NpcId");

            migrationBuilder.CreateIndex(
                name: "IX_SpawnRules_TargetMapId",
                table: "SpawnRules",
                column: "TargetMapId");

            migrationBuilder.CreateIndex(
                name: "IX_SpawnRules_TargetMarkerId",
                table: "SpawnRules",
                column: "TargetMarkerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpawnRules");
        }
    }
}
