using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.MySql.Migrations
{
    /// <inheritdoc />
    public partial class RecipeOutputs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecipeOutputs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false),
                    RecipeId = table.Column<Guid>(type: "char(36)", nullable: false),
                    ItemId = table.Column<Guid>(type: "char(36)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeOutputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeOutputs_Recipes_RecipeId",
                        column: x => x.RecipeId,
                        principalTable: "Recipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeOutputs_ItemId",
                table: "RecipeOutputs",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeOutputs_RecipeId",
                table: "RecipeOutputs",
                column: "RecipeId");

            // Bestehende Rezepte behalten ihr Ergebnis: aus den beiden alten Spalten wird das
            // erste Ziel-Item. Erst danach fallen die Spalten weg.
            migrationBuilder.Sql(
                """
                INSERT INTO `RecipeOutputs` (`Id`, `RecipeId`, `ItemId`, `Quantity`, `SortOrder`)
                SELECT UUID(), `Id`, `OutputItemId`, GREATEST(`OutputQuantity`, 1), 0
                FROM `Recipes`
                WHERE `OutputItemId` IS NOT NULL;
                """);

            migrationBuilder.DropIndex(
                name: "IX_Recipes_OutputItemId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "OutputItemId",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "OutputQuantity",
                table: "Recipes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OutputItemId",
                table: "Recipes",
                type: "char(36)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OutputQuantity",
                table: "Recipes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_OutputItemId",
                table: "Recipes",
                column: "OutputItemId");

            // Zurück passt nur das erste Ziel-Item — mehr trägt das alte Schema nicht.
            migrationBuilder.Sql(
                """
                UPDATE `Recipes` r
                INNER JOIN `RecipeOutputs` o ON o.`RecipeId` = r.`Id` AND o.`SortOrder` = 0
                SET r.`OutputItemId` = o.`ItemId`, r.`OutputQuantity` = o.`Quantity`;
                """);

            migrationBuilder.DropTable(
                name: "RecipeOutputs");
        }
    }
}
