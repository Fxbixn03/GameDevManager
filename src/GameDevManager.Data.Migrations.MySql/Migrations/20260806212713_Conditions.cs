using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.MySql.Migrations
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
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    GameProjectId = table.Column<Guid>(type: "char(36)", nullable: false),
                    OwnerId = table.Column<Guid>(type: "char(36)", nullable: false),
                    OwnerModuleKey = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Slot = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Logic = table.Column<int>(type: "int", nullable: false)
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
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Conditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    ConditionSetId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    TargetModuleKey = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    TargetEntityId = table.Column<Guid>(type: "char(36)", nullable: true),
                    Operator = table.Column<int>(type: "int", nullable: false),
                    NumberValue = table.Column<double>(type: "double", nullable: true),
                    BooleanValue = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    TextValue = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
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
                })
                .Annotation("MySQL:Charset", "utf8mb4");

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
