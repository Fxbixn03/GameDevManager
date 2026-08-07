using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Effects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameEffects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameEffects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameEffects_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GameEffects_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EffectAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameEffectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EffectAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EffectAssignments_GameEffects_GameEffectId",
                        column: x => x.GameEffectId,
                        principalTable: "GameEffects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EffectAssignments_GameEffectId",
                table: "EffectAssignments",
                column: "GameEffectId");

            migrationBuilder.CreateIndex(
                name: "IX_EffectAssignments_ItemId",
                table: "EffectAssignments",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_GameEffects_ContentTypeId",
                table: "GameEffects",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_GameEffects_GameProjectId",
                table: "GameEffects",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_GameEffects_Name",
                table: "GameEffects",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EffectAssignments");

            migrationBuilder.DropTable(
                name: "GameEffects");
        }
    }
}
