using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class LootTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LootTableId",
                table: "Npcs",
                type: "char(36)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LootTables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    RollMode = table.Column<int>(type: "int", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LootTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LootTables_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LootTables_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "LootEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    LootTableId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ItemId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Chance = table.Column<double>(type: "double", nullable: false),
                    MinQuantity = table.Column<int>(type: "int", nullable: false),
                    MaxQuantity = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LootEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LootEntries_LootTables_LootTableId",
                        column: x => x.LootTableId,
                        principalTable: "LootTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_LootTableId",
                table: "Npcs",
                column: "LootTableId");

            migrationBuilder.CreateIndex(
                name: "IX_LootEntries_ItemId",
                table: "LootEntries",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_LootEntries_LootTableId",
                table: "LootEntries",
                column: "LootTableId");

            migrationBuilder.CreateIndex(
                name: "IX_LootTables_ContentTypeId",
                table: "LootTables",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LootTables_GameProjectId",
                table: "LootTables",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_LootTables_Name",
                table: "LootTables",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LootEntries");

            migrationBuilder.DropTable(
                name: "LootTables");

            migrationBuilder.DropIndex(
                name: "IX_Npcs_LootTableId",
                table: "Npcs");

            migrationBuilder.DropColumn(
                name: "LootTableId",
                table: "Npcs");
        }
    }
}
