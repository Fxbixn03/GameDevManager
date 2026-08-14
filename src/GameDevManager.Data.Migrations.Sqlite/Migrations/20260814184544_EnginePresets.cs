using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class EnginePresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnginePresets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Engine = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    ModuleKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TypeName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnginePresets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnginePresets_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EnginePresets_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EnginePresetMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnginePresetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Target = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ConstantValue = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnginePresetMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnginePresetMappings_EnginePresets_EnginePresetId",
                        column: x => x.EnginePresetId,
                        principalTable: "EnginePresets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnginePresetMappings_EnginePresetId",
                table: "EnginePresetMappings",
                column: "EnginePresetId");

            migrationBuilder.CreateIndex(
                name: "IX_EnginePresets_ContentTypeId",
                table: "EnginePresets",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_EnginePresets_GameProjectId",
                table: "EnginePresets",
                column: "GameProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnginePresetMappings");

            migrationBuilder.DropTable(
                name: "EnginePresets");
        }
    }
}
