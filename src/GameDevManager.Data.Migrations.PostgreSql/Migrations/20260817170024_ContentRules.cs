using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ContentRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ModuleKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ContentTypeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Check = table.Column<int>(type: "integer", nullable: false),
                    FieldDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TagId = table.Column<Guid>(type: "uuid", nullable: true),
                    Slot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentRules_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContentRules_GameProjectId",
                table: "ContentRules",
                column: "GameProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentRules");
        }
    }
}
