using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class Story : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Body = table.Column<string>(type: "longtext", nullable: true),
                    GameProjectId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryEntries_ContentTypes_ContentTypeId",
                        column: x => x.ContentTypeId,
                        principalTable: "ContentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StoryEntries_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "StoryParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    StoryEntryId = table.Column<Guid>(type: "char(36)", nullable: false),
                    TargetModuleKey = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    TargetEntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoryParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoryParticipants_StoryEntries_StoryEntryId",
                        column: x => x.StoryEntryId,
                        principalTable: "StoryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_StoryEntries_ContentTypeId",
                table: "StoryEntries",
                column: "ContentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryEntries_GameProjectId",
                table: "StoryEntries",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryEntries_GameProjectId_SortOrder",
                table: "StoryEntries",
                columns: new[] { "GameProjectId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryEntries_Name",
                table: "StoryEntries",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_StoryParticipants_StoryEntryId",
                table: "StoryParticipants",
                column: "StoryEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_StoryParticipants_TargetEntityId",
                table: "StoryParticipants",
                column: "TargetEntityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoryParticipants");

            migrationBuilder.DropTable(
                name: "StoryEntries");
        }
    }
}
