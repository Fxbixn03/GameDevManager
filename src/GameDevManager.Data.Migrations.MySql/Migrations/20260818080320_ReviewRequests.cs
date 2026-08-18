using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class ReviewRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReviewRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "char(36)", nullable: false),
                    OwnerEntityId = table.Column<Guid>(type: "char(36)", nullable: false),
                    OwnerModuleKey = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    RequestedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    AssignedUserId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Note = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    DecisionNote = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    DecidedBy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewRequests_AppUsers_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ReviewRequests_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRequests_AssignedUserId",
                table: "ReviewRequests",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRequests_GameProjectId_AssignedUserId_Decision",
                table: "ReviewRequests",
                columns: new[] { "GameProjectId", "AssignedUserId", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRequests_OwnerEntityId",
                table: "ReviewRequests",
                column: "OwnerEntityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewRequests");
        }
    }
}
