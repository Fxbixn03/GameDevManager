using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.SqlServer.Migrations
{
    /// <inheritdoc />
    public partial class Classes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CharacterClassId",
                table: "PlayerCharacters",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CharacterClassId",
                table: "Npcs",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CharacterClasses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterClasses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterClasses_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CharacterClasses_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerCharacters_CharacterClassId",
                table: "PlayerCharacters",
                column: "CharacterClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_CharacterClassId",
                table: "Npcs",
                column: "CharacterClassId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterClasses_ContentTypeId",
                table: "CharacterClasses",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterClasses_GameProjectId",
                table: "CharacterClasses",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterClasses_Name",
                table: "CharacterClasses",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterClasses");

            migrationBuilder.DropIndex(
                name: "IX_PlayerCharacters_CharacterClassId",
                table: "PlayerCharacters");

            migrationBuilder.DropIndex(
                name: "IX_Npcs_CharacterClassId",
                table: "Npcs");

            migrationBuilder.DropColumn(
                name: "CharacterClassId",
                table: "PlayerCharacters");

            migrationBuilder.DropColumn(
                name: "CharacterClassId",
                table: "Npcs");
        }
    }
}
