using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Diplomacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiplomaticRelations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FactionAId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FactionBId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Stance = table.Column<int>(type: "INTEGER", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiplomaticRelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiplomaticRelations_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DiplomaticRelations_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiplomaticRelations_ContentTypeId",
                table: "DiplomaticRelations",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DiplomaticRelations_FactionAId",
                table: "DiplomaticRelations",
                column: "FactionAId");

            migrationBuilder.CreateIndex(
                name: "IX_DiplomaticRelations_FactionBId",
                table: "DiplomaticRelations",
                column: "FactionBId");

            migrationBuilder.CreateIndex(
                name: "IX_DiplomaticRelations_GameProjectId",
                table: "DiplomaticRelations",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_DiplomaticRelations_Name",
                table: "DiplomaticRelations",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiplomaticRelations");
        }
    }
}
