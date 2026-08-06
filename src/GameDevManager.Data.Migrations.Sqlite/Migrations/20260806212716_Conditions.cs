using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Conditions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConditionSets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerModuleKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Slot = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Logic = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConditionSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConditionSets_GameProjects_GameProjectId",
                        column: x => x.GameProjectId,
                        principalTable: "GameProjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Conditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConditionSetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetModuleKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    TargetEntityId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Operator = table.Column<int>(type: "INTEGER", nullable: false),
                    NumberValue = table.Column<double>(type: "REAL", nullable: true),
                    BooleanValue = table.Column<bool>(type: "INTEGER", nullable: true),
                    TextValue = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conditions_ConditionSets_ConditionSetId",
                        column: x => x.ConditionSetId,
                        principalTable: "ConditionSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Conditions_ConditionSetId",
                table: "Conditions",
                column: "ConditionSetId");

            migrationBuilder.CreateIndex(
                name: "IX_Conditions_TargetEntityId",
                table: "Conditions",
                column: "TargetEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ConditionSets_GameProjectId",
                table: "ConditionSets",
                column: "GameProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ConditionSets_OwnerId_Slot",
                table: "ConditionSets",
                columns: new[] { "OwnerId", "Slot" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Conditions");

            migrationBuilder.DropTable(
                name: "ConditionSets");
        }
    }
}
