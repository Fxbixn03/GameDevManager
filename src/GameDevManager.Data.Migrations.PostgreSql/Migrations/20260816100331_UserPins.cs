using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class UserPins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserPins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AppUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPins_AppUsers_AppUserId",
                        column: x => x.AppUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPins_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserPins_AppUserId_EntityId",
                table: "UserPins",
                columns: new[] { "AppUserId", "EntityId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserPins_AppUserId_GameProjectId",
                table: "UserPins",
                columns: new[] { "AppUserId", "GameProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPins_GameProjectId",
                table: "UserPins",
                column: "GameProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserPins");
        }
    }
}
