using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class HealthCheckMutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HealthCheckMutes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "char(36)", nullable: false),
                    CheckKey = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    EntityName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HealthCheckMutes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HealthCheckMutes_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_HealthCheckMutes_GameProjectId_CheckKey_EntityId",
                table: "HealthCheckMutes",
                columns: new[] { "GameProjectId", "CheckKey", "EntityId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HealthCheckMutes");
        }
    }
}
