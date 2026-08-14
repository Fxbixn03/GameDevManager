using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameDevManager.Data.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ContentStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "WorldStates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "StoryEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "SoundEffects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Skills",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Recipes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Rarities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Quests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Npcs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Maps",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "LootTables",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Items",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "GameEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "GameEffects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Factions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "DiplomaticRelations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Dialogues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Cutscenes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Currencies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Collectibles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "CharacterClasses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Achievements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_WorldStates_GameProjectId_Status",
                table: "WorldStates",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StoryEntries_GameProjectId_Status",
                table: "StoryEntries",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SoundEffects_GameProjectId_Status",
                table: "SoundEffects",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Skills_GameProjectId_Status",
                table: "Skills",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_GameProjectId_Status",
                table: "Recipes",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Rarities_GameProjectId_Status",
                table: "Rarities",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Quests_GameProjectId_Status",
                table: "Quests",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Npcs_GameProjectId_Status",
                table: "Npcs",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Maps_GameProjectId_Status",
                table: "Maps",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_LootTables_GameProjectId_Status",
                table: "LootTables",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Items_GameProjectId_Status",
                table: "Items",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GameEvents_GameProjectId_Status",
                table: "GameEvents",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GameEffects_GameProjectId_Status",
                table: "GameEffects",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Factions_GameProjectId_Status",
                table: "Factions",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DiplomaticRelations_GameProjectId_Status",
                table: "DiplomaticRelations",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Dialogues_GameProjectId_Status",
                table: "Dialogues",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Cutscenes_GameProjectId_Status",
                table: "Cutscenes",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_GameProjectId_Status",
                table: "Currencies",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Collectibles_GameProjectId_Status",
                table: "Collectibles",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterClasses_GameProjectId_Status",
                table: "CharacterClasses",
                columns: new[] { "GameProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_GameProjectId_Status",
                table: "Achievements",
                columns: new[] { "GameProjectId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorldStates_GameProjectId_Status",
                table: "WorldStates");

            migrationBuilder.DropIndex(
                name: "IX_StoryEntries_GameProjectId_Status",
                table: "StoryEntries");

            migrationBuilder.DropIndex(
                name: "IX_SoundEffects_GameProjectId_Status",
                table: "SoundEffects");

            migrationBuilder.DropIndex(
                name: "IX_Skills_GameProjectId_Status",
                table: "Skills");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_GameProjectId_Status",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Rarities_GameProjectId_Status",
                table: "Rarities");

            migrationBuilder.DropIndex(
                name: "IX_Quests_GameProjectId_Status",
                table: "Quests");

            migrationBuilder.DropIndex(
                name: "IX_Npcs_GameProjectId_Status",
                table: "Npcs");

            migrationBuilder.DropIndex(
                name: "IX_Maps_GameProjectId_Status",
                table: "Maps");

            migrationBuilder.DropIndex(
                name: "IX_LootTables_GameProjectId_Status",
                table: "LootTables");

            migrationBuilder.DropIndex(
                name: "IX_Items_GameProjectId_Status",
                table: "Items");

            migrationBuilder.DropIndex(
                name: "IX_GameEvents_GameProjectId_Status",
                table: "GameEvents");

            migrationBuilder.DropIndex(
                name: "IX_GameEffects_GameProjectId_Status",
                table: "GameEffects");

            migrationBuilder.DropIndex(
                name: "IX_Factions_GameProjectId_Status",
                table: "Factions");

            migrationBuilder.DropIndex(
                name: "IX_DiplomaticRelations_GameProjectId_Status",
                table: "DiplomaticRelations");

            migrationBuilder.DropIndex(
                name: "IX_Dialogues_GameProjectId_Status",
                table: "Dialogues");

            migrationBuilder.DropIndex(
                name: "IX_Cutscenes_GameProjectId_Status",
                table: "Cutscenes");

            migrationBuilder.DropIndex(
                name: "IX_Currencies_GameProjectId_Status",
                table: "Currencies");

            migrationBuilder.DropIndex(
                name: "IX_Collectibles_GameProjectId_Status",
                table: "Collectibles");

            migrationBuilder.DropIndex(
                name: "IX_CharacterClasses_GameProjectId_Status",
                table: "CharacterClasses");

            migrationBuilder.DropIndex(
                name: "IX_Achievements_GameProjectId_Status",
                table: "Achievements");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "WorldStates");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "StoryEntries");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SoundEffects");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Skills");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Recipes");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Rarities");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Quests");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Npcs");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Maps");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "LootTables");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "GameEvents");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "GameEffects");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Factions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "DiplomaticRelations");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Dialogues");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Cutscenes");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Currencies");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Collectibles");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "CharacterClasses");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Achievements");
        }
    }
}
