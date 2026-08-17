using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class Webhooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Webhooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    Secret = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    ModuleKeys = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LastDeliveryAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastStatusCode = table.Column<int>(type: "int", nullable: true),
                    LastError = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Webhooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Webhooks_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Webhooks_GameProjectId",
                table: "Webhooks",
                column: "GameProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Webhooks");
        }
    }
}
