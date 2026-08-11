using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.Sqlite.Migrations
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
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecipeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
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
                });

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
            //
            // SQLite kennt keine UUID-Funktion — der Schlüssel wird aus Zufallsbytes
            // zusammengesetzt. Großschreibung ist Pflicht und kein Schönheitsfehler: GUIDs
            // liegen hier als Text, und Textvergleiche sind in SQLite case-sensitiv. Ein
            // kleingeschriebener Schlüssel würde von keiner Abfrage mehr gefunden.
            migrationBuilder.Sql(
                """
                INSERT INTO "RecipeOutputs" ("Id", "RecipeId", "ItemId", "Quantity", "SortOrder")
                SELECT hex(randomblob(4)) || '-' ||
                       hex(randomblob(2)) || '-4' ||
                       substr(hex(randomblob(2)), 2) || '-' ||
                       substr('89AB', 1 + (abs(random()) % 4), 1) ||
                       substr(hex(randomblob(2)), 2) || '-' ||
                       hex(randomblob(6)),
                       "Id", "OutputItemId", max("OutputQuantity", 1), 0
                FROM "Recipes"
                WHERE "OutputItemId" IS NOT NULL;
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
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OutputQuantity",
                table: "Recipes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_OutputItemId",
                table: "Recipes",
                column: "OutputItemId");

            // Zurück passt nur das erste Ziel-Item — mehr trägt das alte Schema nicht.
            migrationBuilder.Sql(
                """
                UPDATE "Recipes"
                SET "OutputItemId" = (
                        SELECT o."ItemId" FROM "RecipeOutputs" o
                        WHERE o."RecipeId" = "Recipes"."Id" ORDER BY o."SortOrder" LIMIT 1),
                    "OutputQuantity" = COALESCE((
                        SELECT o."Quantity" FROM "RecipeOutputs" o
                        WHERE o."RecipeId" = "Recipes"."Id" ORDER BY o."SortOrder" LIMIT 1), 1);
                """);

            migrationBuilder.DropTable(
                name: "RecipeOutputs");
        }
    }
}
