using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Tags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Color = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentTags_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentTagAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentTagId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetModuleKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TargetEntityId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentTagAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentTagAssignments_ContentTags_ContentTagId",
                        column: x => x.ContentTagId,
                        principalTable: "ContentTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContentTagScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContentTagId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ModuleKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentTagScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentTagScopes_ContentTags_ContentTagId",
                        column: x => x.ContentTagId,
                        principalTable: "ContentTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentTagAssignments_ContentTagId",
                table: "ContentTagAssignments",
                column: "ContentTagId");

            migrationBuilder.CreateIndex(
                name: "IX_ContentTagAssignments_TargetEntityId_ContentTagId",
                table: "ContentTagAssignments",
                columns: new[] { "TargetEntityId", "ContentTagId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentTags_GameProjectId_Name",
                table: "ContentTags",
                columns: new[] { "GameProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentTagScopes_ContentTagId",
                table: "ContentTagScopes",
                column: "ContentTagId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentTagAssignments");

            migrationBuilder.DropTable(
                name: "ContentTagScopes");

            migrationBuilder.DropTable(
                name: "ContentTags");
        }
    }
}
