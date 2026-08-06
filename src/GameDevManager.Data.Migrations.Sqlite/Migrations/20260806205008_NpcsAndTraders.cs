using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class NpcsAndTraders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Npcs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    IsTrader = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsQuestGiver = table.Column<bool>(type: "INTEGER", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Npcs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Npcs_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Npcs_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TraderOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    NpcId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CurrencyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SellPrice = table.Column<double>(type: "REAL", nullable: true),
                    BuyPrice = table.Column<double>(type: "REAL", nullable: true),
                    Stock = table.Column<int>(type: "INTEGER", nullable: true),
                    RestockSeconds = table.Column<double>(type: "REAL", nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TraderOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TraderOffers_Npcs_NpcId",
                        column: x => x.NpcId,
                        principalTable: "Npcs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_ContentTypeId",
                table: "Npcs",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_GameProjectId",
                table: "Npcs",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_GameProjectId_Kind",
                table: "Npcs",
                columns: new[] { "GameProjectId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_IsTrader",
                table: "Npcs",
                column: "IsTrader");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_Name",
                table: "Npcs",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_TraderOffers_CurrencyId",
                table: "TraderOffers",
                column: "CurrencyId");

            migrationBuilder.CreateIndex(
                name: "IX_TraderOffers_ItemId",
                table: "TraderOffers",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_TraderOffers_NpcId",
                table: "TraderOffers",
                column: "NpcId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TraderOffers");

            migrationBuilder.DropTable(
                name: "Npcs");
        }
    }
}
