using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.MySql.Migrations
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
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
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
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EffectAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    GameEffectId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ItemId = table.Column<Guid>(type: "char(36)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
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
                })
                .Annotation("MySQL:Charset", "utf8mb4");

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
